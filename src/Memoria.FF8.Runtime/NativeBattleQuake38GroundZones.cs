using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38GroundZones
{
    public static NativeBattleEffectArena Assign8D9950(NativeBattleEffectArena arena, int entryOffset, int zonesOffset, int edge, int first, int second, int centroid, NativeBattleQuake38Resources resources, short halfX, short halfZ, short offsetZ)
    {
        if (entryOffset < 0 || entryOffset > NativeBattleEffectArena.Length - 112 || zonesOffset < 0 || zonesOffset > NativeBattleEffectArena.Length - 0xCC0 || (uint)edge >= 4 || (uint)first >= 5 || (uint)second >= 5 || (uint)centroid >= 5)
        {
            throw new InvalidDataException("Native Quake zone assignment requires its actual split entry, edge selectors and vertex-zone bank.");
        }
        // Two entry bytes, then five passes of three mask/count WORD pairs, including a truncated zero bit.
        Span<NativeBattleQuake38MemoryWrite> writes = stackalloc NativeBattleQuake38MemoryWrite[2 + 5 * 3 * 2];
        NativeBattleQuake38MemoryPlan plan = new(arena, writes);
        short x = unchecked((short)((plan.ReadInt16(entryOffset + first * 8) + plan.ReadInt16(entryOffset + second * 8) + plan.ReadInt16(entryOffset + centroid * 8)) / 3));
        short z = unchecked((short)((plan.ReadInt16(entryOffset + first * 8 + 4) + plan.ReadInt16(entryOffset + second * 8 + 4) + plan.ReadInt16(entryOffset + centroid * 8 + 4)) / 3));
        byte zone = NativeBattleQuake38Zones.Read8D9A80(resources.ZoneMap, x, z, halfX, halfZ, offsetZ);
        plan.WriteByte(entryOffset + 88 + edge, zone);
        plan.WriteByte(entryOffset + 92 + edge, NativeBattleQuake38Zones.Read8D9A80(resources.DelayMap, x, z, halfX, halfZ, offsetZ));
        ushort bit = unchecked((ushort)(1 << zone));
        for (int pass = 0; pass < 5; pass++)
        {
            Mark(ref plan, entryOffset, zonesOffset, first, bit);
            Mark(ref plan, entryOffset, zonesOffset, second, bit);
            Mark(ref plan, entryOffset, zonesOffset, centroid, bit);
        }
        return plan.Commit();
    }

    private static void Mark(ref NativeBattleQuake38MemoryPlan plan, int entryOffset, int zonesOffset, int selector, ushort bit)
    {
        int index = plan.ReadInt16(entryOffset + 52 + selector * 2);
        int offset = checked(index * 4);
        if (offset < 0 || offset + 4 > 0xCC0)
        {
            throw new InvalidDataException("Native Quake vertex-zone index leaves its original bank.");
        }
        ushort mask = plan.ReadUInt16(zonesOffset + offset);
        if ((mask & bit) == 0)
        {
            plan.WriteUInt16(zonesOffset + offset, (ushort)(mask | bit));
            ushort count = plan.ReadUInt16(zonesOffset + offset + 2);
            plan.WriteUInt16(zonesOffset + offset + 2, unchecked((ushort)(count + 1)));
        }
    }
}

internal readonly record struct NativeBattleQuake38MemoryWrite(int Offset, ushort Value, byte Length);

/// <summary>Bounded native stores read their preceding local stores before one accepted helper publication.</summary>
internal ref struct NativeBattleQuake38MemoryPlan(NativeBattleEffectArena arena, Span<NativeBattleQuake38MemoryWrite> writes)
{
    private readonly NativeBattleEffectArena arena = arena;
    private readonly Span<NativeBattleQuake38MemoryWrite> writes = writes;
    private int count;

    public byte ReadByte(int offset)
    {
        for (int index = count - 1; index >= 0; index--)
        {
            NativeBattleQuake38MemoryWrite write = writes[index];
            if (offset >= write.Offset && offset - write.Offset < write.Length)
            {
                return unchecked((byte)(write.Value >> ((offset - write.Offset) * 8)));
            }
        }
        return arena.ReadByte(offset);
    }

    public ushort ReadUInt16(int offset) => unchecked((ushort)(ReadByte(offset) | ReadByte(offset + 1) << 8));
    public short ReadInt16(int offset) => unchecked((short)ReadUInt16(offset));
    public void WriteByte(int offset, byte value) => Store(new(offset, value, 1));
    public void WriteUInt16(int offset, ushort value) => Store(new(offset, value, 2));

    public NativeBattleEffectArena Commit()
    {
        if (count == 0)
        {
            return arena;
        }
        NativeBattleEffectArenaWriter writer = new(arena);
        Span<byte> bytes = stackalloc byte[2];
        for (int index = 0; index < count; index++)
        {
            NativeBattleQuake38MemoryWrite write = writes[index];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, write.Value);
            writer.Write(write.Offset, bytes[..write.Length]);
        }
        return writer.Freeze();
    }

    private void Store(NativeBattleQuake38MemoryWrite write)
    {
        if (count == writes.Length)
        {
            throw new InvalidDataException("Native Quake stores exceed their bounded helper operation.");
        }
        writes[count++] = write;
    }
}
