namespace SqlExplorer.Sdk.Extensibility;

/// <summary>What a subsystem plugin needs to create one host-managed connection: identity, provider and the
/// connection-field values (secrets included — the host routes password-type fields to the OS keychain,
/// exactly as the connection dialog does). <see cref="Folder"/> groups it in the tree.</summary>
public sealed record NewConnectionSpec(
    string Name,
    string ProviderId,
    IReadOnlyDictionary<string, string?> Values,
    string? Folder = null);

/// <summary>
/// Lets a subsystem plugin manage <em>real</em> connections in the host's connection list, tagged with the
/// plugin as their origin (which drives a "managed by X" tree badge). On <see cref="IPluginRuntimeContext.Connections"/>
/// when the plugin declared the <see cref="PluginCapabilities.Connections"/> capability. Deliberately
/// scoped to the plugin's own connections — it can never see or remove the user's, or another plugin's.
/// A plugin does not inject raw tree nodes; it creates connections the host already knows how to render.
/// </summary>
public interface IManagedConnections
{
    /// <summary>Create a connection tagged with this plugin as origin; returns its new id.</summary>
    string Create(NewConnectionSpec spec);

    /// <summary>Remove a connection — only if this plugin created it (origin match); otherwise a no-op.</summary>
    void Remove(string connectionId);

    /// <summary>The ids of the connections this plugin created.</summary>
    IReadOnlyList<string> Mine();
}
