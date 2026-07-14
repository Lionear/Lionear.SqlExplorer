using Lionear.SqlExplorer.Core.Providers;
using Lionear.SqlExplorer.Sdk;

namespace Lionear.SqlExplorer.Core.Connections;

/// <summary>
/// Ties saved connections, the keychain and the providers together. Uses each provider's
/// declared <see cref="ConnectionField"/>s to decide which values are secret (→ keychain) and
/// which are plain (→ config file), so the split is provider-driven, not hard-coded here.
/// </summary>
public sealed class ConnectionService
{
    private readonly IConnectionStore _store;
    private readonly ISecretStore _secrets;
    private readonly IDbProviderRegistry _providers;

    public ConnectionService(IConnectionStore store, ISecretStore secrets, IDbProviderRegistry providers)
    {
        _store = store;
        _secrets = secrets;
        _providers = providers;
    }

    public IReadOnlyList<SavedConnection> List() => _store.GetAll();

    /// <summary>Persist a connection: secrets to the keychain, the rest to the config file.</summary>
    public SavedConnection Save(
        string id, string name, string providerId, IReadOnlyDictionary<string, string?> values,
        string? color = null, bool readOnly = false, string? folder = null,
        AiAccessMode aiAccess = AiAccessMode.None, bool excludeFromMcp = false)
    {
        var fields = _providers.Get(providerId).ConnectionFields;
        var secretKeys = fields.Where(f => f.IsSecret).Select(f => f.Key).ToHashSet();

        foreach (var field in fields.Where(f => f.IsSecret))
        {
            var secretKey = SecretKey(id, field.Key);
            var value = values.TryGetValue(field.Key, out var v) ? v : null;
            if (string.IsNullOrEmpty(value))
            {
                _secrets.Delete(secretKey);
            }
            else
            {
                _secrets.Set(secretKey, value);
            }
        }

        var nonSecret = values
            .Where(kv => !secretKeys.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var connection = new SavedConnection
        {
            Id = id, Name = name, ProviderId = providerId, Color = color, ReadOnly = readOnly,
            Folder = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim(),
            AiAccess = aiAccess, ExcludeFromMcp = excludeFromMcp, Values = nonSecret
        };
        _store.Save(connection);
        return connection;
    }

    /// <summary>
    /// Copy a saved connection (including its keychain secret) under a fresh id and the given name.
    /// The caller supplies the new name so the label stays localizable in the UI layer.
    /// </summary>
    public SavedConnection Duplicate(string id, string newName)
    {
        var original = _store.GetAll().FirstOrDefault(c => c.Id == id)
            ?? throw new InvalidOperationException($"Connection '{id}' not found.");

        // WithSecrets pulls the password back from the keychain so the copy is fully usable.
        var values = WithSecrets(original);
        return Save(Guid.NewGuid().ToString("N"), newName, original.ProviderId, values, original.Color, original.ReadOnly, original.Folder);
    }

    /// <summary>
    /// Move a connection to another folder (or ungroup it) without touching its secrets. Only the
    /// non-secret <see cref="SavedConnection.Folder"/> path changes, so the keychain is left alone —
    /// used by the Connection Manager's drag &amp; drop and folder rename/delete.
    /// </summary>
    public SavedConnection SetFolder(SavedConnection connection, string? folder)
    {
        var updated = connection with { Folder = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim() };
        _store.Save(updated);
        return updated;
    }

    public void Delete(string id)
    {
        var connection = _store.GetAll().FirstOrDefault(c => c.Id == id);
        if (connection is not null)
        {
            foreach (var field in _providers.Get(connection.ProviderId).ConnectionFields.Where(f => f.IsSecret))
            {
                _secrets.Delete(SecretKey(id, field.Key));
            }
        }

        _store.Delete(id);
    }

    /// <summary>
    /// Merge stored non-secret values with keychain secrets into a runnable profile. <paramref name="database"/>
    /// overrides the target catalog when the caller knows it from the schema tree (null = connection default).
    /// </summary>
    public ConnectionProfile Resolve(SavedConnection connection, string? database = null) =>
        BuildProfile(connection.Name, connection.ProviderId, WithSecrets(connection), database);

    /// <summary>All field values (non-secret + secrets from the keychain) to prefill the edit dialog.</summary>
    public IReadOnlyDictionary<string, string?> GetEditableValues(SavedConnection connection) =>
        WithSecrets(connection);

    /// <summary>Build a profile from raw dialog values (used by Test, before anything is persisted).</summary>
    public ConnectionProfile BuildProfile(
        string name, string providerId, IReadOnlyDictionary<string, string?> values, string? database = null) =>
        new()
        {
            Name = name,
            ConnectionString = _providers.Get(providerId).BuildConnectionString(values),
            Database = database
        };

    private Dictionary<string, string?> WithSecrets(SavedConnection connection)
    {
        var values = new Dictionary<string, string?>(connection.Values);
        foreach (var field in _providers.Get(connection.ProviderId).ConnectionFields.Where(f => f.IsSecret))
        {
            values[field.Key] = _secrets.Get(SecretKey(connection.Id, field.Key));
        }

        return values;
    }

    private static string SecretKey(string id, string field) => $"conn:{id}:{field}";
}
