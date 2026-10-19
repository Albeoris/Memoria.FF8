using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlParticleStep(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleEffectTaskPool Pool, NativeBattleLogicalOrdering Ordering, int Returned);

/// <summary>6C7EA0's eight native subdivisions, linked trail rows and same-pass child task appends.</summary>
public static class NativeBattleQuezacotlParticleCallbacks
{
    public static NativeBattleQuezacotlParticleStep Advance6C7EA0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleEffectTaskPool pool, int index, NativeBattleLogicalOrdering ordering, NativeBattleCameraProjection projection, NativeBattleGteDepthTrace? depthTrace = null)
    {
        NativeBattleQuezacotlRibbonResources resources = effect.Binding.NativeQuezacotlResources?.Ribbon ?? throw new NotSupportedException("Native6C7EA0 requires its actual immutable particle and trail resources.");
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C7EA0 requires its current linked row bank.");
        int address = checked((int)(pool.BankAddress - NativeBattleQuezacotlParticleProducer.ArenaAddress) + index * pool.Stride);
        byte[] task = arena.Read(address + 12, pool.Stride - 12);
        bool taskDirty = false;
        bool paused = (memory.QuezacotlPause ?? throw new NotSupportedException("Native6C7EA0 requires its retained pause owner.")) != 0;
        if (!paused)
        {
            for (int division = 0; division < 8; division++)
            {
                short progress = T16(0x1E);
                if (progress >= 4096)
                {
                    continue;
                }
                NativeBattleVector position = Add(TVec(0x18), TVec(0x28));
                SetVec(0x18, position);
                progress = unchecked((short)(progress + T16(0x26)));
                Set16(0x1E, progress);
                SetVec(0x28, Add(TVec(0x28), new(Noise(127, 64), Noise(127, 64), Noise(127, 64))));
                if (Next() < 4096)
                {
                    position = Add(position, new(Noise(511, 256), Noise(511, 256), Noise(511, 256)));
                    SetVec(0x18, position);
                }
                NativeBattleVector endpoint;
                if (progress >= 4096)
                {
                    Set16(0x1E, 4096);
                    progress = 4096;
                    endpoint = TVec(0x20);
                    if (T16(0x3A) != 0 && R32(0x28E50) != 0)
                    {
                        throw new NotSupportedException("Native6C7EA0 enabled impact completion requires its reached6C8430/secondary-spawn contract.");
                    }
                }
                else
                {
                    endpoint = NativeBattleQuezacotlRibbonMath.Blend(position, TVec(0x20), 4096 - progress, progress);
                }
                NativeBattleQuezacotlParticleRow allocated = NativeBattleQuezacotlParticleProducer.Allocate6C7E30(memory with { Arena = arena }, endpoint, T16(0x38));
                memory = allocated.Memory;
                arena = memory.Arena!;
                if (allocated.Row is not int row)
                {
                    Set16(0x1E, 4096);
                    continue;
                }
                uint rowPointer = checked(NativeBattleQuezacotlParticleProducer.ArenaAddress + (uint)row);
                int previous = NativeBattleQuezacotlParticleProducer.RowOffset(T32(0x14));
                arena = NativeBattleQuezacotlParticleProducer.Dword(arena, previous + 0x24, rowPointer);
                Set32(0x14, rowPointer);
                Set16(0xE, unchecked((short)(T16(0xE) + 1)));
                ImmutableArray<short> scale = resources.ParticleScaleWords4096 ?? throw new NotSupportedException("Native6C7EA0 requires authenticated12B4770 velocity WORDs; explicitly migrate this standalone's immutable particle metadata.");
                if (scale.IsDefault || scale.Length != 4096)
                {
                    throw new InvalidDataException("Native6C7EA0 velocity lookup does not contain its original4096 WORDs.");
                }
                NativeBattleVector velocity = NativeBattleQuezacotlRibbonMath.Scale(TVec(0x30), scale[(progress >> 1) & 4095]);
                arena = NativeBattleQuezacotlParticleProducer.WriteVector(arena, row + 8, velocity);
                if (progress < 3072 && Next() < unchecked(T16(0x2E) << 13))
                {
                    NativeBattleVector target;
                    if (T16(0x3A) != 0)
                    {
                        NativeBattleVector basis = TVec(0x20);
                        target = new(unchecked((short)(basis.X + Noise(4095, 2048))), unchecked((short)(basis.Y - (Next() & 2047) - 256)), unchecked((short)(basis.Z - (Next() & 511) - 256)));
                    }
                    else
                    {
                        short Offset(short basis)
                        {
                            bool negative = Next() > 16384;
                            int distance = 512 + (Next() & 511);
                            return unchecked((short)(basis + (negative ? -distance : distance)));
                        }
                        target = new(Offset(endpoint.X), Offset(endpoint.Y), Offset(endpoint.Z));
                    }
                    NativeBattleVector drift = Add(TVec(0x28), new(Noise(511, 256), Noise(511, 256), Noise(511, 256)));
                    short childMode = T16(0x3A) == 1 ? (short)2 : (short)0;
                    SyncPool();
                    NativeBattleQuezacotlParticleArguments arguments = new(drift, endpoint, target, TVec(0x30), -1, progress, 512, unchecked((short)(T16(0x38) >> 1)), childMode, 0);
                    NativeBattleQuezacotlParticleSpawn child = NativeBattleQuezacotlParticleProducer.Spawn6C7D10(memory with { Arena = arena }, effect, arguments);
                    memory = child.Memory;
                    arena = memory.Arena!;
                    effect = child.Effect;
                    pool = effect.Pools.Single(existing => existing.DescriptorAddress == pool.DescriptorAddress);
                    if (child.Row is int childRow)
                    {
                        arena = arena.Write(childRow + 8, arena.Read(row, 8));
                    }
                    Set16(0x2E, unchecked((short)(T16(0x2E) - 1)));
                }
            }
            NativeBattleEffectArenaWriter rowUpdates = new(arena);
            foreach (int row in Chain())
            {
                short width = R16(row + 0x20);
                if (width <= 0)
                {
                    continue;
                }
                short acceleration = R16(row + 0x22);
                width = unchecked((short)(width + acceleration));
                NativeBattleQuezacotlParticleProducer.Word(rowUpdates, row + 0x20, Math.Max(width, (short)0));
                if (width <= 0)
                {
                    continue;
                }
                short wait = R16(row + 0xE);
                if (wait != 0 && acceleration <= 0)
                {
                    NativeBattleQuezacotlParticleProducer.Word(rowUpdates, row + 0xE, unchecked((short)(wait - 1)));
                }
                else if (acceleration > -384)
                {
                    NativeBattleQuezacotlParticleProducer.Word(rowUpdates, row + 0x22, unchecked((short)(acceleration - 320)));
                }
            }
            arena = rowUpdates.Freeze();
        }
        List<int> chain = Chain();
        NativeBattleGteDepthTrace trace = depthTrace ?? new();
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native6C7EA0 requires its prior current view matrix.");
        NativeBattleEffectArenaWriter projectedRows = new(arena);
        foreach (int row in chain)
        {
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(view, RVec(row), projection);
            NativeBattleQuezacotlParticleProducer.Dword(projectedRows, row + 0x10, projected.Screen);
            NativeBattleQuezacotlParticleProducer.Dword(projectedRows, row + 0x14, (uint)(projected.Depth >> 2));
            trace.Push(projected.Depth);
        }
        arena = projectedRows.Freeze();
        int count = T16(0xE);
        if (count >= 2)
        {
            ImmutableArray<int?> actualDepths = trace.DepthFifo4;
            ImmutableArray<int> fifo = actualDepths.All(depth => depth.HasValue) ? actualDepths.Select(depth => depth!.Value).ToImmutableArray() : [];
            NativeBattleQuezacotlTrailBatch trail = NativeBattleQuezacotlTrailPackets.Emit6C84C0(T32(0x10), count, (pointer, offset, length) => arena.Read(checked(NativeBattleQuezacotlParticleProducer.RowOffset(pointer) + offset), length).ToImmutableArray(), resources.DistanceBytes16384, fifo);
            NativeBattleGfGeometryBatch rowStores = new(trail.RowWrites.Select(write => write.Attempt with { ByteOffsetRelative = NativeBattleQuezacotlParticleProducer.RowOffset(write.Address) }).ToImmutableArray(), 0);
            NativeBattleEffectPacketWrite stored = NativeBattleEffectPacketArena.Append(memory with { Arena = arena }, 0, ordering, rowStores);
            NativeBattleQuezacotlPacketWrite drawn = NativeBattleQuezacotlPacketArena.Append(stored.Memory, effect, stored.Ordering, trail.EffectPackets);
            memory = drawn.Memory;
            arena = memory.Arena!;
            effect = drawn.Effect;
            ordering = drawn.Ordering;
        }
        int returned = 0;
        if (!paused)
        {
            foreach (int row in Chain())
            {
                arena = NativeBattleQuezacotlParticleProducer.WriteVector(arena, row, Add(RVec(row), RVec(row + 8)));
            }
            int head = NativeBattleQuezacotlParticleProducer.RowOffset(T32(0x10));
            while (R16(head + 0x20) == 0)
            {
                uint next = R32(head + 0x24);
                if (next == 0)
                {
                    break;
                }
                int following = NativeBattleQuezacotlParticleProducer.RowOffset(next);
                if (R16(following + 0x20) != 0)
                {
                    break;
                }
                arena = NativeBattleQuezacotlParticleProducer.Dword(arena, NativeBattleQuezacotlParticleProducer.CachedRow, checked(NativeBattleQuezacotlParticleProducer.ArenaAddress + (uint)head));
                arena = NativeBattleQuezacotlParticleProducer.Word(arena, head + 6, -1);
                Set32(0x10, next);
                Set16(0xE, unchecked((short)(T16(0xE) - 1)));
                head = following;
                if (T16(0xE) < 2)
                {
                    returned = 2;
                    break;
                }
            }
        }
        SyncPool();
        return new(memory with { Arena = arena }, effect, pool, ordering, returned);

        ushort Next()
        {
            ushort random = effect.Quezacotl!.Random17A0;
            ushort value = NativeBattleQuezacotlPrivateRandom.Draw6C7CF0(ref random);
            effect = effect with { Quezacotl = effect.Quezacotl! with { Random17A0 = random } };
            return value;
        }

        short Noise(int mask, int bias) => unchecked((short)((Next() & mask) - bias));
        short T16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan(offset - 12));
        uint T32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan(offset - 12));
        short R16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        uint R32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
        NativeBattleVector TVec(int offset) => new(T16(offset), T16(offset + 2), T16(offset + 4));
        NativeBattleVector RVec(int offset) => new(R16(offset), R16(offset + 2), R16(offset + 4));

        void Set16(int offset, short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(task.AsSpan(offset - 12), value);
            taskDirty = true;
        }

        void Set32(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(task.AsSpan(offset - 12), value);
            taskDirty = true;
        }

        void SetVec(int offset, NativeBattleVector value)
        {
            Set16(offset, value.X);
            Set16(offset + 2, value.Y);
            Set16(offset + 4, value.Z);
        }

        List<int> Chain()
        {
            List<int> rows = [];
            HashSet<uint> seen = [];
            for (uint next = T32(0x10); next != 0;)
            {
                if (!seen.Add(next) || seen.Count > 256)
                {
                    throw new InvalidDataException("Native6C7EA0 trail links cycle or leave their original256-row bank.");
                }
                int row = NativeBattleQuezacotlParticleProducer.RowOffset(next);
                rows.Add(row);
                next = R32(row + 0x24);
            }
            return rows;
        }

        void SyncPool()
        {
            if (taskDirty)
            {
                arena = arena.Write(address + 12, task);
                taskDirty = false;
            }
            pool = pool with { Slots = pool.Slots.SetItem(index, pool.Slots[index] with { Payload = task.ToImmutableArray() }) };
            effect = effect with { Pools = effect.Pools.Select(existing => existing.DescriptorAddress == pool.DescriptorAddress ? pool : existing).ToImmutableArray() };
            memory = memory with { Arena = arena };
        }
    }

    private static NativeBattleVector Add(NativeBattleVector first, NativeBattleVector second) => new(unchecked((short)(first.X + second.X)), unchecked((short)(first.Y + second.Y)), unchecked((short)(first.Z + second.Z)));
}
