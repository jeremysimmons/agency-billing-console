using System.Reflection;
using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Logging;

namespace Aib.Infrastructure.Persistence;

/// <summary>Applies embedded SQL migration scripts using DbUp (dbup-postgresql).</summary>
public sealed class DatabaseMigrator(string connectionString, ILogger<DatabaseMigrator> logger)
{
    public void Run()
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        DatabaseUpgradeResult result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            logger.LogError(result.Error, "Database migration failed on script {Script}", result.ErrorScript?.Name);
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }

        logger.LogInformation("Database migrations applied ({Count} script(s) executed).",
            result.Scripts.Count());
    }
}
