using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Packet production retains physical links; ordering-table insertion and environment execution belong to the caller.</summary>
public static class NativeBattleDrawEnvironmentPackets
{
    public static ImmutableArray<byte> Restore45C0F0(ImmutableArray<byte> retainedStatic64, ImmutableArray<byte> drawEnvironment92)
    {
        Require(retainedStatic64, 64, true);
        Require(drawEnvironment92, 92, true);
        byte[] result = retainedStatic64.ToArray();
        bool clear = drawEnvironment92[0x18] != 0;
        Store(result, 0, (Read(result, 0) & 0xFFFFFF) | (clear ? 0x08000000u : 0x05000000u));
        short x = Word(0);
        short y = Word(2);
        Store(result, 4, AreaStart(x, y));
        Store(result, 8, AreaEnd(x, y, Word(4), Word(6)));
        Store(result, 12, Offset(Word(8), Word(10)));
        Store(result, 16, 0xE1000000u | ((uint)Word(0x14) & 0x9FF) | (drawEnvironment92[0x16] != 0 ? 0x200u : 0) | (drawEnvironment92[0x17] != 0 ? 0x400u : 0));
        uint windowW = BinaryPrimitives.ReadUInt16LittleEndian(drawEnvironment92.AsSpan().Slice(0x10));
        uint windowH = drawEnvironment92[0x12];
        uint windowX = unchecked((ushort)Word(0xC));
        uint windowY = unchecked((ushort)Word(0xE));
        uint window = unchecked(0xE2000000u | ((~(windowW - 1) >> 3) & 0x1F) | ((~(windowH - 1) & 0xF8) << 2) | ((windowX & 0xF8) << 7) | ((windowY & 0xF8) << 12));
        Store(result, 20, window);
        if (clear)
        {
            Store(result, 24, 0x02000000u | drawEnvironment92[0x19] | ((uint)drawEnvironment92[0x1A] << 8) | ((uint)drawEnvironment92[0x1B] << 16));
            Store(result, 28, BinaryPrimitives.ReadUInt32LittleEndian(drawEnvironment92.AsSpan()));
            Store(result, 32, BinaryPrimitives.ReadUInt32LittleEndian(drawEnvironment92.AsSpan().Slice(4)));
        }
        return result.ToImmutableArray();

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(drawEnvironment92.AsSpan().Slice(offset));
    }

    public static ImmutableArray<byte> Area45C940(ImmutableArray<byte> retainedAllocation, short x, short y, short width, short height)
    {
        Require(retainedAllocation, 12, false);
        byte[] result = retainedAllocation.ToArray();
        Store(result, 0, (Read(result, 0) & 0xFFFFFF) | 0x02000000);
        Store(result, 4, AreaStart(x, y));
        Store(result, 8, AreaEnd(x, y, width, height));
        return result.ToImmutableArray();
    }

    public static ImmutableArray<byte> Offset45C9B0(ImmutableArray<byte> retainedAllocation, short x, short y)
    {
        Require(retainedAllocation, 8, false);
        byte[] result = retainedAllocation.ToArray();
        Store(result, 0, (Read(result, 0) & 0xFFFFFF) | 0x01000000);
        Store(result, 4, Offset(x, y));
        return result.ToImmutableArray();
    }

    private static uint AreaStart(short x, short y) => 0xE3000000u | ((uint)x & 0x3FF) | (((uint)y & 0x1FF) << 10);
    private static uint AreaEnd(short x, short y, short width, short height) => unchecked(0xE4000000u | ((uint)(x + width - 1) & 0x3FF) | (((uint)(y + height - 1) << 10) & 0x7FC00));
    private static uint Offset(short x, short y) => 0xE5000000u | ((uint)x & 0x7FF) | (((uint)y & 0x7FF) << 11);
    private static uint Read(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    private static void Store(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan().Slice(offset), value);

    private static void Require(ImmutableArray<byte> bytes, int length, bool exact)
    {
        if (bytes.IsDefault || (exact ? bytes.Length != length : bytes.Length < length))
        {
            throw new InvalidDataException("Native draw environment packet requires its actual complete source and retained destination bytes.");
        }
    }
}
