namespace Lionear.SqlExplorer.Core.Plugins;

/// <summary>
/// One row in the host's view of installed plugins: what's on disk (from discovery), whether it loaded
/// this run, and any change staged for next startup. This is the model the Store's Installed tab binds to.
/// </summary>
public sealed record InstalledPlugin(
    string Id,
    string? Name,
    string? Version,
    string? Type,
    PluginOrigin Origin,
    bool Enabled,
    PluginPendingAction Pending,
    bool Loaded,
    string? LoadError,
    string Directory)
{
    /// <summary>Bundled plugins are part of the app — they can't be disabled or uninstalled.</summary>
    public bool CanManage => Origin == PluginOrigin.UserInstalled;
}

/// <summary>
/// The host's authoritative view of installed plugins: merges what <see cref="PluginDiscovery"/> found on
/// disk with how it loaded (<see cref="ProviderLoadResult"/> / <see cref="ToolLoadResult"/>) and its
/// persisted <see cref="PluginStateEntry"/>. Enable/disable/uninstall stage a change in the state store
/// that takes effect on next startup (the non-collectible load contexts can't swap live) — so the
/// mutations here update the row's <see cref="InstalledPlugin.Pending"/>/<see cref="InstalledPlugin.Enabled"/>
/// but never touch the running plugin. The Store shows a "restart needed" banner off the back of that.
/// </summary>
public sealed class PluginCatalogService(
    IPluginStateStore stateStore,
    IReadOnlyList<DiscoveredPlugin> discovered,
    IEnumerable<ProviderLoadResult> providerResults,
    IEnumerable<ToolLoadResult> toolResults)
{
    private List<InstalledPlugin> _plugins = Build(stateStore, discovered, providerResults, toolResults);

    public IReadOnlyList<InstalledPlugin> Installed => _plugins;

    /// <summary>Stage a disable (user plugins only); applied on next startup.</summary>
    public void RequestDisable(string id) => SetEnabled(id, enabled: false);

    /// <summary>Stage a re-enable (user plugins only); applied on next startup.</summary>
    public void RequestEnable(string id) => SetEnabled(id, enabled: true);

    /// <summary>Stage an uninstall (user plugins only); the folder is deleted on next startup.</summary>
    public void RequestUninstall(string id)
    {
        var plugin = RequireManageable(id);
        stateStore.Save(id, stateStore.Get(id) with { Pending = PluginPendingAction.Remove });
        Replace(plugin with { Pending = PluginPendingAction.Remove });
    }

    private void SetEnabled(string id, bool enabled)
    {
        var plugin = RequireManageable(id);
        stateStore.Save(id, stateStore.Get(id) with { Enabled = enabled });
        Replace(plugin with { Enabled = enabled });
    }

    private InstalledPlugin RequireManageable(string id)
    {
        var plugin = _plugins.FirstOrDefault(p => p.Id == id)
            ?? throw new InvalidOperationException($"No installed plugin with id '{id}'.");
        if (!plugin.CanManage)
        {
            throw new InvalidOperationException($"Plugin '{id}' is bundled and cannot be changed.");
        }

        return plugin;
    }

    private void Replace(InstalledPlugin updated)
    {
        var index = _plugins.FindIndex(p => p.Id == updated.Id);
        if (index >= 0)
        {
            _plugins[index] = updated;
        }
    }

    private static List<InstalledPlugin> Build(
        IPluginStateStore stateStore,
        IReadOnlyList<DiscoveredPlugin> discovered,
        IEnumerable<ProviderLoadResult> providerResults,
        IEnumerable<ToolLoadResult> toolResults)
    {
        // How each folder loaded, keyed by its directory (unique per discovered plugin). A disabled
        // plugin is never handed to a loader, so it simply has no outcome here.
        var outcomes = new Dictionary<string, (bool Loaded, string? Error)>(StringComparer.Ordinal);
        foreach (var r in providerResults)
        {
            outcomes[r.PluginDirectory] = (r.Succeeded, r.Error);
        }

        foreach (var r in toolResults)
        {
            outcomes[r.PluginDirectory] = (r.Succeeded, r.Error);
        }

        var state = stateStore.GetAll();
        var rows = new List<InstalledPlugin>();

        foreach (var plugin in discovered)
        {
            // Unreadable manifest: fall back to the folder name as id so the row is still addressable.
            var id = plugin.Id ?? Path.GetFileName(plugin.Directory);
            var entry = state.TryGetValue(id, out var s) ? s : new PluginStateEntry();
            var (loaded, error) = outcomes.TryGetValue(plugin.Directory, out var o)
                ? o
                : (false, plugin.ManifestError);

            rows.Add(new InstalledPlugin(
                id,
                plugin.Manifest?.Name,
                plugin.Manifest?.Version,
                plugin.Manifest?.Type,
                plugin.Origin,
                entry.Enabled,
                entry.Pending,
                loaded,
                error,
                plugin.Directory));
        }

        return rows;
    }
}
