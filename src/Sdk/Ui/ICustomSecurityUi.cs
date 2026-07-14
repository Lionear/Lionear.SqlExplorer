using Avalonia.Controls;
using Lionear.SqlExplorer.Sdk.Connections;
using Lionear.SqlExplorer.Sdk.Schema;

namespace Lionear.SqlExplorer.Sdk.Ui;

/// <summary>
/// Optional Route-B capability an <c>IDbProvider</c> may also implement to replace the host's generic
/// "New User…" form with a provider-owned Avalonia view — the seam for a genuinely divergent flow (e.g.
/// SQL Server's full server-Login → database-User → server-role-mapping wizard) without a host-API bump.
/// A fifth Route-B capability alongside <see cref="ICustomConnectionUi"/>, <see cref="ICustomNodeInfoUi"/>,
/// <see cref="ICustomCellActionUi"/> and <c>ICustomToolUi</c>.
/// </summary>
/// <remarks>
/// Not implemented by any provider in v1 — the seam simply exists so the later, richer flow slots in
/// without breaking the contract. Host detection is the usual optional-interface check: a provider that
/// only declares <c>UserFields</c> gets the generic form; one that also implements this gets its own view.
/// </remarks>
public interface ICustomSecurityUi
{
    /// <summary>Build the provider-owned "New User…" view for the given users-folder node.</summary>
    Control CreateUserView(UserUiContext context);
}

/// <summary>Everything a custom user view needs: the resolved profile, the users-folder node it was opened
/// on (its ancestry gives the target database for SQL Server), and the provider itself.</summary>
public sealed record UserUiContext(ConnectionProfile Profile, IReadOnlyList<DbNodeRef> Ancestors, IDbProvider Provider);
