# Writing Plugins for Lionear SQL Explorer

This document explains the plugin system: what a plugin is, how it is loaded,
and how to build one.

## Overview

Lionear SQL Explorer ships **no database drivers in the host binaries**. Every
database engine (PostgreSQL, MySQL, SQL Server, SQLite, ...) is a separate
plugin, discovered at startup and loaded in its own isolated
`AssemblyLoadContext`. This keeps the host provider-agnostic and lets each
plugin carry its own driver version.

Today there is exactly **one plugin type**: `provider` — a database engine
integration. The manifest format (`type` field, see below) is deliberately
open-ended so other plugin kinds could be added later, but `provider` is the
only one that exists and is loaded right now.

## Plugin type: `provider`

A provider plugin teaches the host how to talk to one database engine. It
implements a single interface, `IDbProvider`, from the public SDK project
`src/Provider.Sdk` (namespace `Lionear.SqlExplorer.Sdk`). `Provider.Sdk` is
MIT-licensed specifically so third parties can build and ship their own
providers freely — it is the *only* assembly a provider plugin references
from this repository; no reference to `Core`, `App`, or any driver-specific
host code is needed or allowed.

### The contract: `IDbProvider`

```csharp
public interface IDbProvider
{
    string DisplayName { get; }
    ProviderIcon? Icon { get; }
    ISqlDialect Dialect { get; }
    IReadOnlyList<ConnectionField> ConnectionFields { get; }

    string BuildConnectionString(IReadOnlyDictionary<string, string?> values);

    Task<bool> TestConnectionAsync(ConnectionProfile profile, CancellationToken ct);

    Task<IReadOnlyList<DbTreeNode>> GetChildNodesAsync(
        ConnectionProfile profile,
        IReadOnlyList<DbNodeRef> ancestors,
        CancellationToken ct);

    Task<QueryResult> ExecuteQueryAsync(ConnectionProfile profile, string sql, CancellationToken ct);

    Task<int> ExecuteBatchAsync(
        ConnectionProfile profile,
        IReadOnlyList<SqlStatement> statements,
        CancellationToken ct);
}
```

| Member | Purpose |
|---|---|
| `DisplayName` | Human-readable name shown in the UI (e.g. `"PostgreSQL"`). |
| `Icon` | Optional glyph/image for connection nodes. Use `ProviderIconLoader.Load(typeof(YourProvider), "🔧")` — it embeds an `icon.png` next to the project if present, otherwise falls back to the given emoji glyph. |
| `Dialect` | The provider's `ISqlDialect` implementation (see below). |
| `ConnectionFields` | Declares the fields of the connection dialog. The host renders a generic form from this — no provider-specific UI code is ever needed. |
| `BuildConnectionString` | Composes a driver connection string from the submitted field values (keyed by `ConnectionField.Key`), including any secret just retrieved from the OS keychain. |
| `TestConnectionAsync` | Opens and validates a connection; used by the "Test connection" button. |
| `GetChildNodesAsync` | Lazily lists the children of one schema-tree node (DBeaver-style on-demand loading, so large servers are never introspected all at once). `ancestors` is the path from the connection root to the node being expanded — empty for the top-level nodes. Each provider decides its own hierarchy shape (server → database → schema → tables/views → columns, or something flatter, as SQLite does). |
| `ExecuteQueryAsync` | Runs a free-form SQL string and returns a `QueryResult`. |
| `ExecuteBatchAsync` | Runs a set of parameterised `SqlStatement`s inside a single transaction, rolling back on any failure. This is the commit step of the editable-grid save flow: the host generates dialect-quoted INSERT/UPDATE/DELETE statements, the provider only owns parameter binding and transaction handling. |

### The dialect: `ISqlDialect`

```csharp
public interface ISqlDialect
{
    IReadOnlySet<string> Keywords { get; }
    string QuoteIdentifier(string identifier);
    string QualifyName(string? database, string? schema, string table);
    string Paginate(string sql, int limit, int offset, string? orderBy = null);
}
```

| Member | Purpose |
|---|---|
| `Keywords` | SQL keyword set used for syntax highlighting. |
| `QuoteIdentifier` | Quotes/escapes a single identifier (table, column, ...) in the engine's own syntax. |
| `QualifyName` | Builds a fully qualified, quoted object name from optional database/schema and a table name. |
| `Paginate` | Wraps a query with the engine's pagination syntax (`LIMIT/OFFSET`, `OFFSET/FETCH`, ...), optionally applying an `ORDER BY`. Used by the Browse tab's paging and sorting. |

### Supporting DTOs (all in `Provider.Sdk`)

- **`ConnectionField(Key, Label, Type, Required, Default, Placeholder)`** — one
  field of the connection dialog. `Type` is `Text | Password | Number | File |
  Bool`. Fields of type `Password` are automatically routed to the OS
  keychain (`IsSecret == true`) and never written to the connection config
  file.
- **`ConnectionProfile(Name, ConnectionString, Database)`** — what a provider
  method receives at execute time. `Database` is the optional catalog/database
  context selected in the UI.
- **`DbNodeKind`** — enum of schema-tree node kinds: `Database, SchemaFolder,
  Schema, TableFolder, ViewFolder, IndexFolder, SequenceFolder, Table, View,
  Column, Index, Sequence, Object, Group`.
- **`DbNodeRef(Kind, Name)`** / **`DbTreeNode { Kind, Name, Detail,
  HasChildren }`** — a path segment / a node returned by `GetChildNodesAsync`.
- **`QueryResult { Columns, Rows, RecordsAffected, Elapsed }`** with
  **`ResultColumn(Name, ClrType)`** carrying edit metadata (`BaseSchema,
  BaseTable, BaseColumn, IsKey, IsReadOnly, AllowDbNull`) — this metadata is
  what lets the host decide whether a result grid is safely editable (traces
  back to a single table with a primary key).
- **`SqlStatement(Text, Parameters)`** / **`SqlParam(Name, Value)`** —
  parameterised statement with named placeholders (`@p0, @p1, ...`).

### Host API versioning

`ProviderHostApi.Version` (currently `11`) is the contract version. Every
plugin declares the version it was built against in its manifest
(`hostApiVersion`); the loader rejects a plugin whose version does not match,
rather than risk loading against a contract it doesn't fully implement.
Check `src/Provider.Sdk/ProviderHostApi.cs` for the current value and its
changelog comments before starting a new provider.

## Building a provider plugin, step by step

### 1. Create the project

Add a new project under `src/`, e.g. `src/Providers.MyEngine/`, referencing
**only** `Provider.Sdk`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Lionear.SqlExplorer.Providers.MyEngine</RootNamespace>
    <!-- Required: emit the full private dependency closure (driver + its own
         dependencies) so the plugin loads correctly in its own ALC. -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <!-- Private=false keeps Provider.Sdk.dll OUT of the plugin's own output
         folder, so the host's copy is used across the ALC boundary and
         IDbProvider keeps a single type identity. -->
    <ProjectReference Include="..\Provider.Sdk\Lionear.SqlExplorer.Provider.Sdk.csproj" Private="false" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="MyEngine.Driver" Version="x.y.z" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="plugin.json" CopyToOutputDirectory="PreserveNewest" />
    <!-- Optional: drop a square PNG here as icon.png for branding. -->
    <EmbeddedResource Include="icon.png" LogicalName="icon.png" Condition="Exists('icon.png')" />
  </ItemGroup>

</Project>
```

### 2. Implement `IDbProvider` and `ISqlDialect`

Use `src/Providers.Sqlite/SqliteProvider.cs` and `SqliteDialect.cs` as the
simplest reference implementation (no server/database/schema layers — SQLite
exposes Tables/Views/Sequences directly under the connection root). For an
engine with server → database → schema layering, see
`src/Providers.Postgres` or `src/Providers.MsSql`.

Minimal skeleton:

```csharp
using Lionear.SqlExplorer.Sdk;

namespace Lionear.SqlExplorer.Providers.MyEngine;

public sealed class MyEngineProvider : IDbProvider
{
    public string DisplayName => "MyEngine";

    public ProviderIcon? Icon { get; } = ProviderIconLoader.Load(typeof(MyEngineProvider), "🔧");

    public ISqlDialect Dialect { get; } = new MyEngineDialect();

    public IReadOnlyList<ConnectionField> ConnectionFields { get; } =
    [
        new("host", "Host", ConnectionFieldType.Text, Required: true),
        new("port", "Port", ConnectionFieldType.Number, Default: "5432"),
        new("database", "Database", ConnectionFieldType.Text, Required: true),
        new("username", "Username", ConnectionFieldType.Text, Required: true),
        new("password", "Password", ConnectionFieldType.Password)
    ];

    public string BuildConnectionString(IReadOnlyDictionary<string, string?> values) =>
        /* compose the driver's connection string from `values` */;

    public Task<bool> TestConnectionAsync(ConnectionProfile profile, CancellationToken ct) => /* ... */;

    public Task<IReadOnlyList<DbTreeNode>> GetChildNodesAsync(
        ConnectionProfile profile, IReadOnlyList<DbNodeRef> ancestors, CancellationToken ct) => /* ... */;

    public Task<QueryResult> ExecuteQueryAsync(ConnectionProfile profile, string sql, CancellationToken ct) => /* ... */;

    public Task<int> ExecuteBatchAsync(
        ConnectionProfile profile, IReadOnlyList<SqlStatement> statements, CancellationToken ct) => /* ... */;
}
```

### 3. Write the manifest (`plugin.json`)

Every plugin folder needs a `plugin.json` describing it:

```json
{
  "schemaVersion": 1,
  "id": "myengine",
  "type": "provider",
  "name": "MyEngine",
  "version": "1.0.0",
  "hostApiVersion": 11,
  "entryAssembly": "Lionear.SqlExplorer.Providers.MyEngine.dll"
}
```

| Field | Meaning |
|---|---|
| `schemaVersion` | Manifest format version (currently `1`). |
| `id` | The engine's permanent identity. There is no host-side enum of engines — `id` is what makes the set of engines open; pick something short, lowercase, and stable, since saved connections reference it. |
| `type` | Plugin kind discriminator. Must be `"provider"` — the only value the loader currently accepts. |
| `name` | Display name (informational; `IDbProvider.DisplayName` is what the UI actually shows). |
| `version` | Your plugin's own version string. |
| `hostApiVersion` | Must equal `ProviderHostApi.Version` at build time. A mismatch causes the loader to skip the plugin rather than risk a broken contract. |
| `entryAssembly` | Path (relative to the plugin's own folder) to the compiled plugin DLL. |

### 4. Ship it

A plugin is a folder next to the host executable:

```
plugins/
  myengine/
    plugin.json
    Lionear.SqlExplorer.Providers.MyEngine.dll
    Lionear.SqlExplorer.Providers.MyEngine.deps.json
    MyEngine.Driver.dll
    ... (rest of the build output)
```

For the first-party providers this copy is automated by an MSBuild target,
`StageProviderPlugins`, in `src/Desktop/Lionear.SqlExplorer.Desktop.csproj`,
which runs after build and copies each `Providers.*` project's full output
into `<TargetDir>/plugins/<id>/`. A genuinely third-party/out-of-tree plugin
ships the same way manually — just place the built output (including the
`.deps.json`) plus `plugin.json` in `plugins/<id>/` next to the host
executable.

## How discovery and loading work

At startup (`src/App/DependencyInjection/AppServices.cs`), the host:

1. Resolves `plugins/` next to the executable (`AppContext.BaseDirectory`).
2. Runs `ProviderPluginLoader.Load(pluginsDir)`
   (`src/Core/Plugins/ProviderPluginLoader.cs`), which for each subfolder:
   - Skips folders without a `plugin.json`.
   - Parses the manifest; skips it if `type != "provider"` or
     `hostApiVersion` doesn't match `ProviderHostApi.Version`.
   - Loads `entryAssembly` into a fresh, isolated `ProviderLoadContext`
     (`src/Core/Plugins/ProviderLoadContext.cs`, an `AssemblyLoadContext`
     subclass using `AssemblyDependencyResolver` against the plugin's own
     `.deps.json`) — so each plugin can carry its own driver version
     independent of every other plugin.
   - Reflects for a non-abstract class implementing `IDbProvider` and
     activates it.
   - Never throws back to the caller: failures are captured per plugin as an
     `Error` on the `ProviderLoadResult`, and logged, so one broken plugin
     doesn't take down the app.
3. Registers every successfully loaded provider into `DbProviderRegistry`
   (keyed by manifest `id`) as the DI singleton `IDbProviderRegistry`.

One important detail if you're debugging an ALC loading issue:
`ProviderLoadContext` deliberately returns `null` (falls back to the default
load context) when asked to resolve `Provider.Sdk` itself, so the host's copy
of `Provider.Sdk.dll` is reused across the ALC boundary and `IDbProvider`
keeps a single type identity. This is exactly why every provider `.csproj`
sets `Private="false"` on the `Provider.Sdk` project reference — it must
*not* be copied into the plugin's own output folder.

## Reference implementations

| Provider | Notable for |
|---|---|
| `src/Providers.Sqlite` | Simplest complete example — no server/database/schema layers, good starting template. |
| `src/Providers.Postgres` | Full server → database → schema → table hierarchy; also the proof-of-concept that a provider builds independently of the host. |
| `src/Providers.MySql` | MySQL/MariaDB dialect quirks. |
| `src/Providers.MsSql` | SQL Server dialect and schema layering. |

## Future plugin types

The manifest's `type` field exists specifically so other plugin kinds can be
added without a breaking format change. None exist yet. A per-dialect SQL
formatter (currently a single host-owned `ISqlFormatter` baseline, see
`src/Core/Formatting/`) is noted as a roadmap candidate and a likely first
addition.
