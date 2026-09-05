"""Unit tests for ``scripts/gen-docs.py``.

The script is the guard that keeps the documented tool surface honest, so it needs a guard of
its own: a check that stays silent when the docs drift is worse than no check at all. These
tests drive it against a throwaway tree rather than the repository, which is why every entry
point takes a ``root``.
"""

from __future__ import annotations

import importlib.util
import sys
from pathlib import Path
from typing import TYPE_CHECKING

import pytest

if TYPE_CHECKING:
    from types import ModuleType

REPO_ROOT = Path(__file__).resolve().parents[3]
SCRIPT = REPO_ROOT / "scripts" / "gen-docs.py"


def _load() -> ModuleType:
    # Not importable as a package: it is a hyphenated script outside the distribution.
    spec = importlib.util.spec_from_file_location("gen_docs", SCRIPT)
    assert spec is not None
    assert spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


gen_docs = _load()


@pytest.fixture
def surface() -> object:
    return gen_docs.Surface(
        by_category={"Meta": ["worldbox_health"], "Read": ["worldbox_get_tile"]},
        bridge_commands=1,
    )


@pytest.fixture
def docs(tmp_path: Path) -> Path:
    """A tree whose docs agree with the two-tool surface above."""
    (tmp_path / "docs").mkdir()
    body = (
        "# Title\n\n"
        "<!-- gen-docs:begin total -->2<!-- gen-docs:end total --> tools, spelled "
        "<!-- gen-docs:begin total-words -->Two<!-- gen-docs:end total-words -->, over "
        "<!-- gen-docs:begin bridge-commands -->1<!-- gen-docs:end bridge-commands --> command.\n\n"
        "`worldbox_health` and `worldbox_get_tile`.\n"
    )
    for name in ("README.md", "docs/index.md", "docs/multi-agent.md", "docs/command-reference.md"):
        (tmp_path / name).write_text(body, encoding="utf-8")
    return tmp_path


def test_a_consistent_tree_reports_nothing(surface: object, docs: Path) -> None:
    report = gen_docs.run(surface, docs, write=False)
    assert report.problems == []


def test_stale_count_is_reported(surface: object, docs: Path) -> None:
    index = docs / "docs/index.md"
    index.write_text(index.read_text().replace(">2<", ">26<"), encoding="utf-8")

    report = gen_docs.run(surface, docs, write=False)

    assert any("total: '26' should be '2'" in p for p in report.problems)


def test_missing_tool_in_an_inventory_file_is_reported(surface: object, docs: Path) -> None:
    index = docs / "docs/index.md"
    index.write_text(index.read_text().replace("`worldbox_get_tile`", ""), encoding="utf-8")

    report = gen_docs.run(surface, docs, write=False)

    assert any("never mentions worldbox_get_tile" in p for p in report.problems)


def test_tool_that_does_not_exist_is_reported(surface: object, docs: Path) -> None:
    (docs / "docs/protocol.md").write_text("Call `worldbox_teleport`.\n", encoding="utf-8")

    report = gen_docs.run(surface, docs, write=False)

    assert any("worldbox_teleport" in p for p in report.problems)


def test_known_non_tools_are_not_reported(surface: object, docs: Path) -> None:
    (docs / "docs/protocol.md").write_text(
        "The `worldbox_mcp` package reads `worldbox_version` from `worldbox_dir`.\n",
        encoding="utf-8",
    )

    report = gen_docs.run(surface, docs, write=False)

    assert report.problems == []


def test_command_added_on_one_side_only_is_reported(docs: Path) -> None:
    lopsided = gen_docs.Surface(
        by_category={"Meta": ["worldbox_health"], "Read": ["worldbox_get_tile"]},
        bridge_commands=5,
    )

    report = gen_docs.run(lopsided, docs, write=False)

    assert any("one side of the bridge only" in p for p in report.problems)


def test_write_repairs_a_stale_region(surface: object, docs: Path) -> None:
    index = docs / "docs/index.md"
    index.write_text(index.read_text().replace(">Two<", ">Twenty-six<"), encoding="utf-8")

    report = gen_docs.run(surface, docs, write=True)

    assert report.problems == []
    assert ">Two<" in index.read_text()


def test_a_region_nobody_uses_is_reported(surface: object, tmp_path: Path) -> None:
    (tmp_path / "docs").mkdir()
    for name in ("README.md", "docs/index.md", "docs/multi-agent.md", "docs/command-reference.md"):
        (tmp_path / name).write_text("`worldbox_health` `worldbox_get_tile`\n", encoding="utf-8")

    report = gen_docs.run(surface, tmp_path, write=False)

    assert any("region 'total' is generated but no file uses it" in p for p in report.problems)


@pytest.mark.parametrize(
    ("number", "spelled"),
    [
        (0, "Zero"),
        (9, "Nine"),
        (13, "Thirteen"),
        (20, "Twenty"),
        (29, "Twenty-nine"),
        (99, "Ninety-nine"),
    ],
)
def test_spelling(number: int, spelled: str) -> None:
    assert gen_docs.spell(number) == spelled


def test_spelling_refuses_what_it_cannot_write() -> None:
    with pytest.raises(ValueError, match="extend spell"):
        gen_docs.spell(100)
