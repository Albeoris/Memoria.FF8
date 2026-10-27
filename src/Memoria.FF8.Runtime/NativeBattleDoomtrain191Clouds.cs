using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDoomtrain191Clouds
{
    public static NativeBattleDoomtrain191Frame Spawn(NativeBattleDoomtrain191Frame frame, uint constructor)
    {
        (ushort Duration, ushort Y, ushort Count, ushort Width, ushort Velocity, ushort Scale, ushort Spacing) profile = constructor switch
        {
            0x6432F0 => (19, 0xFF38, 30, 900, 0, 2048, 0xF830),
            0x643560 => (42, 0xFE70, 20, 2000, 800, 5120, 0xF448),
            _ => throw new NotSupportedException($"Native Doomtrain cloud constructor {constructor:X8} requires its original profile.")
        };
        NativeBattleEffectTaskPool pool = Pool(frame);
        (pool, int slot) = pool.Append(0x643340);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0xC, 0).WriteWord(0xE, profile.Duration).WriteWord(0x10, 0).WriteWord(0x12, profile.Y).WriteWord(0x14, 30000).WriteWord(0x16, profile.Count).WriteWord(0x1C, profile.Width).WriteWord(0x1E, profile.Velocity).WriteWord(0x20, profile.Scale).WriteWord(0x22, profile.Spacing);
        return PutPool(frame, pool.WriteSlot(slot, task));
    }

    public static NativeBattleDoomtrain191LeafStep Advance(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleModuleResource sprite, NativeBattleDoomtrain191SteamServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool final = task.Callback == 0x6435B0;
        if (!final && task.Callback != 0x643340)
        {
            throw new InvalidDataException("Native Doomtrain clouds require their actual task.");
        }
        var state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain clouds require their retained work pointer.");
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain clouds require their actual particle bank.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena arena = owned?.View ?? source;
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        int first = checked((int)(state.WorkPointers[0] - state.ArenaAddress));
        bool paused = (services.ReadFlags() & 1) != 0;
        NativeBattleActionMatrix camera = state.BaseView.ReadMatrix();
        var lighting = frame.Memory.ScriptedLighting ?? NativeBattleScriptedLightingMemory.Unknown();
        frame = frame with { Memory = frame.Memory with { ScriptedLighting = lighting with { BackColor = new(camera.X, camera.Y, camera.Z) } } };
        int age = unchecked((short)task.ReadWord(0xC));
        ushort flags = final && age < 11 ? (ushort)0x208 : (ushort)8;
        uint color = 0x808080;
        if (final && age >= 41)
        {
            uint channel = unchecked((uint)((49 - age) << 4));
            flags |= 4;
            color = channel | (channel << 8) | (channel << 16);
        }
        for (int index = 0; index < 170; index++)
        {
            int record = first + index * 24;
            if ((arena.Read(record, 1)[0] & 1) == 0)
            {
                continue;
            }
            ushort phase = unchecked((ushort)Word(record + 4));
            short size = Word(record + 6);
            NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(camera, new(Word(record + 8), Word(record + 10), Word(record + 12)));
            frame = services.Draw(frame, new(new([size, 0, 0, 0, size, 0, 0, 0, size], mac.X, mac.Y, mac.Z, 4096), phase) { Flags = flags, Color = color });
            if (!paused)
            {
                Store(record + 4, phase + 1);
                if (sprite.ProjectedFrame571C80(phase).NextOffset < 0)
                {
                    Store(record + 4, 0);
                }
                else if (!final)
                {
                    Store(record + 12, Word(record + 12) + unchecked((short)task.ReadWord(0x1E)));
                }
            }
        }
        if (paused)
        {
            return new(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, 0);
        }
        if (task.ReadWord(0xC) == 0)
        {
            if (final)
            {
                task = task.WriteWord(0x10, 0).WriteWord(0x12, 0xFE70).WriteWord(0x14, 30000);
            }
            int count = final ? 20 : unchecked((short)task.ReadWord(0x16));
            for (int index = 0; index < count; index++)
            {
                int free = 0;
                while (free < 170 && BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(first + free * 24, 4)) != 0)
                {
                    free++;
                }
                if (free == 170)
                {
                    break;
                }
                int width = final ? 2000 : unchecked((short)task.ReadWord(0x1C));
                short size = final ? (short)5120 : unchecked((short)task.ReadWord(0x20));
                Seed(first + free * 24, -width, size);
                Seed(first + (free + 1) * 24, width, size);
                task = task.WriteWord(0x14, unchecked((ushort)(task.ReadWord(0x14) + (final ? -3000 : unchecked((short)task.ReadWord(0x22))))));
            }
        }
        ushort next = unchecked((ushort)(age + 1));
        int result = unchecked((short)next) >= (final ? 49 : unchecked((short)task.ReadWord(0xE))) ? 2 : 0;
        if (result != 0)
        {
            for (int index = 0; index < 170; index++)
            {
                int record = first + index * 24;
                if ((arena.Read(record, 1)[0] & 1) != 0)
                {
                    arena = arena.Write(record, new byte[4]);
                }
            }
        }
        pool = Pool(frame);
        frame = PutPool(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? arena } }, pool.WriteSlot(slot, task.WriteWord(0xC, next).SetNext(pool.Slots[slot].Next)));
        return new(frame, result);

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
        void Store(int offset, int value) => arena = arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
        void Seed(int record, int offset, short size)
        {
            arena = arena.Write(record, BitConverter.GetBytes(1u)).Write(record + 4, new byte[2]).Write(record + 6, BitConverter.GetBytes(size)).Write(record + 8, BitConverter.GetBytes(task.ReadDword(0x10))).Write(record + 12, BitConverter.GetBytes(task.ReadDword(0x14)));
            Store(record + 8, Word(record + 8) + offset);
        }
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
