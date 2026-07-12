namespace Lionear.SqlExplorer.Sdk;

/// <summary>
/// The single seam every database engine plugs into — and the public contract
/// third-party providers implement. UI and view-models depend only on this
/// abstraction, never on a concrete driver.
/// </summary>
/// <remarks>
/// A provider carries no identity of its own: which engine it is comes from the
/// <c>id</c> in its <c>plugin.json</c> manifest, attached by the loader. That keeps the
/// set of engines open — a third party ships a new provider with a new manifest id,
/// no host enum to extend.
/// </remarks>
public interface IDbProvider
{
    /// <summary>Human-friendly provider name shown in the UI (e.g. "Microsoft SQL Server").</summary>
    string DisplayName { get; }

    /// <summary>Icon shown on this provider's connection nodes; null falls back to a host default.</summary>
    ProviderIcon? Icon { get; }

    ISqlDialect Dialect { get; }

    /// <summary>The fields this provider needs to build a connection — drives the connection dialog.</summary>
    IReadOnlyList<ConnectionField> ConnectionFields { get; }

    /// <summary>
    /// Compose a connection string from field values (keyed by <see cref="ConnectionField.Key"/>),
    /// including any secret just fetched from the keychain. The provider owns its own syntax/escaping.
    /// </summary>
    string BuildConnectionString(IReadOnlyDictionary<string, string?> values);

    Task<bool> TestConnectionAsync(ConnectionProfile profile, CancellationToken ct);

    /// <summary>
    /// Lazily list the children of a schema-tree node. <paramref name="ancestors"/> is the
    /// path from the connection root down to the node being expanded; an empty list means the
    /// connection's own top-level nodes. On-demand loading keeps large servers from being fully
    /// introspected up front (DBeaver-style). Each provider decides its own hierarchy shape.
    /// </summary>
    Task<IReadOnlyList<DbTreeNode>> GetChildNodesAsync(
        ConnectionProfile profile,
        IReadOnlyList<DbNodeRef> ancestors,
        CancellationToken ct);

    Task<QueryResult> ExecuteQueryAsync(ConnectionProfile profile, string sql, CancellationToken ct);

    /// <summary>
    /// Run a batch of parameterised statements inside a single transaction and return the
    /// total number of affected rows. Any failure rolls the whole batch back — this is the
    /// commit step of the editable-grid save-flow (see Notes.md §8). The host generates the
    /// statements (dialect-quoted INSERT/UPDATE/DELETE); the provider owns parameter binding.
    /// </summary>
    Task<int> ExecuteBatchAsync(
        ConnectionProfile profile,
        IReadOnlyList<SqlStatement> statements,
        CancellationToken ct);

    /// <summary>What DDL Create can build for this provider, and which tree-node kind each "New …"
    /// action appears under. Empty = nothing creatable (host hides every DDL Create menu item).</summary>
    IReadOnlyList<CreateCapability> CreateCapabilities { get; }

    /// <summary>Column-type suggestions offered in the DDL Create table dialog's type dropdown.</summary>
    IReadOnlyList<string> ColumnTypes { get; }

    /// <summary>Render <paramref name="spec"/> as dialect-correct DDL for the host to preview (and let
    /// the user edit) before running it via <see cref="ExecuteDdlAsync"/>.</summary>
    SqlStatement BuildCreateStatement(CreateObjectSpec spec);

    /// <summary>
    /// Run one DDL statement (typically the — possibly user-edited — text from
    /// <see cref="BuildCreateStatement"/>) outside any transaction: some engines forbid statements like
    /// <c>CREATE DATABASE</c> inside one, which rules out reusing <see cref="ExecuteBatchAsync"/>.
    /// </summary>
    Task ExecuteDdlAsync(ConnectionProfile profile, string sql, CancellationToken ct);

    /// <summary>The databases/catalogs reachable on this connection, for the query-tab database
    /// switcher. Empty for engines with no database layer (e.g. SQLite).</summary>
    Task<IReadOnlyList<string>> GetDatabasesAsync(ConnectionProfile profile, CancellationToken ct);
}
