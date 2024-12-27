using System.Data.Common;
using MySqlConnector;

namespace Sandbox.Infrastructure;

public class DbSessionFactory : IDbSessionFactory
{
    private readonly string _connectionString;

    public DbSessionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public Task<DbConnection> OpenAsync(CancellationToken ct)
    {
        return OpenFor(_connectionString, ct);
    }

    private static async Task<DbConnection> OpenFor(string connectionString, CancellationToken ct)
    {
        var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
