using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlPathStep(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleEffectTaskPool Pool, NativeBattleLogicalOrdering Ordering, int Returned, ImmutableArray<int?> DepthFifo4);

/// <summary>6C7800's four source steps, same-pass recursive producers, row decay, draw and head trimming.</summary>
public static class NativeBattleQuezacotlPathCallbacks
{
    public static NativeBattleQuezacotlPathStep Advance6C7800(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleEffectTaskPool pool, int index, NativeBattleLogicalOrdering ordering, NativeBattleCameraProjection projection, ImmutableArray<int?> incomingDepthFifo4 = default)
    {
        NativeBattleQuezacotlPathResources? resources = effect.Binding.NativeQuezacotlResources?.Paths;
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C7800 requires its retained row and task memory.");
        int address = checked((int)(pool.BankAddress - NativeBattleQuezacotlPathProducer.ArenaAddress) + index * pool.Stride);
        byte[] task = arena.Read(address + 12, 20);
        bool paused = (memory.QuezacotlPause ?? throw new NotSupportedException("Native6C7800 requires its retained pause DWORD.")) != 0;
        if (!paused)
        {
            for (int division = 0; division < 4; division++)
            {
                uint source = T32(0x14);
                if (source == 0)
                {
                    continue;
                }
                NativeBattleQuezacotlPathResources paths = resources ?? throw new NotSupportedException("Native6C7800 requires its authenticated12AFE58 path source bank.");
                short vertex = paths.ReadWord(source);
                Set32(0x14, checked(source + 2));
                HashSet<uint> branches = [];
                while (vertex < 0)
                {
                    if (!branches.Add(source))
                    {
                        throw new InvalidDataException("Native6C7800 path branch source contains a cycle.");
                    }
                    SyncPool();
                    NativeBattleQuezacotlPathSpawn spawned = NativeBattleQuezacotlPathProducer.Spawn6C76E0(memory with { Arena = arena }, effect, paths, T32(0x14), T16(0xE));
                    memory = spawned.Memory;
                    arena = memory.Arena!;
                    effect = spawned.Effect;
                    pool = effect.Pools.Single(existing => existing.DescriptorAddress == pool.DescriptorAddress);
                    source = unchecked(T32(0x14) + (uint)((-1 - vertex) * 2));
                    Set32(0x14, source);
                    vertex = paths.ReadWord(source);
                    Set32(0x14, checked(source + 2));
                }
                uint following = T32(0x14);
                short nextVertex = paths.ReadWord(following);
                if (nextVertex < 0)
                {
                    nextVertex = paths.ReadWord(unchecked(following - (uint)(nextVertex * 2)));
                }
                if (nextVertex == short.MaxValue)
                {
                    if (T16(0xC) <= 3)
                    {
                        return ReleaseAll();
                    }
                    Set32(0x14, 0);
                }
                int width = T16(0xE);
                short variedWidth = unchecked((short)(((Next() * width) >> 15) + width));
                NativeBattleQuezacotlParticleRow allocated = NativeBattleQuezacotlPathProducer.Allocate6C7790(memory with { Arena = arena }, vertex, variedWidth, T16(0x10), T16(0x12));
                memory = allocated.Memory;
                arena = memory.Arena!;
                if (allocated.Row is not int row)
                {
                    return ReleaseAll();
                }
                uint pointer = checked(NativeBattleQuezacotlPathProducer.ArenaAddress + (uint)row);
                int previous = NativeBattleQuezacotlPathProducer.RowOffset(T32(0x1C));
                arena = NativeBattleQuezacotlParticleProducer.Dword(arena, previous + 0x18, pointer);
                Set16(0xC, unchecked((short)(T16(0xC) + 1)));
                Set32(0x1C, pointer);
            }
            foreach (int row in Chain())
            {
                short intensity = R16(row + 0x14);
                if (intensity > 0)
                {
                    intensity = unchecked((short)(intensity + R16(row + 0x16)));
                    arena = NativeBattleQuezacotlParticleProducer.Word(arena, row + 0x14, intensity > 0 ? intensity : (short)0);
                }
            }
        }
        ImmutableArray<int?> fifo = incomingDepthFifo4;
        int count = T16(0xC);
        if (count >= 2)
        {
            NativeBattleActionMatrix captured = memory.QuezacotlCapturedMatrix1780 ?? throw new NotSupportedException("Native6C38C4/6C79A0 requires its retained captured2521780 matrix.");
            ImmutableArray<byte> distance = effect.Binding.NativeQuezacotlResources?.Ribbon?.DistanceBytes16384 ?? throw new NotSupportedException("Native6C7B30 requires its original distance lookup bytes.");
            NativeBattleQuezacotlPathBatch drawn = NativeBattleQuezacotlPathPackets.Emit6C79A0(T32(0x18), count, (pointer, offset, length) => arena.Read(checked(NativeBattleQuezacotlPathProducer.RowOffset(pointer) + offset), length).ToImmutableArray(), ReadVertex, captured, projection, distance, incomingDepthFifo4);
            NativeBattleGfGeometryBatch writes = new(drawn.RowWrites.Select(write => write.Attempt with { ByteOffsetRelative = NativeBattleQuezacotlPathProducer.RowOffset(write.Address) }).ToImmutableArray(), 0);
            NativeBattleEffectPacketWrite stored = NativeBattleEffectPacketArena.Append(memory with { Arena = arena }, 0, ordering, writes);
            NativeBattleQuezacotlPacketWrite emitted = NativeBattleQuezacotlPacketArena.Append(stored.Memory, effect, stored.Ordering, drawn.EffectPackets);
            memory = emitted.Memory;
            arena = memory.Arena!;
            effect = emitted.Effect;
            ordering = emitted.Ordering;
            fifo = drawn.FinalDepthFifo4;
        }
        int returned = 0;
        if (!paused)
        {
            while (true)
            {
                int head = NativeBattleQuezacotlPathProducer.RowOffset(T32(0x18));
                if (R16(head + 0x14) > 32)
                {
                    break;
                }
                uint next = R32(head + 0x18);
                int following = NativeBattleQuezacotlPathProducer.RowOffset(next);
                if (R16(following + 0x14) > 32)
                {
                    break;
                }
                arena = NativeBattleQuezacotlParticleProducer.Dword(arena, NativeBattleQuezacotlPathProducer.CachedRow, T32(0x18));
                arena = NativeBattleQuezacotlParticleProducer.Word(arena, head, -1);
                Set16(0xC, unchecked((short)(T16(0xC) - 1)));
                Set32(0x18, next);
                if (T16(0xC) < 2)
                {
                    returned = 2;
                    break;
                }
            }
        }
        SyncPool();
        return new(memory with { Arena = arena }, effect, pool, ordering, returned, fifo);

        NativeBattleQuezacotlPathStep ReleaseAll()
        {
            arena = NativeBattleQuezacotlParticleProducer.Dword(arena, NativeBattleQuezacotlPathProducer.CachedRow, T32(0x18));
            foreach (int row in Chain())
            {
                arena = NativeBattleQuezacotlParticleProducer.Word(arena, row, -1);
            }
            SyncPool();
            return new(memory with { Arena = arena }, effect, pool, ordering, 2, incomingDepthFifo4);
        }

        NativeBattleVector ReadVertex(short selector)
        {
            NativeBattleQuezacotlExecutableMemory source = memory.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native6C79A0 requires its current primary executable vertex bytes.");
            int offset = checked(8 + selector * 8);
            if (offset < 0 || offset > source.Data.Length - 6)
            {
                throw new NotSupportedException("Native6C79A0 signed vertex index leaves its current executable region.");
            }
            ReadOnlySpan<byte> bytes = source.Data.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
        }

        ushort Next()
        {
            ushort random = effect.Quezacotl!.Random17A0;
            ushort result = NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random);
            effect = effect with { Quezacotl = effect.Quezacotl! with { Random17A0 = random } };
            return result;
        }

        short T16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan(offset - 12));
        uint T32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan(offset - 12));
        short R16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        uint R32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

        void Set16(int offset, short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(task.AsSpan(offset - 12), value);
            arena = NativeBattleQuezacotlParticleProducer.Word(arena, address + offset, value);
        }

        void Set32(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(task.AsSpan(offset - 12), value);
            arena = NativeBattleQuezacotlParticleProducer.Dword(arena, address + offset, value);
        }

        List<int> Chain()
        {
            List<int> rows = [];
            HashSet<uint> visited = [];
            for (uint pointer = T32(0x18); pointer != 0;)
            {
                if (!visited.Add(pointer))
                {
                    throw new InvalidDataException("Native6C7800 retained row chain contains a cycle.");
                }
                int row = NativeBattleQuezacotlPathProducer.RowOffset(pointer);
                rows.Add(row);
                pointer = R32(row + 0x18);
            }
            return rows;
        }

        void SyncPool()
        {
            pool = pool with { Slots = pool.Slots.SetItem(index, pool.Slots[index] with { Payload = task.ToImmutableArray() }) };
            memory = memory with { Arena = arena };
            effect = effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == pool.DescriptorAddress ? pool : existing).ToImmutableArray() };
        }
    }
}
