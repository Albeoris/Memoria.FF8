using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMeltdownController
{
    public const uint RootDescriptor = 0x2465778;

    public static NativeBattleMeltdownFrame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleMeltdownResources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 148, Entry: 0x6120D0 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Meltdown requires its selected entry and action context.");
        }
        NativeBattleMeltdownState state = memory.Meltdown148 ?? throw new NotSupportedException("Native Meltdown requires its explicit process memory owner.");
        if (state.Globals.IsDefault || state.Globals.Length != 0x1AB0)
        {
            throw new InvalidDataException("Native Meltdown lost its retained executable fields.");
        }
        if ((parameters.Flags79 & 1) == 0)
        {
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Meltdown requires its current texture queue.");
            NativeBattleEffectInitializerAsset texture = effect.Binding.InitializerAsset ?? throw new NotSupportedException("Native Meltdown requires its original TIM allocation.");
            (textures, _) = textures.EnqueueTim505E30(new(texture.Name, texture.Data));
            memory = memory with { Textures = textures.Register5099A0(resources.Animation) };
        }
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, 0x2465768, 0x10, 1);
        (root, _) = root.Append(0x614230);
        NativeBattleEffectTaskPool tasks = NativeBattleEffectTaskPool.Initialize(0x24657E8, 0x24657F8, 0x24, 100);
        (tasks, int slot) = tasks.Append(0x612190);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Meltdown requires its original first target.");
        NativeBattleModulePoolTask director = tasks.Slots[slot].WriteWord(12, 0).WriteWord(14, 0).WriteWord(32, packets.ReadContact(first).Primary.ActorIndex).WriteWord(34, header.ReadByte(0));
        tasks = tasks with { Slots = tasks.Slots.SetItem(slot, director) };
        return new(memory, effect with { Pools = [root, tasks], ReturnedDescriptor = RootDescriptor }, random);
    }

    public static NativeBattleMeltdownFrame Advance(NativeBattleMeltdownFrame frame, NativeBattleMeltdownResources resources, NativeBattleMeltdownInputs inputs)
    {
        return frame.Effect.ReturnedDescriptor == 0 ? frame : new Execution(frame, resources, inputs).Advance();
    }

    private sealed partial class Execution
    {
        private NativeBattleMeltdownFrame current;
        private readonly NativeBattleMeltdownResources resources;
        private readonly NativeBattleMeltdownInputs inputs;
        private readonly byte[] globals;
        private NativeBattleEffectTaskPool tasks;

        internal Execution(NativeBattleMeltdownFrame frame, NativeBattleMeltdownResources resources, NativeBattleMeltdownInputs inputs)
        {
            current = frame;
            this.resources = resources;
            this.inputs = inputs;
            globals = (frame.Memory.Meltdown148 ?? throw new NotSupportedException("Native Meltdown requires its explicit process memory owner.")).Globals.ToArray();
            if (frame.Effect.Pools.Length != 2 || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.Effect.Pools[1].DescriptorAddress != 0x24657E8)
            {
                throw new InvalidDataException("Native Meltdown lost its selected physical queues.");
            }
            tasks = frame.Effect.Pools[1];
        }

        internal NativeBattleMeltdownFrame Advance()
        {
            NativeBattleEffectTaskPool root = current.Effect.Pools[0];
            NativeBattleModulePoolTask task = root.Slots[0];
            NativeBattleMeltdownState state = current.Memory.Meltdown148!;
            current = current with { Memory = current.Memory with { Meltdown148 = state with { Cursor = (task.ReadByte(12) & 1) == 0 ? 0 : 0x8000 } } };
            NativeBattleEffectTaskStep step = tasks.Advance((pool, slot) =>
            {
                tasks = pool;
                NativeBattleModulePoolTask node = tasks.Slots[slot];
                bool complete = node.Callback switch
                {
                    0x612190 => Director(ref node),
                    0x612540 => Melt(ref node),
                    0x612D90 => Disc(ref node),
                    0x613A50 => Glow(ref node),
                    0x613C40 => Particles(ref node, false),
                    0x613EF0 => Particles(ref node, true),
                    _ => throw new NotSupportedException("Native Meltdown reached an unknown private callback.")
                };
                node = node with { Next = tasks.Slots[slot].Next };
                tasks = tasks with { Slots = tasks.Slots.SetItem(slot, node) };
                return new(tasks, complete ? 2 : 0);
            });
            tasks = step.Pool;
            task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            root = root with { Slots = root.Slots.SetItem(0, task) };
            if (step.Survivors == 0)
            {
                root = root.Advance((pool, _) => new(pool, 2)).Pool;
            }
            PublishGlobals();
            return current with { Effect = current.Effect with { Pools = [root, tasks], ReturnedDescriptor = step.Survivors == 0 ? 0u : RootDescriptor } };
        }

        private void Emit(NativeBattleMeltdownOperation operation)
        {
            PublishGlobals();
            current = inputs.Execute(current, operation);
        }

        private void PublishGlobals()
        {
            NativeBattleMeltdownState state = current.Memory.Meltdown148 ?? throw new InvalidDataException("Native Meltdown operation lost process memory.");
            current = current with { Memory = current.Memory with { Meltdown148 = state with { Globals = globals.ToImmutableArray() } } };
        }

        private NativeBattleModulePoolTask Add(uint callback, Action<NativeBattleModulePoolTask, int> initialize)
        {
            (tasks, int slot) = tasks.Append(callback);
            if (slot < 0)
            {
                throw new InvalidDataException("Native Meltdown private callback allocation reached its original unchecked failure.");
            }
            NativeBattleModulePoolTask task = tasks.Slots[slot];
            initialize(task, slot);
            return tasks.Slots[slot];
        }

        private void Set(int slot, NativeBattleModulePoolTask task) => tasks = tasks with { Slots = tasks.Slots.SetItem(slot, task) };
        private short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        private static int Offset(uint address) => address >= 0x2476640 ? checked(0x1A90 + (int)(address - 0x2476640)) : checked((int)(address - 0x2464BA8));
        private short W(uint address) => BinaryPrimitives.ReadInt16LittleEndian(globals.AsSpan(Offset(address)));
        private void W(uint address, int value) => BinaryPrimitives.WriteInt16LittleEndian(globals.AsSpan(Offset(address)), unchecked((short)value));
        private int D(uint address) => BinaryPrimitives.ReadInt32LittleEndian(globals.AsSpan(Offset(address)));
        private void D(uint address, int value) => BinaryPrimitives.WriteInt32LittleEndian(globals.AsSpan(Offset(address)), value);
        private NativeBattleModuleAnchor Anchor(uint address) => new(W(address), W(address + 2), W(address + 4), W(address + 6));
        private void Anchor(uint address, NativeBattleModuleAnchor value)
        {
            W(address, value.X);
            W(address + 2, value.Y);
            W(address + 4, value.Z);
            W(address + 6, value.W);
        }
        private NativeBattleActionMatrix Matrix(uint address) => new(Enumerable.Range(0, 9).Select(index => W(address + (uint)(2 * index))).ToImmutableArray(), D(address + 20), D(address + 24), D(address + 28), 4096);
        private void Matrix(uint address, NativeBattleActionMatrix value)
        {
            for (int index = 0; index < 9; index++)
            {
                W(address + (uint)(index * 2), value.Rotation[index]);
            }
            D(address + 20, value.X);
            D(address + 24, value.Y);
            D(address + 28, value.Z);
        }
        private ushort Roll()
        {
            NativeCrtRandomState random = current.Random.Draw(out ushort value);
            current = current with { Random = random };
            return value;
        }
        private static uint Target(int action) => checked(0x2466608u + (uint)(8 * action));
        private static uint ActionFrame(int action) => checked(0x2465788u + (uint)(32 * action));
    }
}
