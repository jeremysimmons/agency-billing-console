using System.Reflection;
using System.Text;
using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Logging;

namespace Aib.Migrations;

/// <summary>Applies embedded SQL migration scripts using DbUp (dbup-postgresql).</summary>
public sealed class DatabaseMigrator(string connectionString, ILogger<DatabaseMigrator> logger)
{
    const string LegacyPrefix = "Aib.Infrastructure.Migrations.";

    public void Run()
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var assembly = Assembly.GetExecutingAssembly();
        var scripts = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Missing embedded resource {name}.");
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var contents = reader.ReadToEnd();
                var scriptName = LegacyPrefix + name.Split('.')[^2] + ".sql";
                return new SqlScript(scriptName, contents);
            })
            .ToArray();

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScripts(scripts)
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
