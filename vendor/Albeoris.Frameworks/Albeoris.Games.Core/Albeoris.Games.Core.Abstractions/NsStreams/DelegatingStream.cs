namespace Albeoris.Games.Core.Abstractions.NsStreams;

public abstract class DelegatingStream : Stream
{
    public Stream BaseStream { get; }

    public DelegatingStream(Stream baseStream)
    {
        ArgumentNullException.ThrowIfNull(baseStream);

        BaseStream = baseStream;
    }

    public override Boolean CanSeek => BaseStream.CanSeek;
    public override Int64 Seek(Int64 offset, SeekOrigin origin) => BaseStream.Seek(offset, origin);

    public override Int64 Length => BaseStream.Length;
    public override void SetLength(Int64 value) => BaseStream.SetLength(value);

    public override Int64 Position
    {
        get => BaseStream.Position;
        set => BaseStream.Position = value;
    }

    public override Boolean CanTimeout => BaseStream.CanSeek;

    public override Int32 ReadTimeout
    {
        get => BaseStream.ReadTimeout;
        set => BaseStream.ReadTimeout = value;
    }

    public override Int32 WriteTimeout
    {
        get => BaseStream.WriteTimeout;
        set => BaseStream.WriteTimeout = value;
    }

    public override Boolean CanRead => BaseStream.CanRead;
    public override Int32 ReadByte() => BaseStream.ReadByte();
    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count) => BaseStream.Read(buffer, offset, count);
    public override Int32 Read(Span<Byte> buffer) => BaseStream.Read(buffer);
    public override IAsyncResult BeginRead(Byte[] buffer, Int32 offset, Int32 count, AsyncCallback? callback, Object? state) => BaseStream.BeginRead(buffer, offset, count, callback, state);
    public override Int32 EndRead(IAsyncResult asyncResult) => BaseStream.EndRead(asyncResult);
    public override Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken) => BaseStream.ReadAsync(buffer, offset, count, cancellationToken);
    public override ValueTask<Int32> ReadAsync(Memory<Byte> buffer, CancellationToken cancellationToken = default) => BaseStream.ReadAsync(buffer, cancellationToken);

    public override Boolean CanWrite => BaseStream.CanWrite;
    public override void WriteByte(Byte value) => BaseStream.WriteByte(value);
    public override void Write(Byte[] buffer, Int32 offset, Int32 count) => BaseStream.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<Byte> buffer) => BaseStream.Write(buffer);
    public override IAsyncResult BeginWrite(Byte[] buffer, Int32 offset, Int32 count, AsyncCallback? callback, Object? state) => BaseStream.BeginWrite(buffer, offset, count, callback, state);
    public override void EndWrite(IAsyncResult asyncResult) => BaseStream.EndWrite(asyncResult);
    public override Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken) => BaseStream.WriteAsync(buffer, offset, count, cancellationToken);
    public override ValueTask WriteAsync(ReadOnlyMemory<Byte> buffer, CancellationToken cancellationToken = default) => BaseStream.WriteAsync(buffer, cancellationToken);

    public override void CopyTo(Stream destination, Int32 bufferSize) => BaseStream.CopyTo(destination, bufferSize);
    public override Task CopyToAsync(Stream destination, Int32 bufferSize, CancellationToken cancellationToken) => BaseStream.CopyToAsync(destination, bufferSize, cancellationToken);

    public override void Flush() => BaseStream.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => BaseStream.FlushAsync(cancellationToken);

    public override void Close() => BaseStream.Close();
    public override ValueTask DisposeAsync() => BaseStream.DisposeAsync();

    protected override void Dispose(Boolean disposing)
    {
        if (disposing)
            BaseStream.Dispose();
    }
}