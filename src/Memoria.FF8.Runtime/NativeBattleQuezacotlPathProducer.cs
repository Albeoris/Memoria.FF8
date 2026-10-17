using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlPathSpawn(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, int? Slot, int? Row);

public static class NativeBattleQuezacotlPathProducer
{
    public const int Rows = 0x40200;
    public const int RowSize = 0x1C;
    public const int RowCount = 512;
    public const int CachedRow = 0x43A00;
    public const uint Descriptor = 0x2521708;
    public const uint BankAddress = 0x211ECB8;
    public const uint ArenaAddress = NativeBattleQuezacotlParticleProducer.ArenaAddress;

    public static NativeBattleQuezacotlPathSpawn SpawnFour6C45AA(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        int paused = memory.QuezacotlPause ?? throw new NotSupportedException("Native6C45A3 requires its retained pause DWORD.");
        if (paused != 0)
        {
            return new(memory, effect, null, null);
        }
        NativeBattleQuezacotlPathResources resources = effect.Binding.NativeQuezacotlResources?.Paths ?? throw new NotSupportedException("Native6C76C0 requires its authenticated12AFE58 path bank; explicitly migrate this standalone's resource metadata.");
        NativeBattleQuezacotlPathSpawn result = new(memory, effect, null, null);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            ushort random = effect.Quezacotl?.Random17A0 ?? throw new NotSupportedException("Native6C45AA requires its retained private random seed.");
            int value = NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random);
            effect = effect with { Quezacotl = effect.Quezacotl! with { Random17A0 = random } };
            int index = value * resources.ReadWord(resources.Address) >> 15;
            result = Spawn6C76C0(memory, effect, resources, index);
            memory = result.Memory;
            effect = result.Effect;
        }
        return result;
    }

    public static NativeBattleQuezacotlPathSpawn Spawn6C76C0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleQuezacotlPathResources resources, int index)
    {
        int count = resources.ReadWord(resources.Address);
        if (index < 0 || index >= count)
        {
            throw new InvalidDataException("Native6C76C0 path index leaves its actual signed-WORD directory.");
        }
        short offset = resources.ReadWord(checked(resources.Address + 2u + (uint)index * 2));
        return Spawn6C76E0(memory, effect, resources, unchecked(resources.Address + (uint)(offset * 2)), 260);
    }

    public static NativeBattleQuezacotlPathSpawn Spawn6C76E0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleQuezacotlPathResources resources, uint source, short width)
    {
        ushort random = effect.Quezacotl?.Random17A0 ?? throw new NotSupportedException("Native6C76E0 requires its current private random seed.");
        int first = NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random) & 1023;
        short intensity = checked((short)(first + (NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random) & 511) + 256));
        short decay = unchecked((short)-(intensity >> 3));
        effect = effect with { Quezacotl = effect.Quezacotl! with { Random17A0 = random } };
        NativeBattleQuezacotlParticleRow allocated = Allocate6C7790(memory, resources.ReadWord(source), width, intensity, decay);
        memory = allocated.Memory;
        if (allocated.Row is not int row)
        {
            return new(memory, effect, null, null);
        }
        NativeBattleEffectTaskPool pool = effect.Pools.Single(existing => existing.DescriptorAddress == Descriptor);
        pool.Validate();
        if (pool.BankAddress != BankAddress || pool.Stride != 0x20 || pool.Slots.Length != 128)
        {
            throw new InvalidDataException("Native6C76E0 lost its original128x20 path task bank.");
        }
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            //6C773F returns without releasing the already allocated row.
            return new(memory, effect, null, row);
        }
        int? tail = pool.Tail;
        (pool, int index) = pool.Append(0x6C7800);
        byte[] raw = new byte[0x20];
        BinaryPrimitives.WriteUInt16LittleEndian(raw, pool.Slots[index].Flags);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), 0x6C7800);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0xC), 1);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0xE), width);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x10), intensity);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x12), decay);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x14), checked(source + 2));
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x18), checked(ArenaAddress + (uint)row));
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x1C), checked(ArenaAddress + (uint)row));
        NativeBattleEffectArena arena = memory.Arena!.Write(checked((int)(BankAddress - ArenaAddress) + index * 0x20), raw);
        if (tail is int previous)
        {
            arena = NativeBattleQuezacotlParticleProducer.Dword(arena, checked((int)(BankAddress - ArenaAddress) + previous * 0x20 + 4), checked(BankAddress + (uint)index * 0x20));
        }
        pool = pool with { Slots = pool.Slots.SetItem(index, pool.Slots[index] with { Payload = raw.AsSpan(12).ToArray().ToImmutableArray() }) };
        ImmutableArray<uint> heads = memory.QuezacotlSecondaryHeads ?? throw new NotSupportedException("Native6C76E0 requires its retained secondary descriptor heads.");
        heads = heads.SetItem(2, pool.Head is int head ? checked(BankAddress + (uint)head * 0x20) : 0);
        return new(memory with { Arena = arena, QuezacotlSecondaryHeads = heads }, effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == Descriptor ? pool : existing).ToImmutableArray() }, index, row);
    }

    public static NativeBattleQuezacotlParticleRow Allocate6C7790(NativeBattleEffectMemory memory, short vertex, short width, short intensity, short decay)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C7790 requires its current phase86 row bank.");
        uint cached = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(CachedRow, 4));
        int row;
        if (cached != 0)
        {
            row = RowOffset(cached);
            arena = NativeBattleQuezacotlParticleProducer.Dword(arena, CachedRow, 0);
        }
        else
        {
            row = -1;
            for (int index = 0; index < RowCount; index++)
            {
                int candidate = Rows + index * RowSize;
                if (BinaryPrimitives.ReadInt16LittleEndian(arena.Read(candidate, 2)) == -1)
                {
                    row = candidate;
                    break;
                }
            }
            if (row < 0)
            {
                return new(memory, null);
            }
        }
        arena = NativeBattleQuezacotlParticleProducer.Word(arena, row, vertex);
        arena = NativeBattleQuezacotlParticleProducer.Word(arena, row + 2, width);
        arena = NativeBattleQuezacotlParticleProducer.Word(arena, row + 0x14, intensity);
        arena = NativeBattleQuezacotlParticleProducer.Word(arena, row + 0x16, decay);
        arena = NativeBattleQuezacotlParticleProducer.Dword(arena, row + 0x18, 0);
        return new(memory with { Arena = arena }, row);
    }

    public static int RowOffset(uint pointer)
    {
        long offset = (long)pointer - ArenaAddress;
        if (offset < Rows || offset >= Rows + RowSize * RowCount || (offset - Rows) % RowSize != 0)
        {
            throw new NotSupportedException("Native6C7800 row pointer leaves its retained512x1C bank.");
        }
        return (int)offset;
    }
}
