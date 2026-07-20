using System.Data;
using Npgsql;

namespace Aib.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    Task<IDbConnection> OpenAsync(CancellationToken ct = default);
}

public sealed class NpgsqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<IDbConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
