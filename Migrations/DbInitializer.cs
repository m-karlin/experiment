using System.Data.Common;
using Dapper;
using Polly;
using Polly.Retry;
using Sandbox.Infrastructure;

namespace Sandbox.Migrations;

public class DbInitializer
{
    private readonly AsyncRetryPolicy _retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(retryCount: 20, sleepDurationProvider: attempt => TimeSpan.FromSeconds(5));

    private readonly IEnumerable<IMigrationGroup> _migrationGroups;
    private readonly IDbSessionFactory _sessionFactory;

    public DbInitializer(IEnumerable<IMigrationGroup> migrationGroups, IDbSessionFactory sessionFactory)
    {
        _migrationGroups = migrationGroups;
        _sessionFactory = sessionFactory;
    }

    public async Task Init(CancellationToken ct)
    {
        DbConnection connection = null!;
        await _retryPolicy.ExecuteAsync(async () =>
        {
            connection = await _sessionFactory.OpenAsync(ct);
        });
        const string createMigrationsTable = """
                                             CREATE TABLE IF NOT EXISTS __migrations (
                                                 identifier      BIGINT      NOT NULL PRIMARY KEY,
                                                 applied_on      DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                                                 description     TEXT        CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL
                                             )
                                             """;
        await connection.ExecuteAsync(createMigrationsTable);

        var migrations = _migrationGroups
            .SelectMany(migrationGroup => migrationGroup.Migrations())
            .OrderBy(migration => migration.Identifier);
        foreach (var migration in migrations)
        {
            await _retryPolicy.ExecuteAsync(() => ApplyMigration(connection, migration, ct));
        }
        await connection.CloseAsync();
    }

    private static async Task ApplyMigration(DbConnection connection, Migration migration, CancellationToken ct)
    {
        if (await Applied(connection, migration))
        {
            return;
        }

        await Apply(connection, migration, ct);
    }

    private static async Task<bool> Applied(DbConnection connection, Migration migration)
    {
        const string checkMigration = """
                                      SELECT 1 FROM __migrations WHERE Identifier = @migration_id
                                      """;

        var applied = await connection.ExecuteScalarAsync<int>(
            checkMigration,
            new { migration_id = migration.Identifier }
        );

        return applied == 1;
    }

    private static async Task Apply(DbConnection connection, Migration migration, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string registerMigration = """
                                         INSERT INTO __migrations (identifier, applied_on, description)
                                             VALUE (@identifier, UTC_TIMESTAMP(), @description)
                                         """;
        await connection.ExecuteAsync(
            registerMigration,
            new
            {
                identifier = migration.Identifier,
                description = migration.Description
            },
            transaction
        );

        await connection.ExecuteAsync(migration.SqlString, transaction: transaction);
        await transaction.CommitAsync(ct);
    }
}
