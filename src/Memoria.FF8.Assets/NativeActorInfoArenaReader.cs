using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Checks the declared record capacity of the authenticated native field loader.</summary>
public static class NativeActorInfoArenaReader
{
    public static int ReadDeclaredRecords(NativeFieldRuntimeProfile? profile, ReadOnlySpan<byte> header, long payloadLength)
    {
        if (profile is not { IsVerified: true })
        {
            throw new NotSupportedException("Native actor information requires the authenticated field runtime profile.");
        }
        if (header.Length < 8 || payloadLength < 8)
        {
            throw new InvalidDataException("A field JSM is missing its complete native header.");
        }
        int records = header[0] + header[1] + header[2] + header[3];
        int scriptsOffset = BinaryPrimitives.ReadUInt16LittleEndian(header[4..]);
        int operationsOffset = BinaryPrimitives.ReadUInt16LittleEndian(header[6..]);
        if (records > byte.MaxValue || scriptsOffset < 8 + records * 2 || scriptsOffset > operationsOffset || operationsOffset > payloadLength || (scriptsOffset & 1) != 0 || (operationsOffset & 3) != 0)
        {
            throw new InvalidDataException("Field JSM declarations exceed the native record capacity or disagree with its table boundaries.");
        }
        return records;
    }

    internal static IFlArchive OpenHeaderArchive(byte[] listing, byte[] metrics, Stream content, long length)
    {
        return FlArchive.Open(new MemoryStream(listing, false), new MemoryStream(metrics, false), content.CanSeek ? content : new ForwardHeaderStream(content, length), false, FlArchiveRepresentation.Files);
    }

    private sealed class ForwardHeaderStream(Stream source, long length) : Stream
    {
        private long position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            int count = source.Read(buffer[..(int)Math.Min(buffer.Length, length - position)]);
            position += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long destination = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(position + offset),
                SeekOrigin.End => checked(length + offset),
                _ => throw new ArgumentException("Unknown stream seek origin.", nameof(origin))
            };
            if (destination < position || destination > length)
            {
                throw new NotSupportedException("A field header scan requires forward reads within the declared archive content.");
            }
            Span<byte> discard = stackalloc byte[8192];
            while (position < destination)
            {
                if (Read(discard[..(int)Math.Min(discard.Length, destination - position)]) == 0)
                {
                    throw new EndOfStreamException();
                }
            }
            return position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                source.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
