namespace SqlExplorer.Core.Plugins;

/// <summary>
/// Applies the changes the Store staged while the previous run was live. Must run at startup <b>before</b>
/// any plugin is loaded, since the non-collectible load contexts can't swap a plugin's files mid-process
/// (Notes §4.2 — "no real unload"). It:
/// <list type="number">
/// <item>deletes the folders of anything marked <see cref="PluginPendingAction.Remove"/>;</item>
/// <item>promotes every staged <c>&lt;id&gt;.next</c> to <c>&lt;id&gt;</c>, demoting the current copy to a
/// single <c>&lt;id&gt;.prev</c> backup (an update or a rollback both land here);</item>
/// <item>clears the leftover <see cref="PluginPendingAction.Install"/> markers so a fresh install loads
/// normally next time.</item>
/// </list>
/// Best-effort — a single failing folder never blocks startup.
/// </summary>
public static class PluginMaintenance
{
    private const string StagingDir = ".staging";
    private const string NextSuffix = ".next";
    private const string PrevSuffix = ".prev";

    public static void ApplyPending(IPluginStateStore stateStore, string userRoot)
    {
        // 1. Removals first, so a removed id's staged .next is gone before the swap scan runs.
        foreach (var (id, entry) in stateStore.GetAll())
        {
            if (entry.Pending == PluginPendingAction.Remove)
            {
                TryDelete(Path.Combine(userRoot, id));
                TryDelete(Path.Combine(userRoot, id + NextSuffix));
                TryDelete(Path.Combine(userRoot, id + PrevSuffix));
                stateStore.Remove(id);
            }
        }

        // 2. Promote staged replacements: <id>.next -> <id>, keeping the old <id> as <id>.prev.
        PromoteStaged(userRoot);

        // 3. Clear the "restart needed" markers left by a fresh install.
        foreach (var (id, entry) in stateStore.GetAll())
        {
            if (entry.Pending == PluginPendingAction.Install)
            {
                stateStore.Save(id, entry with { Pending = PluginPendingAction.None });
            }
        }
    }

    private static void PromoteStaged(string userRoot)
    {
        if (!Directory.Exists(userRoot))
        {
            return;
        }

        foreach (var nextDir in Directory.EnumerateDirectories(userRoot))
        {
            var name = Path.GetFileName(nextDir);
            if (name == StagingDir || !name.EndsWith(NextSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var target = Path.Combine(userRoot, name[..^NextSuffix.Length]);
            var prev = target + PrevSuffix;

            try
            {
                TryDelete(prev);
                if (Directory.Exists(target))
                {
                    Directory.Move(target, prev);
                }

                Directory.Move(nextDir, target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Leave the staged folder in place; the plugin keeps running its current version and the
                // swap is retried next startup rather than half-applied.
            }
        }
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left on disk; the state entry is still dropped so it won't load, and the next
            // install/remove will overwrite it. Don't let a locked file block startup.
        }
    }
}
