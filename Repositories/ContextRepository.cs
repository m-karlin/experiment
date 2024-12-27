using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.IO;
using Sandbox.Domain;
using Sandbox.Infrastructure;

namespace Sandbox.Repositories;

public class SagaContextRepository
{
    public SagaContextRepository(IDbSessionFactory sessionFactory, RecyclableMemoryStreamManager streamManager)
    {
        SessionFactory = sessionFactory;
        StreamManager = streamManager;
    }

    public IDbSessionFactory SessionFactory { get; }
    private RecyclableMemoryStreamManager StreamManager { get; }

    public async Task<SagaContext?> Get(string id, CancellationToken ct = default)
    {
        const string query = @"
            SELECT binary data FROM Context
            WHERE id = @id
        ";
        await using var resolver = await ConnectionResolver.GetResolver(SessionFactory, null);
        var command = new CommandDefinition(
            query,
            new { id },
            transaction: resolver.Transaction,
            cancellationToken: ct
        );
        return await resolver.Connection.ExecuteScalarFromJson<SagaContext>(command, ct: ct);
    }

    public async Task Set(SagaContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await using var resolver = await ConnectionResolver.GetResolver(SessionFactory, null);
        await resolver.TryBeginTransaction();

        await using var buffer = StreamManager.GetStream();
        await JsonSerializer.SerializeAsync(buffer, context, CommonJsonConfig.Options, ct);
        buffer.Seek(0, SeekOrigin.Begin);

        var parameters = new DynamicParameters();
        parameters.Add("@id", context.Id);
        parameters.Add("@data", buffer, DbType.Binary);

        var sql = @"
                INSERT INTO Context (id, data) 
                    VALUE (@id, CONVERT(@data using utf8mb4))
            ";

        var command = new CommandDefinition(
            sql,
            parameters,
            transaction: resolver.Transaction,
            cancellationToken: ct
        );
        await resolver.Connection.ExecuteAsync(command);

        buffer.Seek(0, SeekOrigin.Begin);
        await resolver.TryCommit();
    }
}
