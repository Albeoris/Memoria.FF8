using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlPathBatch(ImmutableArray<NativeBattleQuezacotlTrailRowWrite> RowWrites, NativeBattleGfGeometryBatch EffectPackets, ImmutableArray<int?> FinalDepthFifo4)
{
    public uint? FinalOutlineDirectionDword { get; init; }
}

/// <summary>6C79A0/6C7B30's actual linked path projection, sparse row stores and effect-heap GT4s.</summary>
public static class NativeBattleQuezacotlPathPackets
{
    public static NativeBattleQuezacotlPathBatch Emit6C79A0(uint head, int count, Func<uint, int, int, ImmutableArray<byte>> readRow, Func<short, NativeBattleVector> readCurrentVertex, NativeBattleActionMatrix capturedMatrix, NativeBattleCameraProjection projection, ImmutableArray<byte> distanceBytes16384, ImmutableArray<int?> incomingDepthFifo4 = default, uint? incomingOutlineDirectionDword = null)
    {
        if (count is < 2 or > short.MaxValue)
        {
            throw new InvalidDataException("Native6C79A0 requires its admitted signed-WORD path count.");
        }
        if (!incomingDepthFifo4.IsDefault && (incomingDepthFifo4.Length != 4 || incomingDepthFifo4.Any(depth => depth is < 0 or > 65535)))
        {
            throw new InvalidDataException("Native6C79A0 incoming depth FIFO requires four actual or explicitly unknown full-depth values.");
        }
        int?[] fifo = incomingDepthFifo4.IsDefault ? new int?[4] : incomingDepthFifo4.ToArray();
        List<uint> addresses = [];
        List<NativeBattleModuleProjectedVertex> projected = [];
        HashSet<uint> visited = [];
        ImmutableArray<NativeBattleQuezacotlTrailRowWrite>.Builder writes = ImmutableArray.CreateBuilder<NativeBattleQuezacotlTrailRowWrite>();
        uint address = head;
        //6C79AD..79DC follows Next until NULL, independently of the later draw count.
        while (address != 0)
        {
            if (!visited.Add(address))
            {
                throw new NotSupportedException("Native6C79A0 requires its actual acyclic projected row chain.");
            }
            short selector = Read16(address, 0);
            NativeBattleModuleProjectedVertex vertex = NativeBattleModuleProjection.Project45FE10(capturedMatrix, readCurrentVertex(selector), projection);
            addresses.Add(address);
            projected.Add(vertex);
            SkinWrites stores = new(28, 0);
            stores.Word(4, vertex.Screen);
            stores.Word(8, (uint)(vertex.Depth >> 2));
            writes.Add(new(address, stores.Attempt(null)));
            Array.Copy(fifo, 1, fifo, 0, 3);
            fifo[3] = vertex.Depth;
            address = Read32(address, 0x18);
        }
        if (addresses.Count < count)
        {
            throw new NotSupportedException("Native6C79A0 draw count reaches beyond its actual projected row chain.");
        }
        NativeBattleTrailOutline.Point[] outline = new NativeBattleTrailOutline.Point[count];
        for (int index = 0; index < count; index++)
        {
            uint selectedAddress = addresses[index];
            NativeBattleModuleProjectedVertex vertex = projected[index];
            outline[index] = new(vertex.Screen, (short)(vertex.Depth >> 2), () => Read16(selectedAddress, 2));
        }
        uint? direction = NativeBattleTrailOutline.Expand(outline, distanceBytes16384, 1024, incomingOutlineDirectionDword);
        for (int index = 0; index < count; index++)
        {
            NativeBattleTrailOutline.Point point = outline[index];
            if (!point.ScreenWritten && !point.DepthWritten)
            {
                continue;
            }
            SkinWrites stores = new(28, 0);
            if (point.ScreenWritten)
            {
                stores.Word(4, point.Left);
                stores.Word(0xC, point.Right ?? throw new InvalidDataException("Native6C7B30 screen stores lack their right endpoint."));
            }
            if (point.DepthWritten)
            {
                //6C7C33 writes only the low WORD after45E250's earlier full DWORD store.
                stores.Word16(8, unchecked((ushort)point.QuarterDepth));
            }
            writes.Add(new(addresses[index], stores.Attempt(null)));
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
            if (fifo.Any(depth => depth is null))
            {
                throw new NotSupportedException("Native6C79A0 accepted packet requires its actual four full-depth FIFO values after the entire projection chain.");
            }
            uint rightA = first.Right ?? Read32(addresses[index], 0xC);
            uint rightB = second.Right ?? Read32(addresses[index + 1], 0xC);
            short colorA = (short)(Read16(addresses[index], 0x14) >> 4);
            short colorB = (short)(Read16(addresses[index + 1], 0x14) >> 4);
            int bucket = 17 + ((first.QuarterDepth + second.QuarterDepth) >> 3);
            packets.Add(NativeBattleTrailOutline.Packet(allocated, first.Left, rightA, second.Left, rightB, colorA, colorB, bucket, fifo.Select(depth => depth!.Value).ToImmutableArray()));
            allocated = checked(allocated + 52);
        }
        return new(writes.ToImmutable(), new(packets.ToImmutable(), allocated), fifo.ToImmutableArray()) { FinalOutlineDirectionDword = direction };

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
            ImmutableArray<byte> bytes = readRow(selectedAddress, offset, length);
            if (bytes.IsDefault || bytes.Length != length)
            {
                throw new InvalidDataException("Native6C79A0 row read did not return its requested actual byte span.");
            }
            return bytes;
        }
    }
}
