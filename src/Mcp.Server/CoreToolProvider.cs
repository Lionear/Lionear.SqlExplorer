using System.Text.Json;

namespace Lionear.SqlExplorer.Mcp.Server;

/// <summary>
/// The first-party, bundled MCP tool provider — the four core tools every AI client gets: list connections,
/// browse schema, run a query, explain a query. Deliberately thin: each handler only parses its arguments
/// and calls the host's <see cref="IMcpHost"/>, which owns every authorization decision (reachability,
/// read/write/DDL classification, row/timeout caps, audit). These tools never touch a driver or a
/// connection string directly, so they cannot exceed what the host permits.
/// </summary>
public sealed class CoreToolProvider : IMcpToolProvider
{
    public IReadOnlyList<McpToolDefinition> GetTools() =>
    [
        new McpToolDefinition(
            "list_connections",
            "List the database connections the AI is allowed to use (id, name, engine, access mode). Never returns secrets.",
            """{"type":"object","properties":{},"additionalProperties":false}""",
            (_, host, _) => Task.FromResult<object?>(host.ListConnections())),

        new McpToolDefinition(
            "get_schema",
            "List the schema entries (databases/schemas/tables/columns) under an optional path in a connection's tree.",
            """
            {
              "type": "object",
              "properties": {
                "connectionId": { "type": "string", "description": "Connection id from list_connections." },
                "path": { "type": "array", "items": { "type": "string" }, "description": "Ancestor node names from the tree root; omit for the top level." }
              },
              "required": ["connectionId"],
              "additionalProperties": false
            }
            """,
            async (args, host, ct) =>
            {
                var connectionId = RequireString(args, "connectionId");
                var path = ReadStringArray(args, "path");
                return await host.GetSchemaAsync(connectionId, path, ct);
            }),

        new McpToolDefinition(
            "run_query",
            "Run a single SQL statement on a connection and return the (row-capped) result. Writes need a ReadWrite connection; DDL and multi-statement payloads are always refused.",
            """
            {
              "type": "object",
              "properties": {
                "connectionId": { "type": "string", "description": "Connection id from list_connections." },
                "sql": { "type": "string", "description": "A single SQL statement." },
                "maxRows": { "type": "integer", "description": "Optional row cap (only lowers the server cap)." }
              },
              "required": ["connectionId", "sql"],
              "additionalProperties": false
            }
            """,
            async (args, host, ct) =>
            {
                var connectionId = RequireString(args, "connectionId");
                var sql = RequireString(args, "sql");
                var maxRows = ReadInt(args, "maxRows");
                return await host.RunQueryAsync(connectionId, sql, maxRows, ct);
            }),

        new McpToolDefinition(
            "explain_query",
            "Return the query plan (EXPLAIN) for a SQL statement without executing it.",
            """
            {
              "type": "object",
              "properties": {
                "connectionId": { "type": "string", "description": "Connection id from list_connections." },
                "sql": { "type": "string", "description": "The SQL statement to explain." }
              },
              "required": ["connectionId", "sql"],
              "additionalProperties": false
            }
            """,
            async (args, host, ct) =>
            {
                var connectionId = RequireString(args, "connectionId");
                var sql = RequireString(args, "sql");
                return await host.ExplainAsync(connectionId, sql, ct);
            })
    ];

    private static string RequireString(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object
        && args.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new McpAccessException($"Missing required string argument '{name}'.");

    private static int? ReadInt(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object
        && args.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var i)
            ? i
            : null;

    private static IReadOnlyList<string>? ReadStringArray(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString()!);
            }
        }

        return list;
    }
}
