using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Controller;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDisolvingAcid132Trail
{
    public static (NativeBattleDisolvingAcid132Frame Frame, NativeBattleGfGeometryBatch Batch) DrawRibbon6B2870(NativeBattleDisolvingAcid132Frame frame, uint head, short count, NativeBattleDisolvingAcid132Resources resources, NativeBattlePilotVisualIdentity? pilotIdentity = null, IReadOnlyDictionary<uint, System.Numerics.Vector3>? pilotWorld = null)
    {
        if (count < 2 || count > 128)
        {
            throw new InvalidDataException("Disolving Acid requires its actual linked curve count.");
        }
        uint[] chain = Chain(frame, head).ToArray();
        if (chain.Length != count)
        {
            throw new InvalidDataException("Disolving Acid curve count differs from its actual chain.");
        }
        Dictionary<uint, uint>? centers = pilotIdentity.HasValue && pilotWorld is not null ? chain.ToDictionary(point => point, point => PointDword(frame, point + 8)) : null;
        short? previousX = null;
        short? previousY = null;
        for (int index = 0; index < count; index++)
        {
            frame = Edge6B29E0(frame, chain[index], index + 1 < count ? chain[index + 1] : 0, index != 0, ref previousX, ref previousY, resources.DistanceTable);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count - 1; index++)
        {
            uint current = chain[index];
            uint next = chain[index + 1];
            if (PointSigned(frame, current + 12) <= 0 || PointSigned(frame, next + 12) <= 0 || PointSigned(frame, current + 14) > 7)
            {
                continue;
            }
            byte[] packet = new byte[52];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[52];
            Put32(0, 0x0C000000);
            Put32(8, PointDword(frame, current + 8));
            Put32(20, PointDword(frame, current + 16));
            Put32(32, PointDword(frame, next + 8));
            Put32(44, PointDword(frame, next + 16));
            int ordinal = PointSigned(frame, current + 14);
            int uv = unchecked(((ordinal + 1) * 7) << 13);
            int tail = unchecked(uv + 0x2000);
            if (unchecked((ushort)tail) > 0xFF00)
            {
                tail = 0xFF00;
            }
            tail &= 0xFFFF;
            Put32(12, unchecked((uint)tail) | 0x3C140048);
            Put32(24, unchecked((uint)tail) | 0x00B90078);
            Put16(36, unchecked((ushort)(uv | 0x48)));
            Put16(48, unchecked((ushort)(uv | 0x78)));
            Put32(4, 0x3E808080);
            Put32(16, 0x00808080);
            Put32(28, 0x00808080);
            Put32(40, 0x00808080);
            int bucket = 17 + ((PointSigned(frame, current + 12) + PointSigned(frame, next + 12)) >> 3);
            ImmutableArray<uint>.Builder words = ImmutableArray.CreateBuilder<uint>(13);
            for (int offset = 0; offset < 52; offset += 4)
            {
                words.Add(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(offset)));
            }
            NativeBattleGeometryPrimitive primitive = new(bucket, words.ToImmutable(), []);
            if (pilotIdentity.HasValue && pilotWorld is not null && centers is not null)
            {
                uint bank = frame.State.ReadDword(WorkingGlobal) + PointsOffset;
                int a = (int)((current - bank) / PointStride);
                int b = (int)((next - bank) / PointStride);
                NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(132, Arena(frame), bank, a, b) with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(132, Arena(frame), bank, b).Generation };
                System.Numerics.Vector2 OffsetFrom(uint corner, uint center) => new(unchecked((short)corner) - unchecked((short)center), unchecked((short)(corner >> 16)) - unchecked((short)(center >> 16)));
                primitive = primitive with { PilotGeometry = new(identity, pilotWorld[current], pilotWorld[current], pilotWorld[next], pilotWorld[next], 4) { ScreenOffsets = [OffsetFrom(PointDword(frame, current + 8), centers[current]), OffsetFrom(PointDword(frame, current + 16), centers[current]), OffsetFrom(PointDword(frame, next + 8), centers[next]), OffsetFrom(PointDword(frame, next + 16), centers[next])], SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } };
            }
            attempts.Add(new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            allocated += 52;

            void Put32(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
            }
            void Put16(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
            }
        }
        return (frame, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleDisolvingAcid132Frame Edge6B29E0(NativeBattleDisolvingAcid132Frame frame, uint current, uint next, bool hasPrevious, ref short? previousX, ref short? previousY, ImmutableArray<byte> distances)
    {
        if (PointSigned(frame, current + 0xC) < 0)
        {
            return frame;
        }
        int offsetX = 0;
        int offsetY = 0;
        if (next != 0)
        {
            if (PointSigned(frame, next + 0xC) < 0)
            {
                return frame;
            }
            int dx = PointSigned(frame, next + 8) - PointSigned(frame, current + 8);
            int dy = PointSigned(frame, current + 0xA) - PointSigned(frame, next + 0xA);
            if (distances.IsDefault || distances.Length != 16384)
            {
                throw new NotSupportedException("Disolving Acid requires its actual edge distance table.");
            }
            int distance = distances[Math.Min(Math.Abs(dy), 127) * 128 + Math.Min(Math.Abs(dx), 127)];
            int directionX;
            int directionY;
            if (hasPrevious)
            {
                int oldX = previousX ?? throw new NotSupportedException("Disolving Acid requires its produced preceding edge direction.");
                int oldY = previousY ?? throw new NotSupportedException("Disolving Acid requires its produced preceding edge direction.");
                if (distance == 0)
                {
                    directionX = oldX;
                    directionY = oldY;
                }
                else
                {
                    int normalizedX = unchecked(dy << 12) / distance;
                    int normalizedY = unchecked(dx << 12) / distance;
                    previousX = unchecked((short)normalizedX);
                    previousY = unchecked((short)normalizedY);
                    int sumX = unchecked(normalizedX + oldX);
                    int sumY = unchecked(normalizedY + oldY);
                    if (sumX == 0 && sumY == 0)
                    {
                        sumX = unchecked(normalizedX * 2);
                        sumY = unchecked(normalizedY * 2);
                    }
                    int factor = unchecked(0x4000 - (unchecked(sumX * sumX + sumY * sumY) >> 13));
                    directionX = unchecked(factor * sumX) >> 13;
                    directionY = unchecked(factor * sumY) >> 13;
                }
            }
            else
            {
                if (distance == 0)
                {
                    return Word(frame, current + 0xC, ushort.MaxValue);
                }
                directionX = unchecked(dy << 12) / distance;
                directionY = unchecked(dx << 12) / distance;
                previousX = unchecked((short)directionX);
                previousY = unchecked((short)directionY);
            }
            int width = unchecked(PointSigned(frame, next + 6) << 5) / (PointSigned(frame, current + 0xC) + 512);
            offsetX = unchecked(width * directionX) >> 12;
            offsetY = unchecked(width * directionY) >> 12;
        }
        short x = PointSigned(frame, current + 8);
        short y = PointSigned(frame, current + 0xA);
        frame = Word(frame, current + 8, unchecked((ushort)(x - offsetX)));
        frame = Word(frame, current + 0x10, unchecked((ushort)(x + offsetX)));
        frame = Word(frame, current + 0x12, unchecked((ushort)(y + offsetY)));
        return Word(frame, current + 0xA, unchecked((ushort)(y - offsetY)));
    }

}
