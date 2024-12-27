using System.Data.Common;

namespace Sandbox.Infrastructure;

public class ConnectionResolver : IAsyncDisposable
{
    private DbConnection? OwnConnection { get; }
    private DbTransaction? _ownTransaction;
    private DbTransaction? ExternalTransaction { get; }

    /// <summary>
    /// in case of transaction we should use it's connection, or Exception would be thrown
    /// </summary>
    public static async Task<ConnectionResolver> GetResolver(IDbSessionFactory factory, DbTransaction? transaction)
    {
        ArgumentNullException.ThrowIfNull(factory);
        DbConnection? connection = null;
        if (transaction?.Connection is null)
        {
            connection = await factory.OpenAsync(CancellationToken.None);
        }
        return new ConnectionResolver(connection, transaction);
    }

    private ConnectionResolver(DbConnection? ownConnection, DbTransaction? externalTransaction)
    {
        if (ownConnection is null && externalTransaction?.Connection is null)
            throw new ArgumentException("both connections cannot be null");
        OwnConnection = ownConnection;
        ExternalTransaction = externalTransaction;
    }

    public DbConnection Connection => ExternalTransaction?.Connection ?? OwnConnection!;
    public DbTransaction? Transaction => ExternalTransaction ?? _ownTransaction;

    public async Task TryBeginTransaction()
    {
        if (ExternalTransaction is not null) return;
        _ownTransaction = await OwnConnection!.BeginTransactionAsync();
    }
    
    public async Task RollBack()
    {
        if (Transaction is not null)
        {
            await Transaction.RollbackAsync();
        }
    }

    public async Task TryCommit()
    {
        if (_ownTransaction is not null)
        {
            await _ownTransaction.CommitAsync();
            await _ownTransaction.DisposeAsync();
            _ownTransaction = null;
        }
    }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public ValueTask DisposeAsync()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
    {
        OwnConnection?.Dispose();
        _ownTransaction?.Dispose();
        return ValueTask.CompletedTask;
    }
}
