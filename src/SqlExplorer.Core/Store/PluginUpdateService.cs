using SqlExplorer.Core.Plugins;

namespace SqlExplorer.Core.Store;

/// <summary>An available update for an installed plugin: the highest compatible catalog version that is
/// newer than what's installed.</summary>
public sealed record PluginUpdate(string Id, string? CurrentVersion, StoreEntry Entry, StoreVersion Target);

/// <summary>
/// Cross-references installed plugins against the merged catalog to find updates, and drives
/// "Update all". Only user-installed plugins are considered (bundled ones ship with the app); a plugin is
/// updatable when the catalog's highest host-compatible version is a newer SemVer than the installed one.
/// <see cref="UpdateAllAsync"/> reuses the single-plugin install step per update and keeps going when one
/// fails, returning a per-plugin outcome.
/// </summary>
public sealed class PluginUpdateService(IPluginInstaller installer, IPluginPinStore pins)
{
    public IReadOnlyList<PluginUpdate> DetectUpdates(IReadOnlyList<InstalledPlugin> installed, StoreCatalog catalog)
    {
        var byId = catalog.Entries.ToDictionary(e => e.Entry.Id, e => e.Entry, StringComparer.Ordinal);
        var allPins = pins.GetAll();
        var updates = new List<PluginUpdate>();

        foreach (var plugin in installed)
        {
            if (plugin.Origin != PluginOrigin.UserInstalled || !byId.TryGetValue(plugin.Id, out var entry))
            {
                continue;
            }

            // Pinned = user opted out of auto-updates; skip regardless of what's on the catalog. Clearing
            // the pin (Store UI) re-enables updates on the next detect pass.
            if (allPins.ContainsKey(plugin.Id))
            {
                continue;
            }

            var target = entry.HighestCompatibleVersion(HostApiVersions.CompatFor(entry.Type));
            if (target is not null && SemVer.Compare(target.Version, plugin.Version) > 0)
            {
                updates.Add(new PluginUpdate(plugin.Id, plugin.Version, entry, target));
            }
        }

        return updates;
    }

    public async Task<IReadOnlyList<InstallOutcome>> UpdateAllAsync(
        IReadOnlyList<PluginUpdate> updates, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        var outcomes = new List<InstallOutcome>();
        foreach (var update in updates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                outcomes.Add(await installer.InstallAsync(update.Entry, update.Target, progress, ct));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One plugin failing must not abort the batch.
                outcomes.Add(InstallOutcome.Fail(update.Id, ex.Message));
            }
        }

        return outcomes;
    }
}
