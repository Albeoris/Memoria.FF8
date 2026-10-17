using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlTrailRowWrite(uint Address, NativeBattleGfGeometryAttempt Attempt);
public sealed record NativeBattleQuezacotlTrailBatch(ImmutableArray<NativeBattleQuezacotlTrailRowWrite> RowWrites, NativeBattleGfGeometryBatch EffectPackets);

/// <summary>6C84C0/6C8620's current linked rows, exact outline stores and effect-heap GT4 packets.</summary>
public static class NativeBattleQuezacotlTrailPackets
{
    public static NativeBattleQuezacotlTrailBatch Emit6C84C0(uint head, int count, Func<uint, int, int, ImmutableArray<byte>> readRow, ImmutableArray<byte> distanceBytes16384, ImmutableArray<int> depthFifo4)
    {
        if (count is < 2 or > short.MaxValue)
        {
            throw new InvalidDataException("Native6C84C0 requires its admitted signed-WORD chain count and actual four full-depth FIFO values.");
        }
        uint[] addresses = new uint[count];
        NativeBattleTrailOutline.Point[] outline = new NativeBattleTrailOutline.Point[count];
        HashSet<uint> visited = [];
        uint address = head;
        for (int index = 0; index < count; index++)
        {
            if (address == 0 || !visited.Add(address))
            {
                throw new NotSupportedException("Native6C84C0 requires the actual nonnull acyclic row chain for its declared count.");
            }
            uint selectedAddress = address;
            addresses[index] = selectedAddress;
            outline[index] = new(Read32(selectedAddress, 0x10), Read16(selectedAddress, 0x14), () => Read16(selectedAddress, 6));
            if (index < count - 1)
            {
                address = Read32(selectedAddress, 0x24);
            }
        }
        NativeBattleTrailOutline.Expand(outline, distanceBytes16384, 512);
        ImmutableArray<NativeBattleQuezacotlTrailRowWrite>.Builder rowWrites = ImmutableArray.CreateBuilder<NativeBattleQuezacotlTrailRowWrite>();
        for (int index = 0; index < count; index++)
        {
            NativeBattleTrailOutline.Point point = outline[index];
            if (!point.ScreenWritten && !point.DepthWritten)
            {
                continue;
            }
            SkinWrites row = new(40, 0);
            if (point.ScreenWritten)
            {
                row.Word(0x10, point.Left);
                row.Word(0x18, point.Right ?? throw new InvalidDataException("Native outline store lacks its right screen DWORD."));
            }
            if (point.DepthWritten)
            {
                row.Word16(0x14, unchecked((ushort)point.QuarterDepth));
            }
            rowWrites.Add(new(addresses[index], row.Attempt(null)));
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count - 1; index++)
        {
            NativeBattleTrailOutline.Point first = outline[index];
            NativeBattleTrailOutline.Point second = outline[index + 1];
            if (first.QuarterDepth <= 0 || second.QuarterDepth <= 0)
            {
                continue;
            }
            uint rightA = first.Right ?? Read32(addresses[index], 0x18);
            uint rightB = second.Right ?? Read32(addresses[index + 1], 0x18);
            short colorA = (short)(Read16(addresses[index], 0x20) >> 4);
            short colorB = (short)(Read16(addresses[index + 1], 0x20) >> 4);
            int bucket = 17 + ((first.QuarterDepth + second.QuarterDepth) >> 3);
            if (depthFifo4.IsDefault || depthFifo4.Length != 4 || depthFifo4.Any(depth => depth is < 0 or > 65535))
            {
                throw new NotSupportedException("Native6C84C0 packet insertion requires its actual frozen four full-depth FIFO values.");
            }
            packets.Add(NativeBattleTrailOutline.Packet(allocated, first.Left, rightA, second.Left, rightB, colorA, colorB, bucket, depthFifo4));
            allocated = checked(allocated + 52);
        }
        return new(rowWrites.ToImmutable(), new(packets.ToImmutable(), allocated));

        short Read16(uint selectedAddress, int offset)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(Read(selectedAddress, offset, 2).AsSpan());
        }

        uint Read32(uint selectedAddress, int offset)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(Read(selectedAddress, offset, 4).AsSpan());
        }

        ImmutableArray<byte> Read(uint selectedAddress, int offset, int length)
        {
            ImmutableArray<byte> result = readRow(selectedAddress, offset, length);
            if (result.IsDefault || result.Length != length)
            {
                throw new InvalidDataException("Native trail row read did not return its requested actual byte span.");
            }
            return result;
        }
    }
}
