namespace Albeoris.Games.Core.NSCompression.LZS;

/// <summary>Reads LZS data up to the caller's known decompressed size.</summary>
public class LZSDecompressionStream : Stream
{
    private readonly Stream input;
    private readonly bool leaveOpen;
    private readonly LzsDecoderKernel kernel = new();
    private readonly byte[] token = new byte[2];
    private long remaining;
    private bool disposed;

    public LZSDecompressionStream(Stream compressedStream, long compressedSize, long decompressedSize, bool leaveOpen)
    {
        if (compressedStream == null)
        {
            throw new ArgumentNullException(nameof(compressedStream));
        }
        if (!compressedStream.CanRead)
        {
            throw new ArgumentException("The compressed stream does not support reading.", nameof(compressedStream));
        }
        input = compressedStream;
        remaining = decompressedSize;
        this.leaveOpen = leaveOpen;
    }

    public override bool CanRead => !disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateRead(buffer, offset, count);
        int bytesRead = 0;
        while (bytesRead < count && remaining > 0)
        {
            if (kernel.TryReadOutput(out byte value))
            {
                buffer[offset + bytesRead++] = value;
                remaining--;
                continue;
            }
            int required = kernel.RequiredInputBytes;
            for (int index = 0; index < required; index++)
            {
                int next = input.ReadByte();
                if (next == -1)
                {
                    throw new Exception("Unexpected end of stream.");
                }
                token[index] = (byte)next;
            }
            kernel.AcceptInput(token.AsSpan(0, required));
        }
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateRead(buffer, offset, count);
        int bytesRead = 0;
        while (bytesRead < count && remaining > 0)
        {
            if (kernel.TryReadOutput(out byte value))
            {
                buffer[offset + bytesRead++] = value;
                remaining--;
                continue;
            }
            int required = kernel.RequiredInputBytes;
            int total = 0;
            while (total < required)
            {
                int read = await input.ReadAsync(token, total, required - total, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new Exception("Unexpected end of stream.");
                }
                total += read;
            }
            kernel.AcceptInput(token.AsSpan(0, required));
        }
        return bytesRead;
    }

    private void ValidateRead(byte[] buffer, int offset, int count)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(LZSDecompressionStream));
        }
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException();
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing && !leaveOpen)
            {
                input.Dispose();
            }
            disposed = true;
        }
        base.Dispose(disposing);
    }
}
