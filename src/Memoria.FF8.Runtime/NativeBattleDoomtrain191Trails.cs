using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191TrailMemory([property: System.Text.Json.Serialization.JsonRequired] NativeBattleEffectArena Data)
{
    public const int BaseAddress = 0x24FBDC8;
    public const int RowsA = 0x24FBEA8 - BaseAddress;
    public const int RowsB = 0x24FC020 - BaseAddress;
    public const int GatherA = 0x24FBDC8 - BaseAddress;
    public const int GatherB = 0x24FBE10 - BaseAddress;
    public const int CurveA = 0x24FC100 - BaseAddress;
    public const int CurveB = 0x24FC1C0 - BaseAddress;
    private static readonly (int Offset, int Length)[] Ranges = [(RowsA, 40), (RowsB, 40), (GatherA, 16), (GatherB, 16), (CurveA, 64), (CurveB, 64)];
    public static NativeBattleDoomtrain191TrailMemory FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleDoomtrain191TrailMemory Unknown()
    {
        NativeBattleEffectArena memory = NativeBattleEffectArena.Clear();
        foreach (var range in Ranges)
        {
            memory = memory.MarkUnknown(range.Offset, range.Length);
        }
        return new(memory);
    }
    public void Validate()
    {
        Data.Validate();
        foreach (var region in Data.EnumerateDataRanges())
        {
            Require(region.Offset, region.Length);
        }
        foreach (var region in Data.EnumerateUnknownRanges())
        {
            Require(region.Offset, region.Length);
        }
        static void Require(int offset, int length)
        {
            if (!Ranges.Any(range => offset >= range.Offset && offset <= range.Offset + range.Length - length))
            {
                throw new InvalidDataException("Native Doomtrain trail memory leaves its disjoint physical spans.");
            }
        }
    }
}

public static class NativeBattleDoomtrain191Trails
{
    public static NativeBattleDoomtrain191Frame Spawn(NativeBattleDoomtrain191Frame frame, uint constructor)
    {
        (uint Callback, ushort Duration, ushort Threshold, ushort Scale, ushort Spread, ushort Horizontal, ushort Vertical, bool Wheel) profile = constructor switch
        {
            0x643FD0 => (0x644030, 34, 15, 0x580, 0x380, 50, 120, false),
            0x644490 => (0x644030, 20, 0, 0x680, 0x480, 50, 120, false),
            0x6444E0 => (0x644530, 99, 0, 0x198, 0x168, 10, 15, false),
            0x6448F0 => (0x644940, 0, 0, 0x1200, 0xB80, 150, 250, false),
            0x644E10 => (0x644E70, 34, 10, 0x480, 0x440, 50, 50, true),
            0x6454A0 => (0x644E70, 20, 0, 0x480, 0x440, 50, 50, true),
            0x6454F0 => (0x645540, 99, 0, 0x168, 0x148, 8, 8, true),
            0x645A90 => (0x645AE0, 0, 0, 0xC00, 0x580, 150, 150, true),
            _ => throw new NotSupportedException($"Native Doomtrain trail constructor {constructor:X8} requires its complete original profile.")
        };
        bool wheel = profile.Wheel;
        NativeBattleEffectTaskPool pool = Pool(frame);
        (pool, int slot) = pool.Append(profile.Callback);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 0).WriteWord(0x1C, profile.Scale).WriteWord(0x1E, profile.Spread).WriteWord(0x20, profile.Horizontal).WriteWord(0x22, profile.Vertical);
        if (constructor is not (0x6448F0 or 0x645A90))
        {
            task = task.WriteWord(0xE, profile.Duration);
        }
        if (constructor is 0x643FD0 or 0x644490 or 0x644E10 or 0x6454A0)
        {
            task = task.WriteWord(0x12, profile.Threshold);
        }
        var state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain trails require their retained module state.");
        NativeBattleDoomtrain191TrailMemory memory = state.Trails ?? NativeBattleDoomtrain191TrailMemory.Unknown();
        int rows = wheel ? NativeBattleDoomtrain191TrailMemory.RowsA : NativeBattleDoomtrain191TrailMemory.RowsB;
        memory = memory with { Data = memory.Data.Write(rows, new byte[4]).Write(rows + 20, new byte[4]) };
        return PutPool(frame with { Memory = frame.Memory with { Doomtrain191 = state with { Trails = memory } } }, pool.WriteSlot(slot, task));
    }

    public static NativeBattleDoomtrain191LeafStep Advance(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleModuleResource sprite, NativeBattleDoomtrain191SteamServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool wheel = task.Callback is 0x644E70 or 0x645540 or 0x645AE0;
        bool small = task.Callback is 0x644530 or 0x645540;
        bool large = task.Callback is 0x644940 or 0x645AE0;
        bool spawnFirst = small || large;
        if (!wheel && task.Callback is not (0x644030 or 0x644530 or 0x644940))
        {
            throw new InvalidDataException("Native Doomtrain trails require their actual drawing task.");
        }
        var state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain trails require their module state.");
        NativeBattleDoomtrain191TrailMemory memory = state.Trails ?? throw new NotSupportedException("Native Doomtrain trails require their actual row initialization.");
        NativeBattleEffectArena globals = memory.Data;
        NativeBattleActionMatrix anchor = NativeBattleDoomtrain191Model.Require(frame).Anchor.ReadMatrix();
        int rows = wheel ? NativeBattleDoomtrain191TrailMemory.RowsA : NativeBattleDoomtrain191TrailMemory.RowsB;
        int age = unchecked((short)task.ReadWord(0xC));
        int ring = age % 2;
        int row = checked(rows + 20 * ring);
        globals = globals.Write(row, BitConverter.GetBytes(1u));
        globals = PublishPoint(globals, row + 4, wheel ? new(1200, 530, -8500) : new(0, -1780, -9300));
        if (wheel)
        {
            globals = PublishPoint(globals, row + 12, new(-1200, 530, -8500));
        }
        int count = 0;
        for (int index = ring; count < 2 && BinaryPrimitives.ReadUInt32LittleEndian(globals.Read(rows + 20 * index, 4)) != 0; index = index == 0 ? 1 : 0)
        {
            globals = Copy(globals, rows + 20 * index + 4, NativeBattleDoomtrain191TrailMemory.GatherA + count * 8);
            if (wheel)
            {
                globals = Copy(globals, rows + 20 * index + 12, NativeBattleDoomtrain191TrailMemory.GatherB + count * 8);
            }
            count++;
        }
        frame = PublishGlobals(frame);
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain trails require their current particle bank.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena arena = owned?.View ?? source;
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        int first = checked((int)(state.WorkPointers[wheel ? 3 : 2] - state.ArenaAddress));
        int capacity = wheel ? 120 : 150;
        bool paused = (services.ReadFlags() & 1) != 0;
        NativeBattleActionMatrix camera = state.BaseView.ReadMatrix();
        NativeBattleScriptedLightingMemory lighting = frame.Memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        frame = frame with { Memory = frame.Memory with { ScriptedLighting = lighting with { BackColor = new(camera.X, camera.Y, camera.Z) } } };
        int survivors = 0;
        int spawnCount = small ? 4 : large ? age >= 22 && age < 42 ? 2 : 5 : age < unchecked((short)task.ReadWord(0x12)) ? wheel ? 2 : 1 : 7;
        if (spawnFirst && (!paused || small && wheel))
        {
            SpawnCurrent();
            frame = PublishGlobals(frame);
        }
        for (int index = 0; index < capacity; index++)
        {
            int record = first + index * 24;
            if ((arena.Read(record, 1)[0] & 1) == 0)
            {
                continue;
            }
            ushort phase = unchecked((ushort)(ParticleWord(record + 4) * (wheel ? 2 : 1)));
            NativeBattleModuleSpriteFrame selected = sprite.ProjectedFrame571C80(phase);
            NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(camera, new(ParticleWord(record + 8), ParticleWord(record + 10), ParticleWord(record + 12)));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(mac.X, mac.Y, mac.Z);
            short size = ParticleWord(record + 6);
            int offset = -(size >> (small || large && wheel ? 2 : wheel ? 3 : 4));
            NativeBattleActionMatrix matrix = new([size, 0, 0, 0, size, 0, 0, 0, size], unchecked(mac.X + (unchecked(offset * direction.X) >> 12)), unchecked(mac.Y + (unchecked(offset * direction.Y) >> 12)), unchecked(mac.Z + (unchecked(offset * direction.Z) >> 12)), 4096);
            frame = services.Draw(frame, new(matrix, phase) { ResourceAddress = 0xE342A4, Flags = wheel ? (ushort)8 : (ushort)12, Color = wheel ? 0x808080u : 0x586880u });
            if (!paused)
            {
                Store(record + 4, ParticleWord(record + 4) + 1);
                if (selected.NextOffset < 0)
                {
                    arena = arena.Write(record, new byte[4]);
                }
                else if (!small && wheel)
                {
                    Store(record + 8, ParticleWord(record + 8) + ParticleWord(record + 16));
                    Store(record + 10, ParticleWord(record + 10) + ParticleWord(record + 18));
                    Store(record + 16, ParticleWord(record + 16) - (ParticleWord(record + 16) >> 1));
                    Store(record + 18, ParticleWord(record + 18) - (ParticleWord(record + 18) >> 1));
                    if (large)
                    {
                        Store(record + 12, ParticleWord(record + 12) + ParticleWord(record + 20));
                        Store(record + 20, ParticleWord(record + 20) + (ParticleWord(record + 20) >> 2));
                        survivors++;
                    }
                }
                else if (!small)
                {
                    Store(record + 6, size + (size >> 5));
                    Store(record + 10, ParticleWord(record + 10) + ParticleWord(record + 18));
                    Store(record + 18, ParticleWord(record + 18) - (ParticleWord(record + 18) >> 1));
                    if (large)
                    {
                        Store(record + 12, ParticleWord(record + 12) + ParticleWord(record + 20));
                        Store(record + 20, ParticleWord(record + 20) + (ParticleWord(record + 20) >> 2));
                        survivors++;
                    }
                }
            }
        }
        if (paused)
        {
            return new(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, 0);
        }
        if (!spawnFirst)
        {
            SpawnCurrent();
        }
        ushort next = unchecked((ushort)(age + 1));
        int result = 0;
        if (large ? unchecked((short)next) >= 59 && survivors == 0 : unchecked((short)next) >= unchecked((short)task.ReadWord(0xE)))
        {
            for (int index = 0; index < capacity; index++)
            {
                int record = first + index * 24;
                if ((arena.Read(record, 1)[0] & 1) != 0)
                {
                    arena = arena.Write(record, new byte[4]);
                }
            }
            result = 2;
        }
        frame = PublishGlobals(frame);
        pool = Pool(frame);
        frame = PutPool(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, pool.WriteSlot(slot, pool.Slots[slot].WriteWord(0xC, next)));
        return new(frame, result);

        void SpawnCurrent()
        {
            if (count <= 1 || age >= (large ? 59 : unchecked((short)task.ReadWord(0xE))))
            {
                return;
            }
            Sample(NativeBattleDoomtrain191TrailMemory.GatherA, NativeBattleDoomtrain191TrailMemory.CurveA);
            if (wheel)
            {
                Sample(NativeBattleDoomtrain191TrailMemory.GatherB, NativeBattleDoomtrain191TrailMemory.CurveB);
            }
            for (int index = 0; index < spawnCount; index++)
            {
                int available = Free();
                if (available < 0)
                {
                    break;
                }
                SpawnParticle(available, NativeBattleDoomtrain191TrailMemory.CurveA + index * 8, false);
                if (wheel)
                {
                    SpawnParticle(available + 24, NativeBattleDoomtrain191TrailMemory.CurveB + index * 8, true);
                }
            }
        }

        NativeBattleEffectArena PublishPoint(NativeBattleEffectArena current, int destination, NativeBattleVector input)
        {
            NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(anchor, input);
            byte[] bytes = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)(point.X + anchor.X)));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), unchecked((short)(point.Y + anchor.Y)));
            short z = unchecked((short)(point.Z + anchor.Z));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), large ? Math.Max(z, (short)-25000) : z);
            return current.Write(destination, bytes).MarkUnknown(destination + 6, 2);
        }
        static NativeBattleEffectArena Copy(NativeBattleEffectArena current, int from, int to)
        {
            var snapshot = current.ReadSnapshot(from, 8);
            current = current.Write(to, snapshot.Data.AsSpan());
            for (int index = 0; index < 8; index++)
            {
                if (!snapshot.Known[index])
                {
                    current = current.MarkUnknown(to + index, 1);
                }
            }
            return current;
        }
        NativeBattleDoomtrain191Frame PublishGlobals(NativeBattleDoomtrain191Frame current) => current with { Memory = current.Memory with { Doomtrain191 = current.Memory.Doomtrain191! with { Trails = memory with { Data = globals } } } };
        short ParticleWord(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        void Store(int offset, int value) => arena = arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
        int Random()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
        int Free()
        {
            for (int index = 0; index < capacity; index++)
            {
                int record = first + 24 * index;
                if (BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(record, 4)) == 0)
                {
                    return record;
                }
            }
            return -1;
        }
        void Sample(int gather, int curve)
        {
            NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, globals, gather, NativeBattleEffectArena.Clear());
            int samples = small ? 5 : large ? 6 : 8;
            for (int index = 0; index < samples; index++)
            {
                globals = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, index * 4096 / (samples - 1), globals, curve + index * 8);
            }
        }
        void SpawnParticle(int record, int point, bool second)
        {
            arena = arena.Write(record, BitConverter.GetBytes(1u));
            Store(record + 4, 0);
            Store(record + 6, Random() % unchecked((short)task.ReadWord(0x1E)) + unchecked((short)task.ReadWord(0x1C)));
            var selected = globals.ReadSnapshot(point, 8);
            arena = arena.Write(record + 8, selected.Data.AsSpan());
            for (int index = 0; index < 8; index++)
            {
                if (!selected.Known[index])
                {
                    arena = arena.MarkUnknown(record + 8 + index, 1);
                }
            }
            int horizontal = unchecked((short)task.ReadWord(0x20));
            int vertical = unchecked((short)task.ReadWord(0x22));
            Store(record + 8, ParticleWord(record + 8) + Random() % (2 * horizontal) - horizontal);
            Store(record + 10, wheel ? ParticleWord(record + 10) + Random() % (2 * vertical) - vertical : ParticleWord(record + 10) - Random() % vertical);
            Store(record + 12, ParticleWord(record + 12) + Random() % (2 * horizontal) - horizontal);
            if (!small)
            {
                if (wheel)
                {
                    int initial = large ? 160 : 70;
                    int range = large ? 235 : 55;
                    Store(record + 16, second ? -initial - Random() % range : initial + Random() % range);
                    Store(record + 18, initial + Random() % range);
                }
                else
                {
                    int velocity = large ? -160 - Random() % 300 : -65 - Random() % 130;
                    Store(record + 18, large && spawnCount == 2 ? unchecked((short)(velocity * 2)) : velocity);
                }
                if (large)
                {
                    Store(record + 20, age < 42 ? 500 : 0);
                }
            }
        }
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
