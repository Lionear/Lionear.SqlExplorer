namespace Lionear.SqlExplorer.Sdk.Mcp;

/// <summary>A connection as an MCP client may see it — identity + engine + coarse access flags only,
/// never a connection string or secret. The host only ever returns connections that are MCP-reachable
/// (opted in and not hard-excluded), so a client never learns an excluded connection exists.</summary>
public sealed record McpConnectionInfo(string Id, string Name, string ProviderId, bool ReadOnly, string AiAccess);

/// <summary>One schema-tree entry for <c>get_schema</c>: a table/view/column/folder with its child-count
/// hint and (for a column) its type. Deliberately flat — the client walks it with the optional path arg.</summary>
public sealed record McpSchemaEntry(string Name, string Kind, string? Type);

/// <summary>The result of a query/explain run over MCP: column names + row-major values (already row-capped),
/// with the true counts and whether the cap truncated the set.</summary>
public sealed record McpQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int RowCount,
    double DurationMs,
    bool Truncated);

/// <summary>Thrown by <see cref="IMcpHost"/> when a call is refused by the host-side guards (connection not
/// MCP-reachable, statement not permitted for the access mode, DDL/multi-statement, missing connection).
/// The plugin maps it to an MCP error — the refusal happens in the host, never at the driver.</summary>
public sealed class McpAccessException(string message) : Exception(message);
