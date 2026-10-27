using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBeamLaser134Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBeamLaser134Controller
{
    public static NativeBattleBeamLaser134Frame Initialize(NativeBattleBeamLaser134Frame frame, NativeBattleBeamLaser134Resources resources, NativeBattleBeamLaser134Inputs inputs)
    {
        Require(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Beam Laser requires its actual native arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Beam Laser requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Beam Laser requires its texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        int caster = Parameters(frame).Actor78;
        ushort saved = inputs.ReadModel(caster).Flags0;
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteBytes(12, new byte[8]));
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(caster)).WriteDword(SavedCasterFlagsGlobal, saved), Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        return inputs.Execute(frame, new NativeBattleBeamLaser134ActorFlags(caster, 8, 8));
    }

    public static NativeBattleBeamLaser134Frame Advance(NativeBattleBeamLaser134Frame frame, NativeBattleBeamLaser134Resources resources, NativeBattleBeamLaser134Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 5) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Beam Laser requires its original root and four child pools.");
        }
        NativeBattleBeamLaser134Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != RootCallback)
            {
                throw new NotSupportedException("Beam Laser reached an unsupported root callback.");
            }
            bool first = task.ReadDword(16) != 0;
            current = current with { State = current.State.WriteDword(CursorGlobal, checked(Working(current) + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
            current = Write(current, RootBank, task.WriteDword(16, first ? 0u : 1u));
            int caster = Actor(current.State.ReadDword(CasterGlobal));
            current = inputs.Execute(current, new NativeBattleBeamLaser134ActorFlags(caster, 8, 8));
            if (task.ReadWord(12) == 1)
            {
                if (task.ReadByte(15) == 0)
                {
                    current = Setup(current);
                }
                current = TryMain(current, resources, inputs);
            }
            bool started = Read(current, RootBank).ReadByte(15) != 0;
            int survivors = 0;
            if (started)
            {
                for (int index = 1; index < 5; index++)
                {
                    int selected = index;
                    NativeBattleBeamLaser134Frame scan = current;
                    NativeBattleEffectTaskStep children = current.Effect.Pools[index].Advance((owner, child) =>
                    {
                        scan = WithPool(scan, selected, owner);
                        (scan, int result) = Child(scan, owner.BankAddress + (uint)(child * owner.Stride), resources, inputs);
                        return new(scan.Effect.Pools[selected], result);
                    });
                    current = WithPool(scan, index, children.Pool);
                    survivors |= children.Survivors;
                }
            }
            if (started && survivors == 0)
            {
                ushort saved = unchecked((ushort)current.State.ReadDword(SavedCasterFlagsGlobal));
                current = inputs.Execute(current, new NativeBattleBeamLaser134ActorFlags(caster, 8, (ushort)(saved & 8)));
                return new(current.Effect.Pools[0], 2);
            }
            NativeBattleModulePoolTask root = Read(current, RootBank);
            current = Write(current, RootBank, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1))));
            return new(current.Effect.Pools[0], 0);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static NativeBattleBeamLaser134Frame Setup(NativeBattleBeamLaser134Frame frame)
    {
        uint working = Working(frame);
        NativeBattleEffectTaskPool main = Pool(frame, 0x2520EC8, working + 0x720, 0x540, 1);
        NativeBattleEffectTaskPool stationary = Pool(frame, 0x2520EB8, working + 0x5E0, 0x14, 16);
        NativeBattleEffectTaskPool moving = Pool(frame, 0x2520EA8, working + 0x420, 0x1C, 16);
        NativeBattleEffectTaskPool children = Pool(frame, 0x2520E98, working, 0x84, 8);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(15, 1));
        return frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, stationary, moving, children] }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [main, stationary, moving, children]) };
    }

    private static NativeBattleBeamLaser134Frame TryMain(NativeBattleBeamLaser134Frame frame, NativeBattleBeamLaser134Resources resources, NativeBattleBeamLaser134Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, RootBank);
        int ordinal = root.ReadByte(14);
        if (ordinal > Header(frame, inputs).ReadByte(17))
        {
            return frame;
        }
        int headerIndex = checked(Parameters(frame).HeaderIndex7C + ordinal);
        if ((uint)headerIndex >= inputs.Packets.Headers.Length)
        {
            throw new NotSupportedException("Beam Laser selected an absent captured action header.");
        }
        int contact = inputs.Packets.Headers[headerIndex].FirstContactIndex ?? throw new NotSupportedException("Beam Laser requires its selected physical contact pointer.");
        int actor = inputs.Packets.ReadContact(contact).Primary.ActorIndex;
        uint pointer = ModelAddress(actor);
        NativeBattleModulePoolTask existing = frame.Effect.Pools[1].Slots[0];
        if ((existing.Flags & 1) != 0 && existing.ReadDword(16) == pointer)
        {
            return Write(frame, RootBank, root.WriteWord(12, 0));
        }
        (frame, uint address) = Append(frame, 1, MainCallback);
        if (address == 0)
        {
            return frame;
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteBytes(12, new byte[0x534]).WriteWord(14, checked((ushort)ordinal)).WriteDword(16, pointer);
        frame = Write(frame, address, task);
        NativeBattleBeamLaser134PointStep point = inputs.ReadBone502170(frame, actor, 0xF1, 0);
        frame = Write(point.Frame, address, Vector(Read(point.Frame, address), 20, point.Point));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(14, unchecked((byte)(ordinal + 1))));
        int[] offsets = [0x1C, 0x74, 0xCC, 0x2EC, 0x390];
        int[] sizes = [0x58, 0x58, 0x220, 0xA4, 0x1B0];
        uint[] sources = [0x12038DC, 0x12038DC, 0x1205270, 0x12065F0, 0x1204004];
        for (int index = 0; index < offsets.Length; index++)
        {
            frame = InitializeAct(frame, address, offsets[index], sizes[index], resources.Act(sources[index]));
        }
        return frame;
    }

    private static (NativeBattleBeamLaser134Frame Frame, int Result) Child(NativeBattleBeamLaser134Frame frame, uint address, NativeBattleBeamLaser134Resources resources, NativeBattleBeamLaser134Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback == MainCallback)
        {
            return Main(frame, address, resources, inputs);
        }
        if (task.Callback == 0x6B0D30)
        {
            NativeBattleBeamLaser134ContextStep context = inputs.PrepareChildContext(frame, address);
            NativeBattleBeamLaser134ActStep advanced = inputs.AdvanceAct(context.Frame, address, 0x2C, 0x58, resources.Act(0x1207150), context.Context40);
            return (advanced.Frame, advanced.Remaining == 0 ? 2 : 0);
        }
        bool moving = task.Callback == 0x6B0D90;
        if (!moving && task.Callback != 0x6B0E30)
        {
            throw new NotSupportedException("Beam Laser reached an unsupported child callback.");
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Beam Laser requires its actual temporary arena."), 180);
        frame = inputs.Execute(frame with { Staging = allocation.Staging }, new NativeBattleBeamLaser134DrawSprite180(address, allocation.Address, moving));
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging ?? throw new InvalidDataException("Beam Laser lost its temporary arena."), 180) };
        task = Read(frame, address);
        if (moving)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                task = task.WriteWord(12 + axis * 2, unchecked((ushort)(task.ReadWord(12 + axis * 2) + task.ReadWord(20 + axis * 2))));
            }
        }
        short phase = unchecked((short)(task.ReadWord(18) + 1));
        frame = Write(frame, address, task.WriteWord(18, unchecked((ushort)phase)));
        return (frame, phase >= 16 ? 2 : 0);
    }
    private static NativeBattleBeamLaser134Frame InitializeAct(NativeBattleBeamLaser134Frame frame, uint address, int offset, int size, NativeBattleGfAnimationSource source)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[size].ToImmutableArray()).Initialize7016B0(source, size);
        return Write(frame, address, task.WriteBytes(offset, workspace.Data.AsSpan()));
    }

    private static uint WorkspaceSource(NativeBattleModulePoolTask task, int offset) => task.ReadDword(offset);

    private static NativeBattleEffectTaskPool Pool(NativeBattleBeamLaser134Frame frame, uint descriptor, uint bank, ushort stride, ushort count)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Beam Laser requires its current task backing arena.");
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, bank, stride, count);
        for (int slot = 0; slot < count; slot++)
        {
            (byte[] data, bool[] known) = arena.ReadSnapshot(checked((int)(bank - ArenaAddress) + slot * stride + 12), stride - 12);
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteBytes(12, data);
            for (int index = 0; index < known.Length; index++)
            {
                if (!known[index])
                {
                    task = task.MarkPayloadUnknown(12 + index, 1);
                }
            }
            pool = pool.WriteSlot(slot, task);
        }
        return pool;
    }

    public static NativeBattleModulePoolTask Read(NativeBattleBeamLaser134Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return pool.Slots[checked((int)((address - pool.BankAddress) / pool.Stride))];
            }
        }
        throw new NotSupportedException("Beam Laser task address has no current physical owner.");
    }

    public static NativeBattleBeamLaser134Frame Write(NativeBattleBeamLaser134Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return WithPool(frame, index, pool.WriteSlot(checked((int)((address - pool.BankAddress) / pool.Stride)), task));
            }
        }
        throw new NotSupportedException("Beam Laser task write has no current physical owner.");
    }

    private static (NativeBattleBeamLaser134Frame Frame, uint Address) Append(NativeBattleBeamLaser134Frame frame, int index, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, index, pool), pool.BankAddress + (uint)(slot * pool.Stride));
    }

    private static NativeBattleBeamLaser134Frame Random(NativeBattleBeamLaser134Frame frame, out ushort value)
    {
        NativeCrtRandomState next = frame.Random.Draw(out value);
        return frame with { Random = next };
    }

    private static void Require(NativeBattleBeamLaser134Frame frame)
    {
        if (frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Beam Laser requires its selected original binding.");
        }
        frame.State.Validate();
    }

    private static NativeBattleBeamLaser134Frame WithPool(NativeBattleBeamLaser134Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleBeamLaser134Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Beam Laser requires its captured native parameters.");
    private static NativeBattleActionHeaderSlot Header(NativeBattleBeamLaser134Frame frame, NativeBattleBeamLaser134Inputs inputs) => inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
    private static int FirstContact(NativeBattleBeamLaser134Frame frame, NativeBattleBeamLaser134Inputs inputs) => Header(frame, inputs).FirstContactIndex ?? throw new NotSupportedException("Beam Laser requires its current physical contacts.");
    private static uint Working(NativeBattleBeamLaser134Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static uint ModelAddress(int actor) => actor is >= 0 and < 7 ? 0x1D972C0u + (uint)(actor * 156) : throw new NotSupportedException("Beam Laser selected an absent actor.");
    private static int Actor(uint pointer) => pointer >= 0x1D972C0 && pointer < 0x1D972C0 + 7 * 156 && (pointer - 0x1D972C0) % 156 == 0 ? (int)((pointer - 0x1D972C0) / 156) : throw new NotSupportedException("Beam Laser requires its current physical model pointer.");
    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
}
