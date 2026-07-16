namespace SqlExplorer.Sdk.Tools;

/// <summary>
/// Versioning gate between the host and tool plugins, separate from <c>ProviderHostApi</c> so the two
/// plugin kinds evolve independently. A tool's <c>plugin.json</c> declares the version it was built for;
/// the loader refuses one this host cannot satisfy.
/// </summary>
public static class ToolHostApi
{
    // v2 (2026-07-14): added IToolPlugin.MenuPath (default []) — tools can declare a nested submenu path
    //                  (Tools ▸ Shrink ▸ Database) instead of only appearing flat under Tools. Also
    //                  IToolUiContext.QueryAsync (Route-B live-data hook). Both additive.
    public const int Version = 2;

    /// <summary>Oldest tool ABI this host still loads. v1→v2 was additive (IToolPlugin.MenuPath default [],
    /// IToolUiContext.QueryAsync on the host-provided context), so v1 tools load on a v2 host.</summary>
    public const int MinimumSupported = 1;

    public static bool IsCompatible(int pluginVersion) =>
        pluginVersion >= MinimumSupported && pluginVersion <= Version;
}
