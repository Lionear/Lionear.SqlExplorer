using SqlExplorer.Core.Providers;
using SqlExplorer.Providers.MsSql;
using SqlExplorer.Sdk.Provisioning;

namespace SqlExplorer.Core.Tests.Providers;

public class ProviderContainerRecipeTests
{
    [Fact] // The bundled SQL Server provider declares its own recipe (SE-166 dogfood), pinned to 2025-latest.
    public void MsSql_provider_declares_a_2025_recipe()
    {
        var recipe = new MsSqlProvider().ContainerRecipe;

        Assert.NotNull(recipe);
        Assert.Equal("mcr.microsoft.com/mssql/server", recipe!.Image);
        Assert.Equal("2025-latest", recipe.DefaultTag);
        Assert.Equal(1433, recipe.ContainerPort);
        Assert.True(recipe.DatabaseAfterStart); // a named database is created after the server starts

        var env = recipe.Environment(new ContainerEnvInput(
            new Dictionary<string, string?>(), Database: null, User: "sa", Password: "pw"));

        Assert.Contains(env, kv => kv is { Key: "ACCEPT_EULA", Value: "Y" });
        Assert.Contains(env, kv => kv is { Key: "MSSQL_SA_PASSWORD", Value: "pw" });
    }

    [Fact] // The host catalog surfaces the real provider's recipe through the read-seam.
    public void Host_catalog_exposes_the_MsSql_recipe()
    {
        var registry = new DbProviderRegistry([new ProviderRegistration("sqlserver", new MsSqlProvider())]);

        var entry = Assert.Single(new HostProviderCatalog(registry).ContainerRecipes());
        Assert.Equal("sqlserver", entry.ProviderId);
        Assert.Equal("mcr.microsoft.com/mssql/server", entry.Recipe.Image);
    }
}
