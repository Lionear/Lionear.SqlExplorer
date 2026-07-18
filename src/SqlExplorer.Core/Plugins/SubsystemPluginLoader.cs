using SqlExplorer.Core.Localization;
using SqlExplorer.Sdk.Extensibility;
using SqlExplorer.Sdk.Localization;
using SqlExplorer.Sdk.Tools;

namespace SqlExplorer.Core.Plugins;

/// <summary>Outcome of loading one <c>type: "extension"</c> plugin: the subsystem it contributed and its
/// capability-gated runtime context, or an error explaining why it was skipped.</summary>
public sealed record SubsystemLoadResult(
    string PluginDirectory, string? Id, ISubsystemPlugin? Plugin, IPluginRuntimeContext? Context, string? Error)
{
    public bool Succeeded => Error is null && Plugin is not null && Context is not null;
}

/// <summary>
/// Loads <c>type: "extension"</c> plugins (SE-164) — standing subsystems. Mirrors <see cref="ToolPluginLoader"/>
/// and reuses <see cref="ProviderLoadContext"/> (SDK + Avalonia shared with the host). For each it finds the
/// single <see cref="ISubsystemPlugin"/> implementation and builds an <see cref="IPluginRuntimeContext"/>
/// gated on the manifest's declared capabilities. The host then calls <c>Initialize</c> and holds the
/// instance for <c>Deactivate</c> at shutdown.
/// </summary>
public sealed class SubsystemPluginLoader
{
    private readonly ILocalizer? _hostLocalizer;
    private readonly Func<string, IPluginStorage> _storageProvider;
    private readonly Action<string>? _log;

    /// <param name="storageProvider">Builds the plugin-scoped storage for a plugin id (the host injects the
    /// Infrastructure impl, since Core can't). Only wired into the context when the plugin declared the
    /// <see cref="PluginCapabilities.Storage"/> capability.</param>
    public SubsystemPluginLoader(
        Func<string, IPluginStorage> storageProvider, ILocalizer? hostLocalizer = null, Action<string>? log = null)
    {
        _storageProvider = storageProvider;
        _hostLocalizer = hostLocalizer;
        _log = log;
    }

    public IReadOnlyList<SubsystemLoadResult> Load(IEnumerable<DiscoveredPlugin> plugins)
    {
        var results = new List<SubsystemLoadResult>();
        foreach (var plugin in plugins)
        {
            // Skip everything the provider/tool/mcp loaders own; only standing-subsystem extensions here.
            if (plugin.Manifest is not { Type: PluginManifest.Types.Extension } manifest)
            {
                continue;
            }

            results.Add(LoadOne(plugin.Directory, manifest));
        }

        return results;
    }

    private SubsystemLoadResult LoadOne(string dir, PluginManifest manifest)
    {
        try
        {
            if (!ToolHostApi.IsCompatible(manifest.HostApiVersion))
            {
                return new SubsystemLoadResult(dir, manifest.Id, null, null,
                    $"Extension '{manifest.Id}' targets host API v{manifest.HostApiVersion}, this host is v{ToolHostApi.Version}.");
            }

            var assemblyPath = Path.Combine(dir, manifest.EntryAssembly);
            if (!File.Exists(assemblyPath))
            {
                return new SubsystemLoadResult(dir, manifest.Id, null, null,
                    $"Entry assembly '{manifest.EntryAssembly}' not found in '{dir}'.");
            }

            var alc = new ProviderLoadContext(assemblyPath);
            var assembly = alc.LoadFromAssemblyPath(assemblyPath);

            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(ISubsystemPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });
            if (pluginType is null)
            {
                return new SubsystemLoadResult(dir, manifest.Id, null, null,
                    $"Assembly '{manifest.EntryAssembly}' has no public ISubsystemPlugin implementation.");
            }

            if (Activator.CreateInstance(pluginType) is not ISubsystemPlugin subsystem)
            {
                return new SubsystemLoadResult(dir, manifest.Id, null, null,
                    $"Could not instantiate '{pluginType.Name}' as an ISubsystemPlugin.");
            }

            var localizer = _hostLocalizer is not null && !string.IsNullOrWhiteSpace(manifest.Localization)
                ? PluginLocalizer.TryLoad(assembly, manifest.Localization!, _hostLocalizer,
                    warn => _log?.Invoke($"{manifest.Id}: {warn}"))
                : null;

            var context = CreateContext(manifest.Id, manifest.Capabilities, _storageProvider, localizer, _log);
            return new SubsystemLoadResult(dir, manifest.Id, subsystem, context, null);
        }
        catch (Exception ex)
        {
            return new SubsystemLoadResult(dir, null, null, null, ex.Message);
        }
    }

    /// <summary>Build a runtime context with its services gated on the declared <paramref name="capabilities"/>
    /// — a service the plugin didn't declare (and the user didn't consent to) is null. Public + static so it
    /// can be exercised without a real plugin assembly. <paramref name="connectionsProvider"/> is optional
    /// (null until the connections seam is wired post-build).</summary>
    public static IPluginRuntimeContext CreateContext(
        string pluginId,
        IReadOnlyList<string> capabilities,
        Func<string, IPluginStorage> storageProvider,
        IPluginLocalizer? localizer,
        Action<string>? log,
        Func<string, IManagedConnections>? connectionsProvider = null) =>
        new PluginRuntimeContext(
            pluginId,
            capabilities.Contains(PluginCapabilities.Storage) ? storageProvider(pluginId) : null,
            capabilities.Contains(PluginCapabilities.Connections) ? connectionsProvider?.Invoke(pluginId) : null,
            localizer ?? NullPluginLocalizer.Instance,
            log);
}
