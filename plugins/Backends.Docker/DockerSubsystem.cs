namespace SqlExplorer.Backends.Docker;

/// <summary>
/// The Local Containers (Docker) subsystem plugin — the first consumer of the SE-164 extensibility platform,
/// and the target the host-built Docker regie (SE-113 fase-1 Core) migrates into. It dogfoods two seams: the
/// <c>storage</c> seam (its persisted container registry, via the capability-gated
/// <see cref="IPluginRuntimeContext.Storage"/>) and the <c>connections</c> seam (each managed container
/// surfaces as a real host connection, tagged with this plugin as origin, via
/// <see cref="IPluginRuntimeContext.Connections"/>). The panel / background / menu seams (and the migrated
/// compose/CLI logic) land next.
/// </summary>
public sealed class DockerSubsystem : ISubsystemPlugin
{
    private const string RegistryKey = "containers";

    private IPluginRuntimeContext? _context;

    public void Initialize(IPluginRuntimeContext context)
    {
        _context = context;

        if (context.Storage is not { } storage)
        {
            context.Log("Local Containers: the 'storage' capability was not granted — the container registry is unavailable.");
            return;
        }

        // Read-then-write round-trip against plugin-scoped storage: proves both directions of the seam work
        // and that the registry persists across restarts. (The real registry payload arrives with the migration.)
        var containers = storage.Load<List<ManagedContainerRecord>>(RegistryKey) ?? [];
        storage.Save(RegistryKey, containers);
        context.Log($"Local Containers: {containers.Count} managed container(s) restored from storage.");

        ReconcileConnections(context, containers);
    }

    // Dogfood the connections seam: every managed container should surface as a real host connection the user
    // sees in the tree (badged "managed by Local Containers" off its origin) and can't accidentally orphan.
    // Idempotent and honest — an empty registry creates nothing, so a fresh install adds no connections until
    // there is actually a container to represent. The concrete container→connection mapping is refined with
    // the SE-113 migration; this proves the plugin can create origin-tagged connections through the host.
    private static void ReconcileConnections(IPluginRuntimeContext context, List<ManagedContainerRecord> containers)
    {
        if (context.Connections is not { } connections)
        {
            return;
        }

        // We only add on a clean slate for now (no per-container id tracking yet) — enough to prove the seam
        // without duplicating the set on every restart.
        if (connections.Mine().Count > 0 || containers.Count == 0)
        {
            context.Log($"Local Containers: {connections.Mine().Count} managed connection(s) already present.");
            return;
        }

        foreach (var container in containers)
        {
            connections.Create(new NewConnectionSpec(
                container.Name,
                container.ProviderId,
                new Dictionary<string, string?> { ["host"] = "localhost", ["port"] = container.HostPort.ToString() },
                Folder: "Local Containers"));
        }

        context.Log($"Local Containers: created {containers.Count} managed connection(s).");
    }

    public void Deactivate() => _context = null;

    /// <summary>Placeholder registry row — replaced by the migrated SE-113 <c>ManagedContainer</c>.</summary>
    private sealed record ManagedContainerRecord(string Name, string ProviderId, int HostPort);
}
