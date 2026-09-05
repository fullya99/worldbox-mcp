#!/usr/bin/env python3
"""Keep the documented tool surface in step with the tools the server actually registers.

The tool count is stated in six files and has drifted three times, most recently when
``docs/index.md`` still claimed twenty-six tools and was missing three of them outright.
This script removes the drift in two ways.

**Generated regions.** Anything mechanical lives between markers and is rewritten by
``--write``::

    <!-- gen-docs:begin total -->29<!-- gen-docs:end total -->

Everything outside the markers is hand-written prose and is never touched, which is why the
per-version asset counts, the argument columns and the error model survive. The count in
``docs/compatibility.md`` is deliberately not marked: that row records what a released
version shipped, and it must not move when the surface grows.

**Inventory checks.** The category tables carry editorial columns, so rewriting them would
cost more than it saves. They are verified instead: a file listed in :data:`INVENTORY_FILES`
must name every registered tool. Any ``worldbox_*`` identifier anywhere in the docs must
also resolve to a real tool.

Source of truth is the MCP server itself, imported and queried in-process, so no game and no
network are involved. The C# side is counted from source and cross-checked against it, which
catches a command added on one side of the bridge only.

Usage::

    python scripts/gen-docs.py --check   # report drift, exit 1 if any (CI)
    python scripts/gen-docs.py --write   # rewrite the generated regions
"""

from __future__ import annotations

import argparse
import asyncio
import importlib
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SERVER_SRC = REPO_ROOT / "server" / "src"
COMMANDS_DIR = REPO_ROOT / "mod" / "src" / "WorldBoxBridge" / "Commands"

# Category name and the tools submodule that registers it, in the order build_server calls
# them. A new category means a new module, so this list is the one place to extend.
CATEGORY_MODULES: list[tuple[str, str]] = [
    ("Meta", "meta"),
    ("Discovery", "discovery"),
    ("Action", "action"),
    ("Read", "read"),
    ("Control", "control"),
    ("Bus", "bus"),
]

# Files whose tables are meant to be a complete inventory of the tool surface.
INVENTORY_FILES: list[str] = [
    "README.md",
    "docs/index.md",
    "docs/multi-agent.md",
    "docs/command-reference.md",
]

SKIP_DIRS = frozenset({".git", ".venv", "archives", "node_modules", "site"})

# Identifiers that match the tool naming pattern without being tools. Add to this set when a
# new one appears, the alternative is a check that lets a renamed tool slip through.
NOT_A_TOOL: frozenset[str] = frozenset(
    {
        "worldbox_mcp",  # the Python package
        "worldbox_version",  # a field of the /health payload
        "worldbox_dir",  # a server setting
    }
)

# Every command declares its wire name with a `Name =>` property. PauseCommand.cs holds two
# of them, which is why counting files gives the wrong answer.
CSHARP_NAME = re.compile(r"public\s+(?:override\s+|sealed\s+override\s+)?string\s+Name\s*=>")

REGION = re.compile(
    r"(?P<open><!-- gen-docs:begin (?P<name>[a-z][a-z0-9-]*) -->)"
    r"(?P<body>.*?)"
    r"(?P<close><!-- gen-docs:end (?P=name) -->)",
    re.DOTALL,
)

TOOL_MENTION = re.compile(r"\bworldbox_[a-z0-9_]+")

ONES = [
    "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
    "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
    "eighteen", "nineteen",
]
TENS = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"]


def spell(n: int) -> str:
    """Spell out 0 to 99, capitalised, the way the headings do ('Twenty-nine')."""
    if not 0 <= n < 100:
        raise ValueError(f"no spelling for {n}, extend spell() if the surface grew that much")
    word = ONES[n] if n < 20 else TENS[n // 10] + ("-" + ONES[n % 10] if n % 10 else "")
    return word.capitalize()


@dataclass
class Surface:
    """The tool surface as the code defines it."""

    by_category: dict[str, list[str]]
    bridge_commands: int

    @property
    def names(self) -> set[str]:
        return {name for names in self.by_category.values() for name in names}

    @property
    def total(self) -> int:
        return sum(len(names) for names in self.by_category.values())


@dataclass
class Report:
    """What drifted. No problems means the docs agree with the code."""

    problems: list[str] = field(default_factory=list)
    rewrites: list[str] = field(default_factory=list)

    def fail(self, message: str) -> None:
        self.problems.append(message)


def markdown_files(root: Path) -> list[Path]:
    return sorted(
        path
        for path in root.rglob("*.md")
        if not SKIP_DIRS.intersection(path.relative_to(root).parts)
    )


async def _collect_tools() -> dict[str, list[str]]:
    from mcp.server.mcpserver import MCPServer

    from worldbox_mcp.client import BridgeClient
    from worldbox_mcp.config import BridgeAddress

    # The client is never called: registration only needs something to close over.
    client = BridgeClient(BridgeAddress(host="127.0.0.1", port=8723, token="gen-docs"))
    try:
        by_category: dict[str, list[str]] = {}
        seen: dict[str, str] = {}
        for category, module_name in CATEGORY_MODULES:
            server = MCPServer(name="gen-docs")
            module = importlib.import_module(f"worldbox_mcp.tools.{module_name}")
            module.register(server, client)
            names = sorted(tool.name for tool in await server.list_tools())
            for name in names:
                if name in seen:
                    raise RuntimeError(f"{name} registered by both {seen[name]} and {category}")
                seen[name] = category
            by_category[category] = names
        return by_category
    finally:
        await client.aclose()


def read_surface(commands_dir: Path = COMMANDS_DIR) -> Surface:
    """Import the server, register every tool module, and count the C# side too."""
    if str(SERVER_SRC) not in sys.path:
        sys.path.insert(0, str(SERVER_SRC))
    by_category = asyncio.run(_collect_tools())
    bridge_commands = sum(
        len(CSHARP_NAME.findall(path.read_text(encoding="utf-8")))
        for path in sorted(commands_dir.rglob("*.cs"))
    )
    return Surface(by_category=by_category, bridge_commands=bridge_commands)


def region_values(surface: Surface) -> dict[str, str]:
    """What each named region should contain."""
    return {
        "total": str(surface.total),
        "total-words": spell(surface.total),
        "bridge-commands": str(surface.bridge_commands),
    }


def sync_regions(surface: Surface, root: Path, *, write: bool, report: Report) -> None:
    values = region_values(surface)
    seen: set[str] = set()

    for path in markdown_files(root):
        text = path.read_text(encoding="utf-8")
        if "gen-docs:begin" not in text:
            continue
        rel = path.relative_to(root)
        stale: list[str] = []

        def replace(match: re.Match[str]) -> str:
            name = match.group("name")
            seen.add(name)
            if name not in values:
                report.fail(f"{rel}: unknown region '{name}', expected one of {sorted(values)}")
                return match.group(0)
            wanted = values[name]
            if match.group("body") != wanted:
                stale.append(f"{name}: '{match.group('body')}' should be '{wanted}'")
            return f"{match.group('open')}{wanted}{match.group('close')}"

        updated = REGION.sub(replace, text)
        if not stale:
            continue
        if write:
            path.write_text(updated, encoding="utf-8")
            report.rewrites.append(f"{rel}: {', '.join(stale)}")
        else:
            for detail in stale:
                report.fail(f"{rel}: {detail}")

    for name in sorted(set(values) - seen):
        report.fail(f"region '{name}' is generated but no file uses it")


def check_inventories(surface: Surface, root: Path, report: Report) -> None:
    known = surface.names
    for rel in INVENTORY_FILES:
        path = root / rel
        if not path.is_file():
            report.fail(f"{rel}: listed as an inventory file but missing")
            continue
        mentioned = set(TOOL_MENTION.findall(path.read_text(encoding="utf-8"))) - NOT_A_TOOL
        missing = sorted(known - mentioned)
        if missing:
            report.fail(f"{rel}: never mentions {', '.join(missing)}")


def check_mentions(surface: Surface, root: Path, report: Report) -> None:
    allowed = surface.names | NOT_A_TOOL
    for path in markdown_files(root):
        unknown = set(TOOL_MENTION.findall(path.read_text(encoding="utf-8"))) - allowed
        if unknown:
            report.fail(
                f"{path.relative_to(root)}: mentions {', '.join(sorted(unknown))}, which no tool "
                f"is named. Either the docs are stale, or add it to NOT_A_TOOL in this script."
            )


def check_parity(surface: Surface, report: Report) -> None:
    # /capabilities is served by the HTTP layer, not by an ICommand, so the mod declares
    # exactly one command fewer than the server exposes tools.
    expected = surface.bridge_commands + 1
    if expected != surface.total:
        report.fail(
            f"the mod declares {surface.bridge_commands} commands, so the server should expose "
            f"{expected} tools, but it registers {surface.total}. A command was probably added "
            f"on one side of the bridge only."
        )


def run(surface: Surface, root: Path, *, write: bool) -> Report:
    report = Report()
    sync_regions(surface, root, write=write, report=report)
    check_inventories(surface, root, report)
    check_mentions(surface, root, report)
    check_parity(surface, report)
    return report


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Keep the documented tool surface in step with the registered tools."
    )
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="report drift without writing")
    mode.add_argument("--write", action="store_true", help="rewrite the generated regions")
    parser.add_argument(
        "--root", type=Path, default=REPO_ROOT, help="repository root (defaults to this one)"
    )
    args = parser.parse_args(argv)

    surface = read_surface()
    report = run(surface, args.root, write=args.write)

    for line in report.rewrites:
        print(f"updated {line}")

    if report.problems:
        print(f"\n{len(report.problems)} problem(s):", file=sys.stderr)
        for problem in report.problems:
            print(f"  {problem}", file=sys.stderr)
        if not args.write:
            print(
                "\nRun `python scripts/gen-docs.py --write` to refresh the generated regions.",
                file=sys.stderr,
            )
        return 1

    categories = ", ".join(f"{c} {len(n)}" for c, n in surface.by_category.items())
    print(f"{surface.total} tools ({categories}); {surface.bridge_commands} bridge commands. Docs agree.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
