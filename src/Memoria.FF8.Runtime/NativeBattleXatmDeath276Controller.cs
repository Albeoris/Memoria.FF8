using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleXatmDeath276Controller
{
    public static NativeBattleXatmDeath276Frame Initialize684F10(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleXatmDeath276Resources resources)
    {
        if (effect.Binding.Entry != 0x684EE0 || effect.Parameters is null)
        {
            throw new NotSupportedException("Native684F10 requires its original thunk and cast parameters.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native684F10 requires its loaded texture arena.");
        int texture = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x250827C)?.ArenaOffset ?? throw new NotSupportedException("Native684F10 requires its loaded250827C TIM pointer.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native684F10 requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, texture);
        NativeBattleXatmDeath276State state = new(memory.Cursor, null, 0);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2508318, 0x2508290, 0x14, 1);
        (root, int slot) = root.Append(0x684FA0);
        root = root.WriteSlot(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, 0));
        memory = memory with { Textures = textures.Register5099A0(resources.Camera) };
        return new(memory, effect with { Pools = [root], ReturnedDescriptor = 0x2508318 }, state);
    }

    public static NativeBattleXatmDeath276Frame Advance684FA0(NativeBattleXatmDeath276Frame frame, NativeBattleXatmDeath276Resources resources, NativeBattleXatmDeath276Inputs inputs)
    {
        Execution execution = new(frame, resources, inputs);
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            execution.Pool(0, pool);
            int result = execution.Root(slot);
            return new(execution.Frame.Effect.Pools[0], result);
        });
        execution.Pool(0, roots.Pool);
        if (roots.Survivors == 0)
        {
            execution.Frame = execution.Frame with { Effect = execution.Frame.Effect with { ReturnedDescriptor = 0 } };
        }
        return execution.Frame;
    }

    private sealed partial class Execution(NativeBattleXatmDeath276Frame frame, NativeBattleXatmDeath276Resources resources, NativeBattleXatmDeath276Inputs inputs)
    {
        public NativeBattleXatmDeath276Frame Frame { get; set; } = frame;
        public void Pool(int index, NativeBattleEffectTaskPool pool) => Frame = Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(index, pool) } };
        private NativeBattleModulePoolTask Read(int pool, int slot) => Frame.Effect.Pools[pool].Slots[slot];
        private void Put(int pool, int slot, NativeBattleModulePoolTask task) => Pool(pool, Frame.Effect.Pools[pool].WriteSlot(slot, task));
        private void Execute(NativeBattleXatmDeath276Operation operation) => Frame = inputs.Execute(Frame, operation);
        private int Random()
        {
            Frame = Frame with { Memory = Frame.Memory.DrawSharedEffectRandom7059E0(out ushort value) };
            return value;
        }
        private int Append(int pool, uint callback)
        {
            (NativeBattleEffectTaskPool next, int slot) = Frame.Effect.Pools[pool].Append(callback);
            Pool(pool, next);
            return slot;
        }
        public int Root(int slot)
        {
            NativeBattleModulePoolTask task = Read(0, slot);
            bool alternate = task.ReadDword(16) != 0;
            Frame = Frame with { State = Frame.State with { DrawCursor = checked(Frame.State.BufferOffset + (alternate ? 0x4314 : 0x14314)) } };
            task = task.WriteDword(16, alternate ? 0u : 1u);
            Put(0, slot, task);
            if (Word(task, 12) == 1 && task.ReadByte(15) == 0)
            {
                Put(0, slot, task.WriteByte(15, 1));
                Start();
            }
            if (Read(0, slot).ReadByte(15) != 0)
            {
                Execute(new NativeBattleXatmDeath276Bones(true));
                AdvancePool(6, 0x685CF0, Event);
                int arcs = AdvancePool(5, 0x686A10, index =>
                {
                    NativeBattleXatmDeath276Step step = inputs.AdvanceArc(Frame, index);
                    Frame = step.Frame;
                    return step.Result;
                });
                Execute(new NativeBattleXatmDeath276Bones(false));
                AdvancePool(4, 0x6860B0, index =>
                {
                    Frame = inputs.AdvancePrim(Frame, index);
                    NativeBattleModulePoolTask prim = Add(Read(4, index), 12, 1);
                    Put(4, index, prim);
                    return Word(prim, 12) < 16 ? 0 : 2;
                });
                AdvancePool(3, 0x6866F0, Spark);
                AdvancePool(2, 0x686820, index => Moving(index, false));
                AdvancePool(1, 0x686000, index => Moving(index, true));
                int director = AdvancePool(7, 0x6851F0, Director);
                if (director == 0 && arcs == 0)
                {
                    return 2;
                }
            }
            Put(0, slot, Add(Read(0, slot), 12, 1));
            return 0;
        }

        private int AdvancePool(int index, uint callback, Func<int, int> advance)
        {
            NativeBattleEffectTaskStep step = Frame.Effect.Pools[index].Advance((pool, slot) =>
            {
                Pool(index, pool);
                if (Read(index, slot).Callback != callback)
                {
                    throw new NotSupportedException("Native X-ATM092 queue requires its original selected task body.");
                }
                int result = advance(slot);
                return new(Frame.Effect.Pools[index], result);
            });
            Pool(index, step.Pool);
            return step.Survivors;
        }

        private void Start()
        {
            Execute(new NativeBattleXatmDeath276Music(240));
            uint buffer = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)Frame.State.BufferOffset);
            ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = Frame.Effect.Pools.ToBuilder();
            pools.Add(NativeBattleEffectTaskPool.Initialize(0x25082A8, buffer, 0x1C, 0x40));
            pools.Add(NativeBattleEffectTaskPool.Initialize(0x25082B8, buffer + 0x700, 0x1C, 0x2A));
            pools.Add(NativeBattleEffectTaskPool.Initialize(0x25082C8, buffer + 0xB98, 0x18, 0x20));
            pools.Add(NativeBattleEffectTaskPool.Initialize(0x25082D8, buffer + 0xE98, 0x13C, 3));
            pools.Add(NativeBattleEffectTaskPool.Initialize(0x25082E8, buffer + 0x124C, 0x4C, 0x20));
            pools.Add(NativeBattleEffectTaskPool.Initialize(0x25082F8, buffer + 0x1BCC, 0x14, 5));
            pools.Add(NativeBattleEffectTaskPool.Initialize(0x2508308, buffer + 0x1C30, 0x2454, 1));
            Frame = Frame with { Effect = Frame.Effect with { Pools = pools.ToImmutable() } };
            int director = Append(7, 0x6851F0);
            NativeBattleModulePoolTask task = Read(7, director);
            for (int offset = 12; offset < 0x2454; offset += 4)
            {
                task = task.WriteDword(offset, 0);
            }
            Put(7, director, task);
            NativeBattleXatmDeath276Vibration vibration = inputs.StartVibration(Frame);
            Frame = vibration.Frame;
            task = Read(7, director).WriteDword(0x4C, unchecked((uint)vibration.Handle));
            uint address = buffer + 0x1C30;
            task = task.WriteDword(0x50, address + 0x54).WriteDword(0x48, address + 0xE);
            for (int offset = 14; offset < 14 + 55; offset++)
            {
                task = task.WriteByte(offset, 255);
            }
            Put(7, director, task);
            NativeBattleEffectArena arena = Frame.Memory.Arena ?? throw new NotSupportedException("Native684FA0 requires its actual mutable arena.");
            byte[] profile = arena.Read(Frame.State.BufferOffset + 0x4304, 16).ToArray();
            for (int index = 0; index < 8; index++)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(profile.AsSpan(index * 2), unchecked((short)NativeBattleCameraMath.Sine((index << 11) / 7)));
            }
            Frame = Frame with { Memory = Frame.Memory with { Arena = arena.Write(Frame.State.BufferOffset + 0x4304, profile) } };
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask W(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)value));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => W(task, offset, task.ReadWord(offset) + value);
}

