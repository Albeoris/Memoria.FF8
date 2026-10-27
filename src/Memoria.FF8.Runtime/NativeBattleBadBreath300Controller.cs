using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBadBreath300Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBadBreath300Controller
{
    public static NativeBattleBadBreath300Frame Initialize(NativeBattleBadBreath300Frame frame, NativeBattleBadBreath300Resources resources, NativeBattleBadBreath300Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Bad Breath requires its executed texture initializer pointer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Bad Breath requires its texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Bad Breath requires its working arena."), tim);
        frame = frame with { State = frame.State.SeedColdSources(resources).WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)) };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int index) = root.Append(RootCallback);
        root = root.WriteSlot(index, root.Slots[index].WriteDword(12, 0).WriteDword(16, 0));
        return frame with { Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }, Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) } };
    }

    public static NativeBattleBadBreath300Frame Advance(NativeBattleBadBreath300Frame frame, NativeBattleBadBreath300Resources resources, NativeBattleBadBreath300Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 4) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Bad Breath requires its original root and child pool owners.");
        }
        frame = frame with { TargetScaleStack = NativeBattleStackBytes.Unknown($"bad-breath300:{inputs.Callback}", -0x4C, 4) };
        NativeBattleBadBreath300Frame current = frame;
        NativeBattleEffectTaskStep advanced = current.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            (current, int result) = Root(current, slot, resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, advanced.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = advanced.Survivors == 0 ? 0u : RootDescriptor }, TargetScaleStack = null };
    }

    private static (NativeBattleBadBreath300Frame Frame, int Result) Root(NativeBattleBadBreath300Frame frame, int slot, NativeBattleBadBreath300Resources resources, NativeBattleBadBreath300Inputs inputs)
    {
        NativeBattleModulePoolTask root = Task(frame, 0, slot);
        if (root.Callback != RootCallback)
        {
            throw new NotSupportedException("Bad Breath root callback is unsupported.");
        }
        uint working = frame.State.ReadDword(WorkingGlobal);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        root = root.WriteDword(16, first ? 0u : 1u);
        frame = Put(frame, 0, slot, root);
        if (root.ReadWord(12) == 1 && root.ReadByte(15) == 0)
        {
            frame = Put(frame, 0, slot, root.WriteByte(15, 1));
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + 0x800, 0x3C, 1);
            NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(TargetDescriptor, working + 0x300, 0x140, 4);
            NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, working, 0x18, 32);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, targets, particles] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [main, targets, particles]) };
            (frame, uint address) = Append(frame, 1, MainCallback);
            NativeBattleModulePoolTask task = Read(frame, address).WriteBytes(12, new byte[48]);
            int firstContact = FirstContact(frame, inputs);
            int count = inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].ReadByte(16);
            if (count is < 1 or > 4)
            {
                throw new InvalidDataException("Bad Breath requires one to four captured target contacts.");
            }
            task = task.WriteWord(14, (ushort)count).WriteWord(0x24, 0x700);
            short z = 0;
            short y = 0;
            short maximum = 0x700;
            for (int index = 0; index < count; index++)
            {
                int actor = inputs.Packets.ReadContact(firstContact + index).Primary.ActorIndex;
                task = task.WriteDword(0x10 + index * 4, ModelAddress(actor)).WriteByte(0x20 + index, (byte)index);
                NativeBattleBadBreath300AnchorStep read = inputs.ReadAnchor571400(frame, actor);
                frame = read.Frame;
                short absolute = read.Anchor.X < 0 ? unchecked((short)-read.Anchor.X) : read.Anchor.X;
                maximum = Math.Max(maximum, absolute);
                z = unchecked((short)(z + read.Anchor.Z));
                y = unchecked((short)(y + read.Anchor.Y));
            }
            task = task.WriteWord(0x24, unchecked((ushort)(maximum + 256))).WriteWord(0x26, unchecked((ushort)(z / count))).WriteWord(0x28, unchecked((ushort)(y / count)));
            for (int index = 0; index < count - 1; index++)
            {
                for (int next = index + 1; next < count; next++)
                {
                    uint left = task.ReadDword(0x10 + index * 4);
                    uint right = task.ReadDword(0x10 + next * 4);
                    if (Transform(inputs.ReadModel(Actor(left))).Position28.X < Transform(inputs.ReadModel(Actor(right))).Position28.X)
                    {
                        byte old = task.ReadByte(0x20 + index);
                        task = task.WriteDword(0x10 + index * 4, right).WriteDword(0x10 + next * 4, left).WriteByte(0x20 + index, task.ReadByte(0x20 + next)).WriteByte(0x20 + next, old);
                    }
                }
            }
            frame = Write(frame, address, task);
            frame = frame with { State = frame.State.WriteMesh(MeshAAddress, Normalize6B7CB0(frame.State.ReadMesh(MeshAAddress))).WriteMesh(MeshBAddress, Normalize6B7CB0(frame.State.ReadMesh(MeshBAddress))) };
            byte caster = checked((byte)Actor(frame.State.ReadDword(CasterGlobal)));
            frame = inputs.Execute(frame, new NativeBattleBadBreath300BindCaster(caster, working + BindingOffset, 0x103DCB0));
            frame = inputs.Execute(frame, new NativeBattleBadBreath300RingSequence(caster, 1));
        }
        NativeBattleBadBreath300Frame? frameForScan = null;
        if (Task(frame, 0, slot).ReadByte(15) != 0)
        {
            (frame, int main) = Scan(frame, 1, MainCallback, address => Main(address));
            (frame, int targets) = Scan(frame, 2, TargetCallback, address => Target(address));
            (frame, int particles) = Scan(frame, 3, ParticleCallback, address => Particle(address));
            if (main == 0 && targets == 0 && particles == 0)
            {
                return (frame, 2);
            }
        }
        return (Put(frame, 0, slot, Task(frame, 0, slot).WriteWord(12, unchecked((ushort)(Task(frame, 0, slot).ReadWord(12) + 1)))), 0);

        (NativeBattleBadBreath300Frame Frame, int Result) Main(uint address) => AdvanceMain(frameForScan!, address, resources, inputs);
        (NativeBattleBadBreath300Frame Frame, int Result) Target(uint address) => AdvanceTarget(frameForScan!, address, resources, inputs);
        (NativeBattleBadBreath300Frame Frame, int Result) Particle(uint address) => AdvanceParticle(frameForScan!, address, inputs);
        (NativeBattleBadBreath300Frame Frame, int Survivors) Scan(NativeBattleBadBreath300Frame source, int poolIndex, uint callback, Func<uint, (NativeBattleBadBreath300Frame Frame, int Result)> execute)
        {
            NativeBattleBadBreath300Frame scanned = source;
            NativeBattleEffectTaskStep result = source.Effect.Pools[poolIndex].Advance((pool, taskSlot) =>
            {
                frameForScan = WithPool(scanned, poolIndex, pool);
                if (pool.Slots[taskSlot].Callback != callback)
                {
                    throw new NotSupportedException("Bad Breath reached an unsupported child callback.");
                }
                (scanned, int value) = execute(checked(pool.BankAddress + (uint)(taskSlot * pool.Stride)));
                return new(scanned.Effect.Pools[poolIndex], value);
            });
            return (WithPool(scanned, poolIndex, result.Pool), result.Survivors);
        }
    }

    private static NativeBattleModelTransform Transform(NativeBattleGraphicsModel model) => model.Transform ?? throw new NotSupportedException("Bad Breath requires its actual actor transform.");
}
