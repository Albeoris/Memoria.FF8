using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleAcid317Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleAcid317Trail
{
    public static (NativeBattleAcid317Frame Frame, NativeBattleGfGeometryBatch Batch) Draw(NativeBattleAcid317Frame frame, uint headAddress, short count, NativeBattleAcid317Resources resources)
    {
        if (count < 2 || count > PointCount)
        {
            throw new InvalidDataException("Acid trail drawing requires its actual linked point count.");
        }
        uint working = frame.State.ReadDword(WorkingGlobal);
        short? previousX = null;
        short? previousY = null;
        uint current = headAddress;
        uint next = Next(frame, current, working);
        frame = Edge(frame, current, next, false, ref previousX, ref previousY, working, resources.DistanceTable);
        current = next;
        for (int index = 0; index < count - 2; index++)
        {
            next = Next(frame, current, working);
            frame = Edge(frame, current, next, true, ref previousX, ref previousY, working, resources.DistanceTable);
            current = next;
        }
        frame = Edge(frame, current, 0, true, ref previousX, ref previousY, working, resources.DistanceTable);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        current = headAddress;
        for (int index = 0; index < count - 1; index++)
        {
            next = Next(frame, current, working);
            int first = Offset(current, working);
            int second = Offset(next, working);
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Acid trail requires its physical point arena.");
            short firstDepth = Word(arena, first + 0x14);
            if (firstDepth > 0 && Word(arena, second + 0x14) > 0)
            {
                SkinWrites packet = new(52, allocated);
                packet.Word(0, 0x0C000000);
                packet.Word(8, Dword(arena, first + 0x10));
                packet.Word(20, Dword(arena, first + 0x18));
                packet.Word(12, 0x3D14E048);
                packet.Word(24, 0x00BAE078);
                packet.Word16(36, 0xE048);
                packet.Word16(48, 0xE078);
                packet.Word(32, Dword(arena, second + 0x10));
                packet.Word(44, Dword(arena, second + 0x18));
                uint firstColor = Gray(Word(arena, first + 0x20));
                uint secondColor = Gray(Word(arena, second + 0x20));
                packet.Word(4, firstColor);
                packet.Word(16, firstColor);
                packet.Byte(7, 0x3E);
                packet.Word(28, secondColor);
                packet.Word(40, secondColor);
                int bucket = 17 + (unchecked(firstDepth + Word(arena, second + 0x14)) >> 3);
                attempts.Add(packet.Attempt(new(bucket, packet.Words(), [])));
                allocated = checked(allocated + 52);
            }
            current = next;
        }
        return (frame, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleAcid317Frame Edge(NativeBattleAcid317Frame frame, uint current, uint next, bool hasPrevious, ref short? previousX, ref short? previousY, uint working, ImmutableArray<byte> distances)
    {
        int first = Offset(current, working);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Acid trail requires its physical point arena.");
        if (Word(arena, first + 0x14) < 0)
        {
            return frame;
        }
        int offsetX = 0;
        int offsetY = 0;
        if (next != 0)
        {
            int second = Offset(next, working);
            if (Word(arena, second + 0x14) < 0)
            {
                return frame;
            }
            int dx = Word(arena, second + 0x10) - Word(arena, first + 0x10);
            int dy = Word(arena, first + 0x12) - Word(arena, second + 0x12);
            if (distances.IsDefault || distances.Length != 128 * 128)
            {
                throw new NotSupportedException("Acid trail edges require their original distance table.");
            }
            int distance = distances[Math.Min(Math.Abs(dy), 127) * 128 + Math.Min(Math.Abs(dx), 127)];
            int directionX;
            int directionY;
            if (hasPrevious)
            {
                int oldX = previousX ?? throw new NotSupportedException("Acid trail requires its produced preceding edge direction.");
                int oldY = previousY ?? throw new NotSupportedException("Acid trail requires its produced preceding edge direction.");
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
                    int factor = unchecked(0x4000 - (unchecked(unchecked(sumX * sumX) + unchecked(sumY * sumY)) >> 13));
                    directionX = unchecked(factor * sumX) >> 13;
                    directionY = unchecked(factor * sumY) >> 13;
                }
            }
            else
            {
                if (distance == 0)
                {
                    return frame with { Memory = frame.Memory with { Arena = arena.Write(first + 0x14, BitConverter.GetBytes((short)-1)) } };
                }
                directionX = unchecked(dy << 12) / distance;
                directionY = unchecked(dx << 12) / distance;
                previousX = unchecked((short)directionX);
                previousY = unchecked((short)directionY);
            }
            int width = unchecked(Word(arena, second + 6) << 5) / (Word(arena, first + 0x14) + 512);
            offsetX = unchecked(width * directionX) >> 12;
            offsetY = unchecked(width * directionY) >> 12;
        }
        short x = Word(arena, first + 0x10);
        short y = Word(arena, first + 0x12);
        arena = arena.Write(first + 0x10, BitConverter.GetBytes(unchecked((short)(x - offsetX)))).Write(first + 0x18, BitConverter.GetBytes(unchecked((short)(x + offsetX))));
        arena = arena.Write(first + 0x12, BitConverter.GetBytes(unchecked((short)(y - offsetY)))).Write(first + 0x1A, BitConverter.GetBytes(unchecked((short)(y + offsetY))));
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }

    private static uint Gray(short intensity)
    {
        int value = intensity >> 4;
        return unchecked((uint)(((value << 8) | value) << 8 | value));
    }

    private static uint Next(NativeBattleAcid317Frame frame, uint address, uint working) => Dword(frame.Memory.Arena ?? throw new NotSupportedException("Acid trail requires its physical point arena."), Offset(address, working) + 0x24);
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static int Offset(uint address, uint working)
    {
        uint relative = unchecked(address - working);
        if (relative < PointsOffset || relative >= PointsOffset + PointCount * PointStride || (relative - PointsOffset) % PointStride != 0)
        {
            throw new InvalidDataException("Acid trail pointer leaves its actual retained point bank.");
        }
        return checked((int)relative);
    }
}
