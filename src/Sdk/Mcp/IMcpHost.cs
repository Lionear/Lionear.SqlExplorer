namespace Lionear.SqlExplorer.Sdk.Mcp;

/// <summary>
/// The host services an MCP plugin drives, and — crucially — the security boundary. The plugin owns only
/// the transport (a loopback HTTP listener + the MCP protocol); every authorization decision lives behind
/// this interface, implemented by the host against its live connection/provider instances. So the plugin
/// never sees a connection string, never decides what SQL is allowed, and never reaches a driver directly:
/// it calls these methods and the host enforces reachability (opt-in + not-excluded), the read/write/DDL
/// classification for the connection's access mode, the row/timeout caps, and the audit log.
/// </summary>
public interface IMcpHost
{
    /// <summary>The MCP-reachable connections (already filtered to opted-in + not-excluded), with no secrets.
    /// Backs <c>list_connections</c>.</summary>
    IReadOnlyList<McpConnectionInfo> ListConnections();

    /// <summary>The schema entries under <paramref name="path"/> (ancestor names from the tree root; null/empty
    /// = top level) of <paramref name="connectionId"/>. Throws <see cref="McpAccessException"/> when the
    /// connection is not MCP-reachable. Backs <c>get_schema</c>.</summary>
    Task<IReadOnlyList<McpSchemaEntry>> GetSchemaAsync(string connectionId, IReadOnlyList<string>? path, CancellationToken ct);

    /// <summary>Run <paramref name="sql"/> on <paramref name="connectionId"/>, capped to
    /// <paramref name="maxRows"/> (or the host default, whichever is lower). The host classifies the SQL and
    /// throws <see cref="McpAccessException"/> unless it is permitted for the connection's access mode
    /// (reads need ReadOnly+, DML needs ReadWrite, DDL/multi-statement always refused). Logs to history as an
    /// AI-sourced entry. Backs <c>run_query</c>.</summary>
    Task<McpQueryResult> RunQueryAsync(string connectionId, string sql, int? maxRows, CancellationToken ct);

    /// <summary>Run this engine's EXPLAIN for <paramref name="sql"/> (read-only; needs only reachability).
    /// Backs <c>explain_query</c>.</summary>
    Task<McpQueryResult> ExplainAsync(string connectionId, string sql, CancellationToken ct);

    /// <summary>Record one MCP transport call for the audit trail — including refused/unauthorized/excluded
    /// ones, and whether auth was required at the time — so an unauthenticated window is recognisable after
    /// the fact (plan §8 / CRIT-3).</summary>
    void LogAudit(string tool, string? connectionId, bool allowed, string? reason, bool requireAuthOn);
}
