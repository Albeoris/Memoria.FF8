using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSandStorm176Controller
{
    public const uint RootDescriptor = 0x2368598;
    public const uint ModuleBase = 0x2368580;
    public const int ModuleLength = 0xBFF4;

    public static NativeBattleSandStorm176Frame Initialize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleSandStorm176Resources resources, NativeCrtRandomState random)
    {
        if (effect.Binding is not { Effect: 176, Entry: 0x5D00F0 } || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Sand Storm requires its original selected entry and action context.");
        }
        NativeBattleSandStorm176State state = memory.SandStorm176 ?? throw new NotSupportedException("Native Sand Storm requires its retained executable module owner.");
        if (state.PendingColdMatrix)
        {
            if (state.FunnelMatrix is not null)
            {
                throw new InvalidDataException("Native Sand Storm pending matrix already contains mutable state.");
            }
            state = state with { FunnelMatrix = resources.FunnelMatrix, PendingColdMatrix = false };
        }
        using NativeBattleEffectArenaWriter module = new(state.Module);
        for (int index = 0; index < 150; index++)
        {
            module.Write(checked((int)(0x23685B8 - ModuleBase)) + index * 24, new byte[4]);
        }
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, 0x2368588, 16, 1);
        (root, int rootSlot) = root.Append(0x5D1A60);
        root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x23733E8, 0x23733F8, 36, 100);
        (children, int director) = children.Append(0x5D01B0);
        children = children.WriteSlot(director, children.Slots[director].WriteWord(12, 0));
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Sand Storm requires its texture queue.");
        textures = textures.Register5099A0(resources.Camera);
        int source = effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x2374554)?.ArenaOffset ?? throw new NotSupportedException("Native Sand Storm requires its initializer file allocation.");
        (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native Sand Storm requires its file arena."), source);
        return new(memory with { SandStorm176 = state with { Module = module.Freeze() }, Textures = textures }, effect with { Pools = [root, children], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = source }, random);
    }

    public static NativeBattleSandStorm176Frame Advance(NativeBattleSandStorm176Frame frame, NativeBattleSandStorm176Resources resources, NativeBattleSandStorm176Inputs inputs) => frame.Effect.ReturnedDescriptor == 0 ? frame : new Execution(frame, resources, inputs).Advance();

    private sealed partial class Execution
    {
        private NativeBattleSandStorm176Frame current;
        private readonly NativeBattleSandStorm176Resources resources;
        private readonly NativeBattleSandStorm176Inputs inputs;
        private NativeBattleEffectTaskPool children;
        private int parity;

        internal Execution(NativeBattleSandStorm176Frame frame, NativeBattleSandStorm176Resources resources, NativeBattleSandStorm176Inputs inputs)
        {
            current = frame;
            this.resources = resources;
            this.inputs = inputs;
            children = frame.Effect.Pools[1];
        }

        internal NativeBattleSandStorm176Frame Advance()
        {
            NativeBattleEffectTaskPool roots = current.Effect.Pools[0];
            NativeBattleModulePoolTask root = roots.Slots[0];
            parity = root.ReadWord(12) & 1;
            int source = current.Effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x2374554).ArenaOffset;
            current = current with { Effect = current.Effect with { DrawArenaOffset = source + parity * 0xC000 } };
            NativeBattleEffectTaskStep step = children.Advance((pool, slot) =>
            {
                children = pool;
                NativeBattleModulePoolTask task = pool.Slots[slot];
                int result = task.Callback switch
                {
                    0x5D01B0 => Director(ref task),
                    0x5D0740 => Funnel(ref task),
                    0x5D11F0 => Sand(ref task, false),
                    0x5D14B0 => Lift(ref task),
                    0x5D1820 => Sand(ref task, true),
                    _ => throw new NotSupportedException("Native Sand Storm reached an unrelated private callback.")
                };
                task = task.UpdateHeader(task.Flags, task.State, children.Slots[slot].Next, task.Callback);
                children = children.WriteSlot(slot, task);
                return new(children, result);
            });
            children = step.Pool;
            root = root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1)));
            roots = roots.WriteSlot(0, root);
            if (step.Survivors == 0)
            {
                roots = roots.Advance((pool, _) => new(pool, 2)).Pool;
            }
            return current with { Effect = current.Effect with { Pools = [roots, children], ReturnedDescriptor = step.Survivors == 0 ? 0u : RootDescriptor } };
        }

        private NativeBattleSandStorm176State State => current.Memory.SandStorm176 ?? throw new NotSupportedException("Native Sand Storm lost its executable owner.");
        private int Caster => current.Effect.Parameters!.Actor78;
        private NativeBattleActionHeaderSlot Header => inputs.Packets.Headers[current.Effect.Parameters!.HeaderIndex7C];
        private int FirstContact => Header.FirstContactIndex ?? throw new NotSupportedException("Native Sand Storm requires its actual contact list.");
        private NativeBattleVector Bone(int index, byte bone, short fraction) => NativeBattlePoseMatrices.ReadBone(inputs.ReadModel(index), inputs.ReadPoseSource(index), bone, fraction);
        private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
        private short W(uint address) => BinaryPrimitives.ReadInt16LittleEndian(State.Module.Read(checked((int)(address - ModuleBase)), 2));
        private uint D(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(State.Module.Read(checked((int)(address - ModuleBase)), 4));
        private void W(uint address, int value) => SetModule(State.Module.Write(checked((int)(address - ModuleBase)), BitConverter.GetBytes(unchecked((ushort)value))));
        private void D(uint address, uint value) => SetModule(State.Module.Write(checked((int)(address - ModuleBase)), BitConverter.GetBytes(value)));
        private void SetModule(NativeBattleEffectArena module) => current = current with { Memory = current.Memory with { SandStorm176 = State with { Module = module } } };
        private ushort Roll()
        {
            NativeCrtRandomState random = current.Random.Draw(out ushort value);
            current = current with { Random = random };
            return value;
        }
        private int Spawn(uint callback)
        {
            (children, int slot) = children.Append(callback);
            return slot;
        }
        private void Put(int slot, NativeBattleModulePoolTask task) => children = children.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, children.Slots[slot].Next, task.Callback));
        private void Emit(NativeBattleSandStorm176Operation operation)
        {
            current = inputs.Execute(current with { Effect = current.Effect with { Pools = [current.Effect.Pools[0], children] } }, operation);
            children = current.Effect.Pools[1];
        }
        private NativeBattleVector Position(uint address) => new(W(address), W(address + 2), W(address + 4));
        private NativeBattleVector TaskPosition(NativeBattleModulePoolTask task, int offset) => new(S(task, offset), S(task, offset + 2), S(task, offset + 4));
        private static NativeBattleModulePoolTask WritePosition(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));

        private int Director(ref NativeBattleModulePoolTask task)
        {
            if (inputs.DrawOnly)
            {
                return 0;
            }
            int age = S(task, 12);
            if (age == 0)
            {
                NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (current.Effect.Parameters!.TargetMask7A & (1 << actor)) != 0).Select(actor => inputs.ReadModel(actor).Transform?.Position28 ?? throw new NotSupportedException("Native Sand Storm target centre requires its position WORDs.")).ToArray());
                W(0x2374298, center.X);
                W(0x237429A, center.Y);
                W(0x237429C, center.Z);
                InitializeFunnel(false);
                InitializeFunnel(true);
            }
            if (age == 12)
            {
                int first = Spawn(0x5D0740);
                short sway = unchecked((short)(Roll() % 100 - 50));
                short growth = unchecked((short)(Roll() % 40 + 20));
                if (sway < 0)
                {
                    growth = unchecked((short)-growth);
                }
                short speed = unchecked((short)(Roll() % 150 + 150));
                NativeBattleModulePoolTask a = WritePosition(children.Slots[first].WriteWord(12, 0).WriteWord(14, 0).WriteWord(28, unchecked((ushort)sway)).WriteWord(30, unchecked((ushort)growth)).WriteWord(32, 0).WriteWord(34, unchecked((ushort)speed)), 16, Bone(Caster, 0x15, 4096));
                Put(first, a);
                int second = Spawn(0x5D0740);
                Put(second, WritePosition(children.Slots[second].WriteWord(12, 0).WriteWord(14, 1).WriteWord(28, unchecked((ushort)-sway)).WriteWord(30, unchecked((ushort)-growth)).WriteWord(32, 0).WriteWord(34, unchecked((ushort)speed)), 16, Bone(Caster, 0x16, 4096)));
                W(0x23685AA, 0);
                W(0x23685B2, 0);
                W(0x23685AE, 300);
                W(0x23685B6, 300);
                for (int mask = 1; mask <= 2; mask++)
                {
                    int sand = Spawn(0x5D11F0);
                    Put(sand, children.Slots[sand].WriteWord(12, 0).WriteWord(14, (ushort)mask));
                }
            }
            if (age == 14)
            {
                Emit(new NativeBattleSandStorm176Sound(resources.WindSound));
            }
            if (age == 57)
            {
                for (int index = 0; index < Header.ReadByte(16); index++)
                {
                    int actor = inputs.Packets.ReadContact(FirstContact + index).Primary.ActorIndex;
                    int lift = Spawn(0x5D14B0);
                    int spin = (Roll() % 2 + 1) << 12;
                    int speed = -45 - Roll() % 65;
                    NativeBattleVector origin = Bone(actor, 0xF1, 0);
                    int distanceA = Distance(Position(0x23685A8), origin);
                    int distanceB = Distance(Position(0x23685B0), origin);
                    Put(lift, WritePosition(children.Slots[lift].WriteWord(12, 0).WriteWord(14, (ushort)actor).WriteWord(24, (ushort)spin).WriteWord(26, (ushort)index).WriteWord(28, distanceA < distanceB ? (ushort)1 : (ushort)0).WriteWord(30, (ushort)(Roll() % 6)).WriteWord(32, 0).WriteWord(34, unchecked((ushort)speed)), 16, origin));
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 > 120 ? 2 : 0;
        }

        private static int Distance(NativeBattleVector a, NativeBattleVector b)
        {
            int dx = a.X - b.X;
            int dz = a.Z - b.Z;
            return unchecked((int)Math.Sqrt(unchecked(dx * dx + dz * dz)));
        }
    }
}
