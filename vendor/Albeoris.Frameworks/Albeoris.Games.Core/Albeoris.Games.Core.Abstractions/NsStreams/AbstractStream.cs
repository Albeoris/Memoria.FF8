namespace Albeoris.Games.Core.Abstractions.NsStreams;

public abstract class AbstractStream : Stream
{
    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();
    public override void Write(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
    public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(Int64 value) => throw new NotSupportedException();

    public override Boolean CanRead => false;
    public override Boolean CanWrite => false;
    public override Boolean CanSeek => false;
    public override Int64 Length => throw new NotSupportedException();

    public override Int64 Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
}