using SqlExplorer.Core.Plugins;
using SqlExplorer.Sdk;
using SqlExplorer.Sdk.Tools;

namespace SqlExplorer.Core.Store;

/// <summary>
/// The host-side acceptance window for a plugin build. A plugin only declares the version it was built
/// against (<c>minHostApiVersion</c>); whether it is compatible is the host's call — a build is loadable
/// when its version falls in [<see cref="MinSupported"/>, <see cref="Current"/>], because additive host-API
/// bumps stay binary-compatible (new default-interface members, enum values, DTOs). Mirrors the loader gate
/// (<see cref="ProviderHostApi.IsCompatible"/>), so the Store never offers a plugin the loader would refuse.
/// </summary>
public readonly record struct HostApiCompat(int Current, int MinSupported)
{
    public bool Accepts(int pluginMinHostApiVersion) =>
        pluginMinHostApiVersion >= MinSupported && pluginMinHostApiVersion <= Current;
}

/// <summary>
/// Resolves the host API acceptance window a store entry must be judged against. Providers and tools
/// version independently (<see cref="ProviderHostApi"/> vs <see cref="ToolHostApi"/>), so the plugin's
/// <c>type</c> picks which contract's window applies.
/// </summary>
public static class HostApiVersions
{
    public static HostApiCompat CompatFor(string? pluginType) => pluginType switch
    {
        PluginManifest.Types.Tool => new(ToolHostApi.Version, ToolHostApi.MinimumSupported),
        _ => new(ProviderHostApi.Version, ProviderHostApi.MinimumSupported) // provider, or unspecified
    };
}
