using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoomtrain191RigBank([property: JsonRequired] ImmutableArray<NativeBattlePackedMatrix32> World, [property: JsonRequired] ImmutableArray<NativeBattlePackedMatrix32> View);
public sealed record NativeBattleDoomtrain191RigResources(NativeBattleStaticMeshResource First, NativeBattleStaticMeshResource Second, NativeBattleStaticMeshResource Wheel, NativeBattleStaticMeshResource Steam, NativeBattleModuleResource Sprite);
public sealed record NativeBattleDoomtrain191RigServices(Func<uint> ReadFlags, Func<NativeBattleDoomtrain191Frame, NativeBattleModulePoolTask, NativeBattleActionMatrix, NativeBattleDoomtrain191Frame> Draw, Func<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191Frame> PlayStream);

public static class NativeBattleDoomtrain191Rigs
{
    public static NativeBattleDoomtrain191LeafStep Advance(NativeBattleDoomtrain191Frame frame, int slot, NativeBattleDoomtrain191RigServices services)
    {
        NativeBattleEffectTaskPool pool = Pool();
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (services.ReadFlags() & 1) != 0;
        int variant = task.ReadWord(0xE) == 0 ? 0 : 1;
        short age = Word(0xC);
        if (task.Callback == 0x63FB40)
        {
            if (!paused)
            {
                if (age == 0)
                {
                    NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Word(0x18), 0)), Word(0x1C), Word(0x1C), Word(0x1C)) with { X = Word(0x10), Y = Word(0x12), Z = Word(0x14) };
                    NativeBattleDoomtrain191RigBank bank = Bank(true);
                    Set(State() with { Rigs = bank with { World = bank.World.SetItem(variant, NativeBattleDoomtrain191Controller.Pack(world, bank.World[variant], true)) } });
                    Spawn(0x63FDB0, 0, 0, 0, 2048);
                    if (task.ReadWord(0x20) != 0)
                    {
                        Spawn(0x63FEC0, 0, 0, 0, 2048);
                    }
                    int wheel = Spawn(0x63FFD0, 520, -550, 460, 1536);
                    Write(wheel, Pool().Slots[wheel].WriteWord(0x20, 1536).WriteWord(0x18, task.ReadWord(0x1A) == 0 ? (ushort)0 : (ushort)0xFC00).WriteWord(0x1A, task.ReadWord(0x1A) == 0 ? (ushort)0 : (ushort)0xFFCD));
                    if (task.ReadWord(0x20) != 0)
                    {
                        Spawn(0x640280, 0, -720, -180, 1536);
                    }
                }
                if (age % 7 == 0 && task.ReadWord(0x20) != 0 && age < 80)
                {
                    Spawn(0x640140, (age / 7 & 1) != 0 ? 180 : -180, -1180, -190, 1920, false);
                    frame = services.PlayStream(frame);
                }
                task = task.WriteWord(0xC, unchecked((ushort)(age + 1)));
                Write(slot, task);
            }
            NativeBattleDoomtrain191RigBank retained = Bank(false);
            NativeBattleActionMatrix composed = NativeBattlePoseMatrices.Compose56C2F0(State().BaseView.ReadMatrix(), retained.World[variant].ReadMatrix());
            Set(State() with { Rigs = retained with { View = retained.View.SetItem(variant, NativeBattleDoomtrain191Controller.Pack(composed, retained.View[variant], true)) } });
            return new(frame, unchecked((short)task.ReadWord(0xC)) > Word(0x16) ? 2 : 0);
        }
        if (task.Callback is not (0x63FDB0 or 0x63FEC0 or 0x63FFD0 or 0x640140 or 0x640280))
        {
            throw new NotSupportedException($"Native Doomtrain rig callback{task.Callback:X8} requires its original contract.");
        }
        int scale = Word(0x1C);
        int scaleYZ = task.Callback == 0x63FFD0 ? Word(0x20) : scale;
        short angle = task.Callback == 0x63FFD0 ? Word(0x18) : (short)0;
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 0, angle)), scale, scaleYZ, scaleYZ) with { X = Word(0x10), Y = Word(0x12), Z = Word(0x14) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(Bank(false).View[variant].ReadMatrix(), local);
        frame = services.Draw(frame, task, matrix);
        if (paused)
        {
            return new(frame, 0);
        }
        if (task.Callback == 0x63FFD0 && age >= 25)
        {
            if (age < 45)
            {
                task = task.WriteWord(0x18, unchecked((ushort)(Word(0x18) - Word(0x1A))));
            }
            else if (age < 51)
            {
                int adjustment = unchecked(20 * NativeBattleCameraMath.Sine((age - 45) * 1024 / 6 + 1024)) >> 12;
                task = task.WriteWord(0x18, unchecked((ushort)(Word(0x18) + ((age & 1) == 0 ? -adjustment : adjustment))));
            }
            else
            {
                task = task.WriteWord(0x18, 0);
            }
        }
        ushort next = unchecked((ushort)(age + 1));
        Write(slot, task.WriteWord(0xC, next));
        return new(frame, unchecked((short)next) >= (task.Callback == 0x640140 ? 8 : Word(0x16)) ? 2 : 0);

        int Spawn(uint callback, int x, int y, int z, ushort size, bool endAtParent = true)
        {
            NativeBattleEffectTaskPool current = Pool();
            (current, int child) = current.Append(callback);
            Save(current);
            NativeBattleModulePoolTask spawned = current.Slots[child].WriteWord(0xC, 0).WriteWord(0xE, task.ReadWord(0xE)).WriteWord(0x10, unchecked((ushort)x)).WriteWord(0x12, unchecked((ushort)y)).WriteWord(0x14, unchecked((ushort)z)).WriteWord(0x1C, size);
            if (endAtParent)
            {
                spawned = spawned.WriteWord(0x16, task.ReadWord(0x16));
            }
            Write(child, spawned);
            return child;
        }
        short Word(int offset) => unchecked((short)task.ReadWord(offset));
        NativeBattleDoomtrain191State State() => frame.Memory.Doomtrain191 ?? throw new NotSupportedException("Native Doomtrain rigs require their retained module owner.");
        void Set(NativeBattleDoomtrain191State state) => frame = frame with { Memory = frame.Memory with { Doomtrain191 = state } };
        NativeBattleDoomtrain191RigBank Bank(bool writing)
        {
            if (State().Rigs is { } bank)
            {
                return bank;
            }
            if (!writing)
            {
                throw new NotSupportedException("Native Doomtrain rig matrices require their actual constructor writes.");
            }
            NativeBattlePackedMatrix32 unknown = new(new byte[32].ToImmutableArray(), new bool[32].ToImmutableArray());
            return new([unknown, unknown], [unknown, unknown]);
        }
        NativeBattleEffectTaskPool Pool() => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDoomtrain191Controller.ChildDescriptor);
        void Save(NativeBattleEffectTaskPool value) => frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(Pool()), value) } };
        void Write(int index, NativeBattleModulePoolTask value)
        {
            NativeBattleEffectTaskPool current = Pool();
            Save(current.WriteSlot(index, value));
        }
    }
}
