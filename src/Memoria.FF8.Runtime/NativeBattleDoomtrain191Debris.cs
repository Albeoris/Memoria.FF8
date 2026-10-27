using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDoomtrain191Debris
{
    public static NativeBattleDoomtrain191LeafStep AdvanceFactory646180(NativeBattleDoomtrain191Frame frame, int slot, Func<uint> readFlags)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x646180)
        {
            throw new InvalidDataException("Native Doomtrain debris factory requires its actual task.");
        }
        if ((readFlags() & 1) != 0)
        {
            return new(frame, 0);
        }
        int age = unchecked((short)task.ReadWord(0xC));
        if (age >= 0 && age < 12 && age % 3 == 1)
        {
            int count = Random() % 2 + 1;
            for (int index = 0; index < count; index++)
            {
                pool = Pool(frame);
                (pool, int child) = pool.Append(0x6462D0);
                ushort delay = unchecked((ushort)(Random() % 4 + index));
                ushort x = unchecked((ushort)(Random() % 400 + 1000));
                ushort y = unchecked((ushort)(Random() % 2000 - 1300));
                ushort z = unchecked((ushort)(-6000 - Random() % 3000));
                short scale = unchecked((short)(Random() % 3072 + 1152));
                ushort stretch = unchecked((ushort)((Random() % 6144 + 1664) / 2));
                NativeBattleModulePoolTask row = pool.Slots[child].WriteWord(0xC, 0).WriteWord(0xE, delay).WriteWord(0x10, x).WriteWord(0x12, y).WriteWord(0x14, z).WriteWord(0x16, 12).WriteWord(0x1C, unchecked((ushort)scale)).WriteWord(0x1E, unchecked((ushort)(scale / 6))).WriteWord(0x20, stretch).WriteWord(0x22, stretch);
                frame = PutPool(frame, pool.WriteSlot(child, row));
            }
        }
        pool = Pool(frame);
        ushort next = unchecked((ushort)(age + 1));
        frame = PutPool(frame, pool.WriteSlot(slot, pool.Slots[slot].WriteWord(0xC, next)));
        return new(frame, unchecked((short)next) > 12 ? 2 : 0);

        int Random()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    public static NativeBattleDoomtrain191LeafStep Advance6462D0(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleDoomtrain191CreatureResources resources, NativeBattleDoomtrain191MeshServices services)
    {
        NativeBattleEffectTaskPool pool = Pool(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.Callback != 0x6462D0)
        {
            throw new InvalidDataException("Native Doomtrain debris requires its actual drawing task.");
        }
        bool paused = (services.ReadFlags() & 1) != 0;
        if (Word(0xE) > 0)
        {
            if (!paused)
            {
                frame = PutPool(frame, pool.WriteSlot(slot, task.WriteWord(0xE, unchecked((ushort)(task.ReadWord(0xE) - 1)))));
            }
            return new(frame, 0);
        }
        NativeBattleDoomtrain191State state = frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain debris requires its current work pointer.");
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Word(0x10) < 0 ? (short)-100 : (short)100, 1024)), Word(0x1C), Word(0x1C), Word(0x20)) with { X = Word(0x10), Y = Word(0x12), Z = Word(0x14) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(state.BaseView.ReadMatrix(), NativeBattleGfMatrices.Compose56C2F0(NativeBattleDoomtrain191Model.Require(frame).Anchor.ReadMatrix(), local));
        int age = Word(0xC);
        int fadeStart = Word(0x16) - 8;
        uint flags = age < fadeStart ? 0x2033u : 0x20F3u;
        int? fade = age < fadeStart ? null : unchecked((age - fadeStart) << 9);
        NativeBattleStaticMeshResource mesh = resources.RequireMesh(0xE3BD64);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Doomtrain debris requires its actual morph workspace.");
        int destination = checked((int)(state.WorkPointers[6] - state.ArenaAddress));
        int factor = unchecked(age << 12) / 12;
        for (int index = 0; index < count; index++)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                int at = checked(index * 8 + lane * 2);
                short start = BinaryPrimitives.ReadInt16LittleEndian(resources.DebrisStart.Data.AsSpan().Slice(at));
                short end = BinaryPrimitives.ReadInt16LittleEndian(resources.DebrisEnd.Data.AsSpan().Slice(at));
                short value = unchecked((short)(start + (unchecked((end - start) * factor) >> 12)));
                arena = arena.Write(destination + at, BitConverter.GetBytes(value));
            }
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        frame = services.Draw(frame, new(0xE3BD64, matrix, flags, 3, fade) { SelectedVerticesOffset = destination });
        if (paused)
        {
            return new(frame, 0);
        }
        task = task.WriteWord(0x1C, unchecked((ushort)(Word(0x1C) - Word(0x1E))));
        short stretch = unchecked((short)(Word(0x20) + Word(0x22)));
        short step = Word(0x22);
        task = task.WriteWord(0x20, unchecked((ushort)Math.Min(stretch, (short)0x7000))).WriteWord(0x22, unchecked((ushort)(step - step / 8)));
        ushort next = unchecked((ushort)(age + 1));
        pool = Pool(frame);
        frame = PutPool(frame, pool.WriteSlot(slot, task.WriteWord(0xC, next).SetNext(pool.Slots[slot].Next)));
        return new(frame, unchecked((short)next) >= Word(0x16) || (frame.Memory.Doomtrain191!.LookupCounter & 2) != 0 ? 2 : 0);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDoomtrain191Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
    private static NativeBattleDoomtrain191Frame PutPool(NativeBattleDoomtrain191Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool(frame)), pool) } };
}
