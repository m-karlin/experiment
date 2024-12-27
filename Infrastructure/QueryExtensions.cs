using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dapper;

namespace Sandbox.Infrastructure;

public static class QueryExtensions
{
    public static T ParseAs<T>(this string valueString) where T : struct
    {
        return Enum.Parse<T>(valueString, ignoreCase: true);
    }

    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        this DbConnection connection, CommandDefinition command, [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await using var reader = await connection.ExecuteReaderAsync(command);
        var parser = reader.GetRowParser<T>();
        while (await reader.ReadAsync(ct))
        {
            yield return parser(reader);
        }
    }

    public static async Task<QueryResultStream> ExecuteRowStreams(
        this DbConnection connection, CommandDefinition command, CancellationToken ct = default
    )
    {
        ct.ThrowIfCancellationRequested();
        return new QueryResultStream(await connection.ExecuteReaderAsync(command, CommandBehavior.SequentialAccess));
    }

    public static Stream GetStream(this DbDataReader reader, string? name = null)
    {
#pragma warning disable CA1062 // Validate arguments of public methods
        var ordinal = string.IsNullOrEmpty(name) ? 0 : reader.GetOrdinal(name);
#pragma warning restore CA1062 // Validate arguments of public methods
        return new ReaderStreamer(reader, ordinal);
    }

    public static ValueTask<T?> GetFromJson<T>(
        this DbDataReader reader, string? name = null, CancellationToken ct = default
    )
    {
#pragma warning disable CA2000
        return JsonSerializer.DeserializeAsync<T>(reader.GetStream(name), CommonJsonConfig.Options, ct);
#pragma warning restore CA2000
    }

    public static async Task<T?> ExecuteScalarFromJson<T>(
        this DbConnection connection, CommandDefinition command, CancellationToken ct = default
    )
    {
        await using var reader = await connection.ExecuteReaderAsync(
            command, CommandBehavior.SequentialAccess | CommandBehavior.SingleResult | CommandBehavior.SingleRow
        );
        if (!await reader.ReadAsync(ct)) return default;
        return await reader.GetFromJson<T>(ct: ct);
    }

    public static async IAsyncEnumerable<T> ToAsyncEnumerableFromJson<T>(
        this DbConnection connection, CommandDefinition command, [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await using var reader = await connection.ExecuteReaderAsync(command, CommandBehavior.SequentialAccess);
        while (await reader.ReadAsync(ct))
        {
            var result = await reader.GetFromJson<T>(ct: ct);
            if (result is null) continue;
            yield return result;
        }
    }

#pragma warning disable CA1034 // Nested types should not be visible
    public class QueryResultStream : Stream
#pragma warning restore CA1034 // Nested types should not be visible
    {
        private DbDataReader Reader { get; }
        private int Ordinal { get; }
        private Stream ReaderWrapper { get; set; }

        public QueryResultStream(DbDataReader reader, int ordinal = 0)
        {
            Reader = reader;
            Ordinal = ordinal;
            ReaderWrapper = new ReaderStreamer(reader, ordinal);
        }

        public async Task<bool> MoveNext()
        {
            var moved = await Reader.ReadAsync();
            if (!moved) return false;
            ReaderWrapper = new ReaderStreamer(Reader, Ordinal);
            return true;
        }

        public override void Flush()
        {
            ReaderWrapper.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReaderWrapper.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return ReaderWrapper.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            ReaderWrapper.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ReaderWrapper.Write(buffer, offset, count);
        }

        public override bool CanRead => ReaderWrapper.CanRead;

        public override bool CanSeek => ReaderWrapper.CanSeek;

        public override bool CanWrite => ReaderWrapper.CanWrite;

        public override long Length => ReaderWrapper.Length;

        public override long Position
        {
            get => ReaderWrapper.Position;
            set => ReaderWrapper.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Reader.Dispose();
            base.Dispose(disposing);
        }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
        public override async ValueTask DisposeAsync()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        {
            await Reader.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    private sealed class ReaderStreamer : Stream
    {
        private DbDataReader Reader { get; }
        private int Ordinal { get; }
        private long _position;

        public ReaderStreamer(DbDataReader reader, int ordinal = 0)
        {
            Reader = reader;
            Ordinal = ordinal;
        }

        public override void Flush() {}

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = Reader.GetBytes(Ordinal, _position, buffer, offset, count);
            _position += read;
            return (int)read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }
    }
}
