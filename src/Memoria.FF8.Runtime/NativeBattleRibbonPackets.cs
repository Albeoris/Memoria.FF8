using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleRibbonWidthPolicy
{
    Projected,
    Anchored
}

public sealed record NativeBattleRibbonBank(uint Address, int ArenaOffset, int PointCount);
public sealed record NativeBattleRibbonPacketPolicy(NativeBattleRibbonWidthPolicy Width, byte DepthShift, int TableBaseBucket, uint FirstUvClut, uint SecondUvPage, ushort ThirdUv, ushort FourthUv);

public static class NativeBattleRibbonPackets
{
    public static (NativeBattleEffectArena Arena, NativeBattleGfGeometryBatch Batch) Draw(NativeBattleEffectArena arena, uint headAddress, short count, NativeBattleRibbonBank bank, ImmutableArray<byte> distances, NativeBattleRibbonPacketPolicy policy, uint? incomingDirectionWord = null)
    {
        if (count < 2 || count > bank.PointCount || bank.PointCount > short.MaxValue || bank.PointCount <= 0 || bank.Address == 0 || bank.ArenaOffset < 0 || bank.ArenaOffset > NativeBattleEffectArena.Length - checked(bank.PointCount * 40) || (ulong)bank.Address + (uint)bank.PointCount * 40 > (ulong)uint.MaxValue + 1 || policy.DepthShift > 31 || policy.Width is not (NativeBattleRibbonWidthPolicy.Projected or NativeBattleRibbonWidthPolicy.Anchored))
        {
            throw new InvalidDataException("The ribbon drawing requires its actual linked point count.");
        }
        short? previousX = incomingDirectionWord is uint incomingX ? unchecked((short)incomingX) : null;
        short? previousY = incomingDirectionWord is uint incomingY ? unchecked((short)(incomingY >> 16)) : null;
        uint current = headAddress;
        uint next = Next(arena, current, bank);
        arena = Edge(arena, current, next, false, ref previousX, ref previousY, bank, distances, policy.Width);
        current = next;
        for (int index = 0; index < count - 2; index++)
        {
            next = Next(arena, current, bank);
            arena = Edge(arena, current, next, true, ref previousX, ref previousY, bank, distances, policy.Width);
            current = next;
        }
        arena = Edge(arena, current, 0, true, ref previousX, ref previousY, bank, distances, policy.Width);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        current = headAddress;
        for (int index = 0; index < count - 1; index++)
        {
            next = Next(arena, current, bank);
            int first = Offset(current, bank);
            int second = Offset(next, bank);
            short firstDepth = Word(arena, first + 0x14);
            if (firstDepth > 0 && Word(arena, second + 0x14) > 0)
            {
                SkinWrites packet = new(52, allocated);
                packet.Word(0, 0x0C000000);
                packet.Word(8, Dword(arena, first + 0x10));
                packet.Word(20, Dword(arena, first + 0x18));
                packet.Word(12, policy.FirstUvClut);
                packet.Word(24, policy.SecondUvPage);
                packet.Word16(36, policy.ThirdUv);
                packet.Word16(48, policy.FourthUv);
                packet.Word(32, Dword(arena, second + 0x10));
                packet.Word(44, Dword(arena, second + 0x18));
                uint firstColor = Gray(Word(arena, first + 0x20));
                uint secondColor = Gray(Word(arena, second + 0x20));
                packet.Word(4, firstColor);
                packet.Word(16, firstColor);
                packet.Byte(7, 0x3E);
                packet.Word(28, secondColor);
                packet.Word(40, secondColor);
                int bucket = policy.TableBaseBucket + (unchecked(firstDepth + Word(arena, second + 0x14)) >> policy.DepthShift);
                attempts.Add(packet.Attempt(new(bucket, packet.Words(), [])));
                allocated = checked(allocated + 52);
            }
            current = next;
        }
        return (arena, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleEffectArena Edge(NativeBattleEffectArena arena, uint current, uint next, bool hasPrevious, ref short? previousX, ref short? previousY, NativeBattleRibbonBank bank, ImmutableArray<byte> distances, NativeBattleRibbonWidthPolicy widthPolicy)
    {
        int first = Offset(current, bank);
        if (Word(arena, first + 0x14) < 0)
        {
            return arena;
        }
        int offsetX = 0;
        int offsetY = 0;
        if (next != 0)
        {
            int second = Offset(next, bank);
            if (Word(arena, second + 0x14) < 0)
            {
                return arena;
            }
            int dx = Word(arena, second + 0x10) - Word(arena, first + 0x10);
            int dy = Word(arena, first + 0x12) - Word(arena, second + 0x12);
            if (distances.IsDefault || distances.Length != 128 * 128)
            {
                throw new NotSupportedException("The ribbon edges require their original distance table.");
            }
            int distance = distances[Math.Min(Math.Abs(dy), 127) * 128 + Math.Min(Math.Abs(dx), 127)];
            NativeBattleRibbonDirectionStep direction = NativeBattleRibbonDirection.Resolve(dx, dy, distance, hasPrevious, previousX, previousY);
            previousX = direction.PreviousX;
            previousY = direction.PreviousY;
            if (!direction.Valid)
            {
                return arena.Write(first + 0x14, BitConverter.GetBytes((short)-1));
            }
            int widthOffset = widthPolicy == NativeBattleRibbonWidthPolicy.Projected ? 6 : 2;
            int depthBias = widthPolicy == NativeBattleRibbonWidthPolicy.Projected ? 512 : 1024;
            int width = unchecked(Word(arena, second + widthOffset) << 5) / (Word(arena, first + 0x14) + depthBias);
            offsetX = unchecked(width * direction.X) >> 12;
            offsetY = unchecked(width * direction.Y) >> 12;
        }
        short x = Word(arena, first + 0x10);
        short y = Word(arena, first + 0x12);
        arena = arena.Write(first + 0x10, BitConverter.GetBytes(unchecked((short)(x - offsetX)))).Write(first + 0x18, BitConverter.GetBytes(unchecked((short)(x + offsetX))));
        arena = arena.Write(first + 0x12, BitConverter.GetBytes(unchecked((short)(y - offsetY)))).Write(first + 0x1A, BitConverter.GetBytes(unchecked((short)(y + offsetY))));
        return arena;
    }

    private static uint Gray(short intensity)
    {
        int value = intensity >> 4;
        return unchecked((uint)(((value << 8) | value) << 8 | value));
    }

    private static uint Next(NativeBattleEffectArena arena, uint address, NativeBattleRibbonBank bank) => Dword(arena, Offset(address, bank) + 0x24);
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static int Offset(uint address, NativeBattleRibbonBank bank)
    {
        uint relative = unchecked(address - bank.Address);
        if (relative >= bank.PointCount * 40 || relative % 40 != 0)
        {
            throw new InvalidDataException("The ribbon pointer leaves its actual retained point bank.");
        }
        return checked(bank.ArenaOffset + (int)relative);
    }
}
