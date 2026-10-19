using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlParticleArguments(NativeBattleVector Drift, NativeBattleVector Position, NativeBattleVector Target, NativeBattleVector? TrailVelocity, short VelocityScale, short InitialProgress, short ProgressStep, short Width, short ImpactMode, short BranchBudget);
public sealed record NativeBattleQuezacotlParticleSpawn(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, int? Slot, int? Row);
public sealed record NativeBattleQuezacotlParticleRow(NativeBattleEffectMemory Memory, int? Row);

public static class NativeBattleQuezacotlPrivateRandom
{
    public static ushort Draw6C7CF0(ref ushort state)
    {
        //6C7CF0 has the same proved125*x+14 modulo32768 transition as4A7080, but a separate retained seed.
        _ = new NativeBattleUiRandomState(state).Draw(out ushort result);
        state = result;
        return result;
    }
}

/// <summary>6C7E30's retained40-byte row bank and6C7D10's independent32x3C task producer.</summary>
public static class NativeBattleQuezacotlParticleProducer
{
    public const int Rows = 0x3C9FC;
    public const int RowSize = 0x28;
    public const int RowCount = 256;
    public const int CachedRow = 0x3F1FC;
    public const uint ArenaAddress = 0x20DFAB8;

    public static NativeBattleQuezacotlParticleRow Allocate6C7E30(NativeBattleEffectMemory memory, NativeBattleVector position, short width)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C7E30 requires its phase86 row bank.");
        uint cached = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(CachedRow, 4));
        int row;
        if (cached != 0)
        {
            row = RowOffset(cached);
            arena = arena.Write(CachedRow, [0, 0, 0, 0]);
        }
        else
        {
            row = -1;
            for (int index = 0; index < RowCount; index++)
            {
                int offset = Rows + index * RowSize;
                if (BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 6, 2)) == -1)
                {
                    row = offset;
                    break;
                }
            }
            if (row < 0)
            {
                return new(memory, null);
            }
        }
        NativeBattleEffectArenaWriter writer = new(arena);
        byte[] raw = new byte[6];
        PutVector(raw, 0, position);
        writer.Write(row, raw);
        Word(writer, row + 6, width);
        Word(writer, row + 0xE, 2);
        Word(writer, row + 0x20, 512);
        Word(writer, row + 0x22, 768);
        writer.Write(row + 0x24, [0, 0, 0, 0]);
        return new(memory with { Arena = writer.Freeze() }, row);
    }

    public static NativeBattleQuezacotlParticleSpawn Spawn6C7D10(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleQuezacotlParticleArguments arguments)
    {
        NativeBattleQuezacotlParticleRow allocated = Allocate6C7E30(memory, arguments.Position, arguments.Width);
        memory = allocated.Memory;
        if (allocated.Row is not int row)
        {
            return new(memory, effect, null, null);
        }
        NativeBattleEffectArena arena = memory.Arena!;
        NativeBattleEffectTaskPool pool = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521718);
        pool.Validate();
        if (pool.BankAddress != 0x211BD34 || pool.Stride != 0x3C || pool.Slots.Length != 32)
        {
            throw new InvalidDataException("Native6C7D10 lost its phase86 particle task bank.");
        }
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return new(memory with { Arena = Word(arena, row + 6, -1) }, effect, null, null);
        }
        int? previousTail = pool.Tail;
        (pool, int slot) = pool.Append(0x6C7EA0);
        int address = checked((int)(pool.BankAddress - ArenaAddress) + slot * pool.Stride);
        //508360 plus6C7D10 write every final byte; old flags come from the separate required slot owner.
        byte[] raw = new byte[pool.Stride];
        BinaryPrimitives.WriteUInt16LittleEndian(raw, pool.Slots[slot].Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), 0x6C7EA0);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0xC), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0xE), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x10), checked(ArenaAddress + (uint)row));
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x14), checked(ArenaAddress + (uint)row));
        PutVector(raw, 0x18, arguments.Position);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x1E), arguments.InitialProgress);
        PutVector(raw, 0x20, arguments.Target);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x26), arguments.ProgressStep);
        PutVector(raw, 0x28, arguments.Drift);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x2E), arguments.BranchBudget);
        if (arguments.TrailVelocity is NativeBattleVector velocity)
        {
            PutVector(raw, 0x30, velocity);
        }
        else
        {
            arena = arena.Write(row + 8, new byte[6]);
            ushort random = effect.Quezacotl?.Random17A0 ?? throw new InvalidDataException("Native6C7CF0 lost its private seed.");
            short Noise() => unchecked((short)((NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random) & 255) - 128));
            PutVector(raw, 0x30, new(Noise(), Noise(), Noise()));
            effect = effect with { Quezacotl = effect.Quezacotl! with { Random17A0 = random } };
        }
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x36), arguments.VelocityScale);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x38), arguments.Width);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x3A), arguments.ImpactMode);
        arena = arena.Write(address, raw);
        if (previousTail is int tail)
        {
            arena = Dword(arena, checked((int)(pool.BankAddress - ArenaAddress) + tail * pool.Stride + 4), checked(pool.BankAddress + (uint)slot * pool.Stride));
        }
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot] with { Payload = raw.AsSpan(12).ToArray().ToImmutableArray() }) };
        ImmutableArray<uint> heads = memory.QuezacotlSecondaryHeads ?? throw new NotSupportedException("Native6C7D10 requires its retained secondary descriptor heads.");
        heads = heads.SetItem(1, pool.Head is int head ? checked(pool.BankAddress + (uint)head * pool.Stride) : 0);
        return new(memory with { Arena = arena, QuezacotlSecondaryHeads = heads }, effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == pool.DescriptorAddress ? pool : existing).ToImmutableArray() }, slot, row);
    }

    public static int RowOffset(uint pointer)
    {
        long offset = (long)pointer - ArenaAddress;
        if (offset < Rows || offset >= Rows + RowSize * RowCount || (offset - Rows) % RowSize != 0)
        {
            throw new NotSupportedException("Native Quezacotl particle pointer leaves its observed256x40 row bank.");
        }
        return (int)offset;
    }

    internal static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int address, short value)
    {
        byte[] raw = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(raw, value);
        return arena.Write(address, raw);
    }

    internal static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int address, uint value)
    {
        byte[] raw = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(raw, value);
        return arena.Write(address, raw);
    }

    internal static void Word(NativeBattleEffectArenaWriter writer, int address, short value)
    {
        Span<byte> raw = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(raw, value);
        writer.Write(address, raw);
    }

    internal static void Dword(NativeBattleEffectArenaWriter writer, int address, uint value)
    {
        Span<byte> raw = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(raw, value);
        writer.Write(address, raw);
    }

    internal static NativeBattleEffectArena WriteVector(NativeBattleEffectArena arena, int address, NativeBattleVector vector)
    {
        byte[] raw = new byte[6];
        PutVector(raw, 0, vector);
        return arena.Write(address, raw);
    }

    private static void PutVector(byte[] raw, int offset, NativeBattleVector vector)
    {
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(offset), vector.X);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(offset + 2), vector.Y);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(offset + 4), vector.Z);
    }
}
