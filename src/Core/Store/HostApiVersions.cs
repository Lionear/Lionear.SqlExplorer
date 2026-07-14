using Lionear.SqlExplorer.Core.Plugins;
using Lionear.SqlExplorer.Sdk;
using Lionear.SqlExplorer.Sdk.Tools;

namespace Lionear.SqlExplorer.Core.Store;

/// <summary>
/// Resolves which host API version a store entry's compatibility must be judged against. Providers and
/// tools version independently (<see cref="ProviderHostApi"/> vs <see cref="ToolHostApi"/>), so the same
/// numeric range in an <c>index.json</c> means different things depending on the plugin's <c>type</c>.
/// </summary>
public static class HostApiVersions
{
    public static int For(string? pluginType) => pluginType switch
    {
        PluginManifest.Types.Tool => ToolHostApi.Version,
        _ => ProviderHostApi.Version // provider, or unspecified: default to the provider contract
    };
}
