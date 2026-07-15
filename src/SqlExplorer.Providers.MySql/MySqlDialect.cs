using SqlExplorer.Sdk;

namespace SqlExplorer.Providers.MySql;

public sealed class MySqlDialect : ISqlDialect
{
    public IReadOnlySet<string> Keywords { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "GROUP", "BY", "ORDER", "HAVING", "LIMIT", "OFFSET",
        "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "FULL", "CROSS", "ON", "USING",
        "AND", "OR", "NOT", "IN", "IS", "NULL", "LIKE", "REGEXP", "BETWEEN",
        "AS", "DISTINCT", "UNION", "ALL", "INSERT", "INTO", "VALUES", "UPDATE",
        "SET", "DELETE", "REPLACE", "WITH", "CASE", "WHEN", "THEN", "ELSE", "END",
        "ASC", "DESC", "TRUE", "FALSE", "COUNT", "SUM", "AVG", "MIN", "MAX"
    };

    // MySQL/MariaDB quote identifiers with backticks; escape an embedded backtick by doubling it.
    public string QuoteIdentifier(string identifier) =>
        $"`{identifier.Replace("`", "``")}`";

    // MySQL's "database" IS the schema: two-part `db`.`table` (the tree exposes the db; schema is unused).
    // Naming the db explicitly also makes generated SQL resolve across databases on one connection.
    public string QualifyName(string? database, string? schema, string table) =>
        string.IsNullOrEmpty(database)
            ? QuoteIdentifier(table)
            : $"{QuoteIdentifier(database)}.{QuoteIdentifier(table)}";

    public string Paginate(string sql, int limit, int offset, string? orderBy = null)
    {
        var order = orderBy is null ? string.Empty : $"\nORDER BY {orderBy}";
        return $"{sql}{order}\nLIMIT {limit} OFFSET {offset}";
    }
}
