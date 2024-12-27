using System.Data.Common;

namespace Sandbox.Infrastructure;

public interface IDbSessionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken ct);
}