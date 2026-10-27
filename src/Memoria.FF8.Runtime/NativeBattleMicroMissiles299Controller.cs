using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMicroMissiles299Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMicroMissiles299Controller
{
    public static NativeBattleMicroMissiles299Frame Initialize(NativeBattleMicroMissiles299Frame frame, NativeBattleMicroMissiles299Resources resources, NativeBattleMicroMissiles299Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Micro Missiles requires its executed texture initializer pointer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Micro Missiles requires its texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(frame.Memory.Arena ?? throw new NotSupportedException("Micro Missiles requires its working arena."), tim);
        NativeBattleEffectParameters parameters = Parameters(frame);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(parameters.Actor78)) };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int index) = root.Append(RootCallback);
        root = root.WriteSlot(index, root.Slots[index].WriteDword(12, 0).WriteDword(16, 0));
        return frame with { Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null }, Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) } };
    }

    public static NativeBattleMicroMissiles299Frame Advance(NativeBattleMicroMissiles299Frame frame, NativeBattleMicroMissiles299Resources resources, NativeBattleMicroMissiles299Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 4) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor || frame.Effect.Pools[0].Stride != 20)
        {
            throw new InvalidDataException("Micro Missiles requires its actual root and three child task pools.");
        }
        NativeBattleMicroMissiles299Frame current = frame;
        NativeBattleEffectTaskStep result = current.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            (current, int outcome) = Root(current, slot, resources, inputs);
            return new(current.Effect.Pools[0], outcome);
        });
        current = WithPool(current, 0, result.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = result.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static (NativeBattleMicroMissiles299Frame Frame, int Result) Root(NativeBattleMicroMissiles299Frame frame, int slot, NativeBattleMicroMissiles299Resources resources, NativeBattleMicroMissiles299Inputs inputs)
    {
        NativeBattleModulePoolTask root = Task(frame, 0, slot);
        if (root.Callback != RootCallback)
        {
            throw new NotSupportedException("Micro Missiles root callback is unsupported.");
        }
        uint working = frame.State.ReadDword(WorkingGlobal);
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(working + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Put(frame, 0, slot, root.WriteDword(16, first ? 0u : 1u));
        short age = unchecked((short)root.ReadWord(12));
        if (age == 1)
        {
            NativeBattleEffectTaskPool missiles = NativeBattleEffectTaskPool.Initialize(MissileDescriptor, working + 0x1518, 0x80C, 6);
            NativeBattleEffectTaskPool impacts = NativeBattleEffectTaskPool.Initialize(ImpactDescriptor, working + 0xD80, 0x144, 6);
            NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, working, 0x24, 96);
            frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], missiles, impacts, particles] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [missiles, impacts, particles]) };
            byte caster = (byte)Actor(frame.State.ReadDword(CasterGlobal));
            frame = inputs.Execute(frame, new NativeBattleMicroMissiles299BindCaster(caster, working + BindingOffset, 0x1047120));
            frame = inputs.Execute(frame, new NativeBattleMicroMissiles299RingSequence(caster, 2));
        }
        int birth = age - 10;
        if ((uint)birth < 24 && (birth & 3) == 0)
        {
            frame = SpawnMissile(frame, birth >> 2, resources, inputs);
        }
        int missilesLeft = -1;
        int impactsLeft = -1;
        int particlesLeft = -1;
        NativeBattleMicroMissiles299Frame? frameForScan = null;
        if (age >= 10)
        {
            (frame, missilesLeft) = Scan(frame, 1, address => Missile(frameForScan!, address, resources, inputs));
            if (missilesLeft == 0 && Task(frame, 0, slot).ReadWord(14) == 0)
            {
                frame = Put(frame, 0, slot, Task(frame, 0, slot).WriteWord(14, 1));
            }
            (frame, impactsLeft) = Scan(frame, 2, address => Impact(frameForScan!, address, resources, inputs));
            (frame, particlesLeft) = Scan(frame, 3, address => Particle(frameForScan!, address, inputs));
            if (age == 10)
            {
                frame = inputs.Execute(frame, new NativeBattleMicroMissiles299Sound(resources.Sound, 128));
            }
        }
        if (Task(frame, 0, slot).ReadWord(14) != 0 && missilesLeft == 0 && impactsLeft == 0 && particlesLeft == 0)
        {
            return (frame, 2);
        }
        return (Put(frame, 0, slot, Task(frame, 0, slot).WriteWord(12, unchecked((ushort)(age + 1)))), 0);

        (NativeBattleMicroMissiles299Frame Frame, int Survivors) Scan(NativeBattleMicroMissiles299Frame source, int poolIndex, Func<uint, (NativeBattleMicroMissiles299Frame Frame, int Result)> callback)
        {
            NativeBattleMicroMissiles299Frame scanned = source;
            NativeBattleEffectTaskStep scan = source.Effect.Pools[poolIndex].Advance((pool, taskSlot) =>
            {
                frameForScan = WithPool(scanned, poolIndex, pool);
                uint address = checked(pool.BankAddress + (uint)(taskSlot * pool.Stride));
                (scanned, int result) = callback(address);
                return new(scanned.Effect.Pools[poolIndex], result);
            });
            return (WithPool(scanned, poolIndex, scan.Pool), scan.Survivors);
        }
    }

    private static NativeBattleMicroMissiles299Frame SpawnMissile(NativeBattleMicroMissiles299Frame frame, int index, NativeBattleMicroMissiles299Resources resources, NativeBattleMicroMissiles299Inputs inputs)
    {
        int caster = Actor(frame.State.ReadDword(CasterGlobal));
        int target = inputs.Packets.ReadContact(FirstContact(frame, inputs)).Primary.ActorIndex;
        NativeBattleMicroMissiles299AnchorStep anchor = inputs.ReadAnchor571400(frame, caster);
        frame = anchor.Frame;
        NativeBattleGraphicsModel casterModel = inputs.ReadModel(caster);
        int yaw = -(casterModel.Rotation14 ?? throw new NotSupportedException("Micro Missiles requires its caster yaw."));
        short x = unchecked((short)(anchor.Anchor.X + (unchecked(NativeBattleCameraMath.Sine(yaw) * 300) >> 12)));
        short z = unchecked((short)(anchor.Anchor.Z + (unchecked(-NativeBattleCameraMath.Cosine(yaw) * 300) >> 12)));
        short y = unchecked((short)((casterModel.Transform ?? throw new NotSupportedException("Micro Missiles requires its caster position WORDs.")).Angle36 - 620));
        (frame, uint address) = Append(frame, 1, MissileCallback);
        if (address == 0)
        {
            throw new NotSupportedException("Micro Missiles constructor reached a full native missile pool.");
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(0x24, unchecked((ushort)x)).WriteWord(0x26, unchecked((ushort)y)).WriteWord(0x28, unchecked((ushort)z)).WriteWord(0x2A, 204).WriteWord(0x2C, unchecked((ushort)x)).WriteWord(0x2E, unchecked((ushort)y)).WriteWord(0x30, unchecked((ushort)z)).WriteWord(0x32, 204);
        frame = Write(frame, address, task);
        NativeBattleMicroMissiles299BoneStep bone;
        do
        {
            frame = DrawRandom(frame, out ushort random);
            NativeBattlePoseSource pose = inputs.ReadPoseSource(target);
            int selected = random * pose.BoneCount >> 15;
            bone = NativeBattleMicroMissiles299Bone.Query67E550(frame, inputs.ReadModel(target), pose, selected);
            frame = bone.Frame;
        }
        while (bone.Result != 0);
        NativeBattleVector endpoint = bone.Point ?? throw new InvalidDataException("Micro Missiles accepted bone query has no output.");
        ushort variant;
        if (index % 3 == 1)
        {
            frame = DrawRandom(frame, out ushort rx);
            frame = DrawRandom(frame, out ushort rz);
            endpoint = new(unchecked((short)(endpoint.X + (rx & 2047) - 1024)), (inputs.ReadModel(target).Transform ?? throw new NotSupportedException("Micro Missiles requires its target WORD24.")).Angle36, unchecked((short)(endpoint.Z + (rz & 2047) - 1024)));
            variant = 2;
        }
        else
        {
            variant = (ushort)(index == 5 ? 1 : 0);
        }
        (NativeBattleActionMatrix basis, int length) = NativeBattleFunguarLaser277.Basis50CBA0(x - endpoint.X, endpoint.Y - y, endpoint.Z - z);
        int angle = index * 256 - 640;
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        NativeBattleActionMatrix roll = new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix rotated = NativeBattleGfMatrices.Multiply56C090(basis, roll);
        task = Read(frame, address).WriteWord(0x20, unchecked((ushort)length)).WriteWord(0x22, variant).WriteWord(0x1E, 0);
        task = WriteRotation(task, 0xC, rotated);
        task = new NativeBattleGfAnimationWorkspace(task.ReadBytes(0x34, 0x7D8).ToImmutableArray()).Initialize7016B0(resources.Missile, 0x7D8).CopyTo(task, 0x34, retainUnknown: false);
        return Write(frame, address, task);
    }

    private static NativeBattleMicroMissiles299Frame DrawRandom(NativeBattleMicroMissiles299Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleEffectParameters Parameters(NativeBattleMicroMissiles299Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Micro Missiles requires its captured action parameters.");
    private static int FirstContact(NativeBattleMicroMissiles299Frame frame, NativeBattleMicroMissiles299Inputs inputs)
    {
        int index = Parameters(frame).HeaderIndex7C;
        if ((uint)index >= inputs.Packets.Headers.Length || inputs.Packets.Headers[index].ReadByte(16) == 0)
        {
            throw new InvalidDataException("Micro Missiles requires its first captured target contact.");
        }
        return inputs.Packets.Headers[index].FirstContactIndex ?? throw new NotSupportedException("Micro Missiles requires its actual captured contact pointer.");
    }
    private static void RequireBinding(NativeBattleMicroMissiles299Frame frame)
    {
        if (frame.Effect.RequestedEffect != 299 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Micro Missiles requires its original selected entry.");
        }
    }
    private static int Actor(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Micro Missiles actor pointer leaves the native seven-model bank.");
        }
        return (int)(offset / 156);
    }
    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)actor * 156) : throw new InvalidDataException("Micro Missiles actor leaves the native model bank.");
    private static NativeBattleModulePoolTask Task(NativeBattleMicroMissiles299Frame frame, int pool, int slot) => frame.Effect.Pools[pool].Slots[slot];
    private static NativeBattleMicroMissiles299Frame WithPool(NativeBattleMicroMissiles299Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleMicroMissiles299Frame Put(NativeBattleMicroMissiles299Frame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool].WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, Task(frame, pool, slot).Next, task.Callback)));
    public static NativeBattleModulePoolTask Read(NativeBattleMicroMissiles299Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length) && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Micro Missiles task pointer leaves its canonical pools.");
    }
    public static NativeBattleMicroMissiles299Frame Write(NativeBattleMicroMissiles299Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int pool = 0; pool < frame.Effect.Pools.Length; pool++)
        {
            NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
            if (address >= owner.BankAddress && address - owner.BankAddress < (uint)(owner.Stride * owner.Slots.Length) && (address - owner.BankAddress) % owner.Stride == 0)
            {
                return Put(frame, pool, (int)((address - owner.BankAddress) / owner.Stride), task);
            }
        }
        throw new InvalidDataException("Micro Missiles task pointer leaves its canonical pools.");
    }
    private static (NativeBattleMicroMissiles299Frame Frame, uint Address) Append(NativeBattleMicroMissiles299Frame frame, int pool, uint callback)
    {
        NativeBattleEffectTaskPool owner = frame.Effect.Pools[pool];
        if (!owner.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return (frame, 0);
        }
        (owner, int slot) = owner.Append(callback);
        return (WithPool(frame, pool, owner), checked(owner.BankAddress + (uint)(slot * owner.Stride)));
    }
    private static NativeBattleModulePoolTask WriteRotation(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            task = task.WriteWord(offset + lane * 2, unchecked((ushort)matrix.Rotation[lane]));
        }
        return task;
    }
}
