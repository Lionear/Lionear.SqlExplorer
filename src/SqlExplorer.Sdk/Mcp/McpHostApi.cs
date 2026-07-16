namespace SqlExplorer.Sdk.Mcp;

/// <summary>
/// Versioning gate between the host and <c>mcp</c> plugins, separate from ProviderHostApi/ToolHostApi so
/// the three plugin kinds evolve independently. An MCP plugin's <c>plugin.json</c> declares the version it
/// was built for; the loader refuses one this host cannot satisfy.
/// </summary>
public static class McpHostApi
{
    // v1 (2026-07-14): initial IMcpPlugin/IMcpHost contract — list_connections/get_schema/run_query/
    //                  explain_query, host-side authz (AiAccess + SQL classification + caps + audit).
    public const int Version = 1;

    /// <summary>Oldest MCP ABI this host still loads. Only one version exists so far; a future additive
    /// bump keeps this at 1, a breaking change raises it.</summary>
    public const int MinimumSupported = 1;

    public static bool IsCompatible(int pluginVersion) =>
        pluginVersion >= MinimumSupported && pluginVersion <= Version;
}
