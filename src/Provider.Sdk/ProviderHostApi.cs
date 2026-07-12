namespace Lionear.SqlExplorer.Sdk;

/// <summary>
/// Versioning contract between the host and provider plugins. A plugin manifest
/// declares the host API version it was built against; the loader refuses a plugin
/// whose version this host cannot satisfy. Bump <see cref="Version"/> on a breaking
/// change to <see cref="IDbProvider"/> or the shared DTOs.
/// </summary>
public static class ProviderHostApi
{
    // v2 (2026-07-11): added ConnectionFields + BuildConnectionString to IDbProvider.
    // v3 (2026-07-12): replaced eager IntrospectSchemaAsync with lazy GetChildNodesAsync (DBeaver tree).
    // v4 (2026-07-12): added IDbProvider.Icon (ProviderIcon: glyph and/or image).
    // v5 (2026-07-12): added ResultColumn edit metadata (Base*/IsKey/…) + IDbProvider.ExecuteBatchAsync
    //                  (editable resultset save-flow, Notes §8).
    // v6 (2026-07-12): added IDbProvider.DisplayName (human-friendly provider label).
    // v7 (2026-07-12): ISqlDialect.Paginate gained an optional orderBy (server-side browse sort).
    // v8 (2026-07-12): added DbNodeKind SchemaFolder/IndexFolder/SequenceFolder/Index/Sequence/Group
    //                  (richer schema tree: schemas grouping, indexes, sequences, cosmetic folders).
    // v9 (2026-07-12): added DbNodeKind Object (generic provider-defined leaf: users/roles/logins/jobs).
    // v10 (2026-07-12): removed the DatabaseKind enum. Provider identity is now the manifest 'id'
    //                   string (loader-attached); dropped IDbProvider.Kind, ISqlDialect.Kind and
    //                   ConnectionProfile.Kind. Open engine set — no central enum to extend.
    // v11 (2026-07-12): added ConnectionProfile.Database (execute-time catalog context; fixes BUG-1
    //                   where MSSQL browse/generate ran against the default catalog, not the tree's db)
    //                   and ISqlDialect.QualifyName (dialect-driven qualified names for generated SQL;
    //                   SQL Server three-part [db].[schema].[table] so a query tab hits the right db).
    public const int Version = 11;

    /// <summary>True when this host can load a plugin built for <paramref name="pluginVersion"/>.</summary>
    public static bool IsCompatible(int pluginVersion) => pluginVersion == Version;
}
