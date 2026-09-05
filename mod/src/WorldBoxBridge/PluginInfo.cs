namespace WorldBoxBridge;

/// <summary>
/// Compile-time constants for the plugin identity.
/// Kept here (not in Plugin.cs) so tests can reference them without pulling in BepInEx.
/// </summary>
internal static class PluginInfo
{
    public const string Guid = "com.fullya99.worldbox-mcp.bridge";
    public const string Name = "WorldBoxBridge";

    /// <summary>
    /// SemVer string. Tracked by release-please (see release-please-config.json) — do NOT
    /// edit by hand; commit with Conventional Commits and the next release PR will bump
    /// this in sync with WorldBoxBridge.csproj's &lt;Version&gt; and pyproject.toml.
    /// </summary>
    public const string Version = "0.4.0"; // x-release-please-version
}
