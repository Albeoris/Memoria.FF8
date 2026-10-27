using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleHypnotize133Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleHypnotize133Controller
{
    public static NativeBattleHypnotize133Frame Initialize(NativeBattleHypnotize133Frame frame, NativeBattleHypnotize133Resources resources, NativeBattleHypnotize133Inputs inputs)
    {
        Require(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Hypnotize requires its existing native arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Hypnotize requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Hypnotize requires its texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteBytes(12, new byte[8]));
        NativeBattleHypnotize133State state = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78));
        return frame with { State = state, Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattleHypnotize133Frame Advance(NativeBattleHypnotize133Frame frame, NativeBattleHypnotize133Resources resources, NativeBattleHypnotize133Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 5) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Hypnotize requires its actual root and four child pools.");
        }
        NativeBattleHypnotize133Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != RootCallback)
            {
                throw new NotSupportedException("Hypnotize reached an unsupported root callback.");
            }
            bool first = task.ReadDword(16) != 0;
            current = current with { State = current.State.WriteDword(CursorGlobal, checked(Working(current) + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
            current = Write(current, pool.BankAddress, task.WriteDword(16, first ? 0u : 1u));
            if (task.ReadWord(12) == 1 && task.ReadByte(15) == 0)
            {
                current = Setup(current, resources, inputs);
            }
            bool started = Read(current, pool.BankAddress).ReadByte(15) != 0;
            int survivors = 0;
            if (started)
            {
                for (int index = 1; index < 5; index++)
                {
                    int selected = index;
                    NativeBattleHypnotize133Frame scan = current;
                    NativeBattleEffectTaskStep children = current.Effect.Pools[index].Advance((owner, child) =>
                    {
                        scan = WithPool(scan, selected, owner);
                        uint address = owner.BankAddress + (uint)(child * owner.Stride);
                        (scan, int result) = Child(scan, address, resources, inputs);
                        return new(scan.Effect.Pools[selected], result);
                    });
                    current = WithPool(scan, index, children.Pool);
                    survivors |= children.Survivors;
                }
            }
            if (started && survivors == 0)
            {
                return new(current.Effect.Pools[0], 2);
            }
            NativeBattleModulePoolTask root = current.Effect.Pools[0].Slots[slot];
            current = Write(current, pool.BankAddress, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1))));
            return new(current.Effect.Pools[0], 0);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static NativeBattleHypnotize133Frame Setup(NativeBattleHypnotize133Frame frame, NativeBattleHypnotize133Resources resources, NativeBattleHypnotize133Inputs inputs)
    {
        uint working = Working(frame);
        NativeBattleEffectTaskPool main = Pool(frame, 0x2520F40, working + 0xD40, 0xC4, 1);
        NativeBattleEffectTaskPool anchored = Pool(frame, 0x2520F30, working + 0x7C0, 0xB0, 8);
        NativeBattleEffectTaskPool fading = Pool(frame, 0x2520F20, working + 0x280, 0x54, 16);
        NativeBattleEffectTaskPool sprites = Pool(frame, 0x2520F10, working, 0x14, 32);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(15, 1));
        frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main, anchored, fading, sprites] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [main, anchored, fading, sprites]) };
        (frame, uint address) = Append(frame, 1, MainCallback);
        NativeBattleModulePoolTask task = Read(frame, address).WriteBytes(12, new byte[0xB8]);
        int count = Header(frame, inputs).ReadByte(16);
        if (count > 3)
        {
            throw new NotSupportedException("Hypnotize contacts exceed its three physical actor pointers.");
        }
        task = task.WriteWord(14, checked((ushort)count));
        for (int index = 0; index < count; index++)
        {
            task = task.WriteDword(16 + index * 4, ModelAddress(inputs.Packets.ReadContact(FirstContact(frame, inputs) + index).Primary.ActorIndex));
        }
        frame = Write(frame, address, task);
        return InitializeAct(frame, address, 0x1C, 0xA8, resources.MainAct);
    }

    private static (NativeBattleHypnotize133Frame Frame, int Result) Child(NativeBattleHypnotize133Frame frame, uint address, NativeBattleHypnotize133Resources resources, NativeBattleHypnotize133Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback == MainCallback)
        {
            return Main(frame, address, resources, inputs);
        }
        if (task.Callback == 0x6B1740)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Hypnotize requires its actual temporary arena."), 180);
            frame = frame with { Staging = allocation.Staging };
            frame = inputs.Execute(frame, new NativeBattleHypnotize133DrawSprite180(address, allocation.Address));
            frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging ?? throw new InvalidDataException("Hypnotize lost its temporary arena."), 180) };
            short phase = unchecked((short)(Read(frame, address).ReadWord(18) + 1));
            frame = Write(frame, address, Read(frame, address).WriteWord(18, unchecked((ushort)phase)));
            return (frame, phase >= 14 ? 2 : 0);
        }
        NativeBattleHypnotize133ContextKind kind;
        int size;
        if (task.Callback == 0x6B17C0)
        {
            kind = NativeBattleHypnotize133ContextKind.Anchored;
            size = 0x9C;
        }
        else if (task.Callback == 0x6B1900)
        {
            kind = NativeBattleHypnotize133ContextKind.Fading;
            size = 0x40;
        }
        else
        {
            throw new NotSupportedException("Hypnotize reached an unsupported child callback.");
        }
        NativeBattleGfAnimationSource source = resources.Act(WorkspaceSource(task, 0x14));
        NativeBattleHypnotize133ContextStep context = inputs.PrepareActContext(frame, address, kind);
        NativeBattleHypnotize133ActStep advanced = inputs.AdvanceAct(context.Frame, address, 0x14, size, source, context.Context40);
        return (advanced.Frame, advanced.Remaining == 0 ? 2 : 0);
    }

    private static (NativeBattleHypnotize133Frame Frame, int Result) Main(NativeBattleHypnotize133Frame frame, uint address, NativeBattleHypnotize133Resources resources, NativeBattleHypnotize133Inputs inputs)
    {
        short age = unchecked((short)Read(frame, address).ReadWord(12));
        if ((ushort)age < 30)
        {
            NativeBattleHypnotize133ContextStep context = inputs.PrepareActContext(frame, address, NativeBattleHypnotize133ContextKind.Main);
            frame = inputs.AdvanceAct(context.Frame, address, 0x1C, 0xA8, resources.MainAct, context.Context40).Frame;
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleHypnotize133Sound(resources.Sound));
        }
        if ((uint)age < 16 && (age & 1) == 0)
        {
            (frame, uint child) = Append(frame, 2, 0x6B17C0);
            if (child != 0)
            {
                int index = age >> 1;
                NativeBattleModuleAnchor anchor = resources.AnchoredOffsets[index];
                frame = Write(frame, child, Read(frame, child).WriteWord(12, unchecked((ushort)anchor.X)).WriteWord(14, unchecked((ushort)anchor.Y)).WriteWord(16, unchecked((ushort)anchor.Z)).WriteWord(18, unchecked((ushort)anchor.W)));
                frame = InitializeAct(frame, child, 0x14, 0x9C, resources.AnchoredActs[index]);
            }
        }
        int relative = age - 2;
        if ((uint)relative < 21 && (relative & 1) == 0)
        {
            (frame, uint child) = Append(frame, 3, 0x6B1900);
            if (child != 0)
            {
                frame = Random(frame, out ushort actorRoll);
                int actor = SelectedActor(frame, address, actorRoll);
                frame = Random(frame, out ushort distanceRoll);
                frame = Random(frame, out ushort boneRoll);
                byte count = inputs.ReadBoneCount(actor);
                NativeBattleHypnotize133PointStep point = inputs.ReadBone502170(frame, actor, checked((byte)((boneRoll * count) >> 15)), unchecked((short)(distanceRoll >> 3)));
                frame = point.Frame;
                frame = Random(frame, out ushort jitter);
                frame = Random(frame, out ushort factor);
                NativeBattleVector position = point.Point with { X = unchecked((short)(point.Point.X + (jitter & 0x7FF) - 0x400)) };
                frame = Write(frame, child, Vector(Read(frame, child), 12, position).WriteWord(18, checked((ushort)((factor >> 4) + 0x800))));
                frame = InitializeAct(frame, child, 0x14, 0x40, resources.FadingActs[(relative >> 1) & 7]);
            }
        }
        relative = age - 18;
        if ((uint)relative < 24 && (relative & 1) == 0)
        {
            (frame, uint child) = Append(frame, 4, 0x6B1740);
            if (child != 0)
            {
                frame = Random(frame, out ushort actorRoll);
                int actor = SelectedActor(frame, address, actorRoll);
                NativeBattleHypnotize133PointStep point = inputs.ReadBone502170(frame, actor, 0xF0, 0);
                frame = Random(point.Frame, out ushort x);
                frame = Random(frame, out ushort y);
                frame = Random(frame, out ushort z);
                NativeBattleVector position = new(unchecked((short)(point.Point.X + (x & 0x1FF) - 0x100)), unchecked((short)(point.Point.Y + (y & 0x1FF) - 0x180)), unchecked((short)(point.Point.Z + (z & 0x1FF) - 0x100)));
                frame = Write(frame, child, Vector(Read(frame, child), 12, position).WriteWord(18, 0));
            }
        }
        if (age == 34)
        {
            frame = inputs.Execute(frame, new NativeBattleHypnotize133BulkContact(FirstContact(frame, inputs), Header(frame, inputs).ReadByte(16)));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        return (frame, next >= 42 ? 2 : 0);
    }

    private static NativeBattleHypnotize133Frame InitializeAct(NativeBattleHypnotize133Frame frame, uint address, int offset, int size, NativeBattleGfAnimationSource source)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, offset, size).Initialize7016B0(source, size);
        return Write(frame, address, NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, offset, workspace));
    }

    private static uint WorkspaceSource(NativeBattleModulePoolTask task, int offset) => task.ReadDword(offset);

    private static NativeBattleEffectTaskPool Pool(NativeBattleHypnotize133Frame frame, uint descriptor, uint bank, ushort stride, ushort count)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Hypnotize requires its current task backing arena.");
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

    public static NativeBattleModulePoolTask Read(NativeBattleHypnotize133Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return pool.Slots[checked((int)((address - pool.BankAddress) / pool.Stride))];
            }
        }
        throw new NotSupportedException("Hypnotize task address has no current physical owner.");
    }

    public static NativeBattleHypnotize133Frame Write(NativeBattleHypnotize133Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return WithPool(frame, index, pool.WriteSlot(checked((int)((address - pool.BankAddress) / pool.Stride)), task));
            }
        }
        throw new NotSupportedException("Hypnotize task write has no current physical owner.");
    }

    private static (NativeBattleHypnotize133Frame Frame, uint Address) Append(NativeBattleHypnotize133Frame frame, int index, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return (frame, 0);
        }
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, index, pool), pool.BankAddress + (uint)(slot * pool.Stride));
    }

    private static int SelectedActor(NativeBattleHypnotize133Frame frame, uint address, ushort roll)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int index = (roll * unchecked((short)task.ReadWord(14))) >> 15;
        return Actor(task.ReadDword(16 + index * 4));
    }

    private static NativeBattleHypnotize133Frame Random(NativeBattleHypnotize133Frame frame, out ushort value)
    {
        NativeCrtRandomState next = frame.Random.Draw(out value);
        return frame with { Random = next };
    }

    private static void Require(NativeBattleHypnotize133Frame frame)
    {
        if (frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Hypnotize requires its selected original binding.");
        }
        frame.State.Validate();
    }

    private static NativeBattleHypnotize133Frame WithPool(NativeBattleHypnotize133Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleHypnotize133Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Hypnotize requires its captured native parameters.");
    private static NativeBattleActionHeaderSlot Header(NativeBattleHypnotize133Frame frame, NativeBattleHypnotize133Inputs inputs) => inputs.Packets.Headers[Parameters(frame).HeaderIndex7C];
    private static int FirstContact(NativeBattleHypnotize133Frame frame, NativeBattleHypnotize133Inputs inputs) => Header(frame, inputs).FirstContactIndex ?? throw new NotSupportedException("Hypnotize requires its current physical contacts.");
    private static uint Working(NativeBattleHypnotize133Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static uint ModelAddress(int actor) => actor is >= 0 and < 7 ? 0x1D972C0u + (uint)(actor * 156) : throw new NotSupportedException("Hypnotize selected an absent actor.");
    private static int Actor(uint pointer) => pointer >= 0x1D972C0 && pointer < 0x1D972C0 + 7 * 156 && (pointer - 0x1D972C0) % 156 == 0 ? (int)((pointer - 0x1D972C0) / 156) : throw new NotSupportedException("Hypnotize requires its current physical model pointer.");
    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
}
