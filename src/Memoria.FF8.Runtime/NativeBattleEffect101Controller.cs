using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect101Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect101Controller
{
    public static NativeBattleEffect101Frame Initialize(NativeBattleEffect101Frame frame, NativeBattleEffect101Resources resources, NativeBattleEffect101Inputs inputs)
    {
        Require(frame);
        resources.Validate();
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect101 requires its actual native arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect101 requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Effect101 requires its texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        int target = inputs.Packets.ReadContact(FirstContact(frame, inputs)).Primary.ActorIndex;
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteBytes(12, new byte[8]));
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(TargetGlobal, ModelAddress(target)), Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        return inputs.Execute(frame, new NativeBattleEffect101ActorFlags(target, 12, 12));
    }

    public static NativeBattleEffect101Frame Advance(NativeBattleEffect101Frame frame, NativeBattleEffect101Resources resources, NativeBattleEffect101Inputs inputs)
    {
        Require(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 2) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Effect101 requires its actual root and main task pools.");
        }
        NativeBattleEffect101Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != RootCallback)
            {
                throw new NotSupportedException("Effect101 reached an unsupported root callback.");
            }
            bool first = task.ReadDword(16) != 0;
            current = current with { State = current.State.WriteDword(CursorGlobal, checked(Working(current) + (uint)(first ? FirstPacketOffset : SecondPacketOffset))) };
            current = Write(current, RootBank, task.WriteDword(16, first ? 0u : 1u));
            if (task.ReadWord(12) == 1 && task.ReadByte(15) == 0)
            {
                current = Setup(current, resources, inputs);
            }
            bool started = Read(current, RootBank).ReadByte(15) != 0;
            int survivors = 0;
            if (started)
            {
                NativeBattleEffect101Frame scan = current;
                NativeBattleEffectTaskStep children = current.Effect.Pools[1].Advance((owner, child) =>
                {
                    scan = WithPool(scan, 1, owner);
                    uint address = owner.BankAddress + (uint)(child * owner.Stride);
                    if (owner.Slots[child].Callback != MainCallback)
                    {
                        throw new NotSupportedException("Effect101 reached an unsupported main callback.");
                    }
                    (scan, int result) = Main(scan, address, resources, inputs);
                    return new(scan.Effect.Pools[1], result);
                });
                current = WithPool(scan, 1, children.Pool);
                survivors = children.Survivors;
            }
            if (started && survivors == 0)
            {
                current = inputs.Execute(current, new NativeBattleEffect101Stage(0, 0));
                return new(current.Effect.Pools[0], 2);
            }
            NativeBattleModulePoolTask root = Read(current, RootBank);
            current = Write(current, RootBank, root.WriteWord(12, unchecked((ushort)(root.ReadWord(12) + 1))));
            return new(current.Effect.Pools[0], 0);
        });
        current = WithPool(current, 0, roots.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = roots.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static NativeBattleEffect101Frame Setup(NativeBattleEffect101Frame frame, NativeBattleEffect101Resources resources, NativeBattleEffect101Inputs inputs)
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, Working(frame), 0x2D0, 1);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(15, 1));
        frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], main] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [main]) };
        (main, int slot) = main.Append(MainCallback);
        frame = WithPool(frame, 1, main);
        uint address = main.BankAddress + (uint)(slot * main.Stride);
        int target = Actor(frame.State.ReadDword(TargetGlobal));
        NativeBattleGraphicsModel model = inputs.ReadModel(target);
        uint color = model.Color44 ?? throw new NotSupportedException("Effect101 requires its current target colorDWORD44.");
        short scale = (model.Transform ?? throw new NotSupportedException("Effect101 requires its target scaleWORD38.")).Scale38;
        NativeBattleEffect101PositionStep position = inputs.ReadPosition8(frame, target);
        frame = position.Frame;
        NativeBattleModulePoolTask task = Read(frame, address).WriteBytes(12, new byte[0x2C4]).WriteDword(16, unchecked((uint)(int)scale)).WriteDword(20, color);
        task = CopyToTask(task, 24, position.Position8, 8);
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[0x2B0].ToImmutableArray()).Initialize7016B0(resources.MainAct, 0x2B0);
        task = workspace.CopyTo(task, 32, retainUnknown: false);
        frame = Write(frame, address, task);
        NativeBattleVector caster = (inputs.ReadModel(Actor(frame.State.ReadDword(CasterGlobal))).Transform ?? throw new NotSupportedException("Effect101 requires its actual caster position.")).Position28;
        frame = inputs.Execute(frame, new NativeBattleEffect101PositionXYZ(target, new(caster.X, 300, unchecked((short)(caster.Z - 1000)))));
        return inputs.Execute(frame, new NativeBattleEffect101RingSequence(target, 14));
    }

    private static (NativeBattleEffect101Frame Frame, int Result) Main(NativeBattleEffect101Frame frame, uint address, NativeBattleEffect101Resources resources, NativeBattleEffect101Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = unchecked((short)task.ReadWord(12));
        int target = Actor(frame.State.ReadDword(TargetGlobal));
        int caster = Actor(frame.State.ReadDword(CasterGlobal));
        frame = inputs.Execute(frame, new NativeBattleEffect101ScalePointer(target, 0));
        int depth = age < 12 ? unchecked(age * 2200) / 12 : age >= 42 ? unchecked((54 - age) * 2200) / 12 : 2200;
        frame = inputs.Execute(frame, new NativeBattleEffect101Stage(unchecked((short)depth), 0));
        if ((uint)age < 38)
        {
            if (age <= 20)
            {
                short factor = unchecked((short)(4096 - NativeBattleCameraMath.Sine((20 - age) * 1024 / 20)));
                NativeBattleEffect101TintStep tint = NativeBattleEffect101Tint.Execute(frame.Memory, task.ReadDword(20), factor, inputs.Colors);
                frame = frame with { Memory = tint.Memory, State = frame.State.WriteDword(0x2544E18, unchecked((uint)(int)factor)).WriteDword(0x2544E1C, unchecked((uint)(int)factor)).WriteDword(0x2544E20, unchecked((uint)(int)factor)) };
                frame = inputs.Execute(frame, new NativeBattleEffect101Color44(target, tint.Rgb | 0x32000000));
                frame = inputs.Execute(frame, new NativeBattleEffect101ScalePointer(target, 0x2544E18));
                int y = unchecked(300 + ((factor - 4096) * 2700) / 4096);
                frame = inputs.Execute(frame, new NativeBattleEffect101PositionY(target, unchecked((short)y)));
                if (age == 20)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect101Color44(target, task.ReadDword(20)));
                }
            }
            NativeBattleVector actualCaster = (inputs.ReadModel(caster).Transform ?? throw new NotSupportedException("Effect101 requires its current caster position.")).Position28;
            NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(0, BitConverter.GetBytes(actualCaster.X)).Write(2, BitConverter.GetBytes(unchecked((short)-2700))).Write(4, BitConverter.GetBytes(unchecked((short)(actualCaster.Z + 1000)))).Write(8, BitConverter.GetBytes(-128)).Write(12, BitConverter.GetBytes(checked(Working(frame) + 0x2D0)));
            frame = inputs.AdvanceAct(frame, address, 0x20, 0x2B0, resources.MainAct, context).Frame;
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect101Sound(resources.Sound));
        }
        int relative = age - 38;
        if (relative < 16)
        {
            int factor = unchecked(relative << 12) / 16;
            if (factor < 0)
            {
                NativeBattleEffect101WordStep bone = inputs.ReadBodyBone0X(frame, target);
                frame = bone.Frame;
                NativeBattleVector actualCaster = (inputs.ReadModel(caster).Transform ?? throw new NotSupportedException("Effect101 requires its current caster position.")).Position28;
                NativeBattleVector point = new(unchecked((short)(bone.Value + actualCaster.X)), 1200, unchecked((short)(actualCaster.Z - 400)));
                frame = inputs.Execute(frame, new NativeBattleEffect101PositionXYZ(target, point));
                frame = inputs.Execute(frame, new NativeBattleEffect101WorldR9TXYZ(target, new([-4096, 0, 0, 0, 4096, 0, 0, 0, -4096], point.X, point.Y, point.Z, 4096)));
                if (relative == -15)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect101ActorFlags(target, 4, 0));
                }
            }
            else
            {
                frame = inputs.Execute(frame, new NativeBattleEffect101ActorFlags(target, 8, 0));
                NativeBattleEffectArena saved = TaskView(Read(frame, address), 24, 8);
                NativeBattleEffect101PointStep blended = inputs.BlendPosition56CB90(frame, target, saved, unchecked((short)(4096 - factor)), unchecked((short)factor));
                frame = inputs.Execute(blended.Frame, new NativeBattleEffect101PositionXYZ(target, blended.Position));
            }
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        if (next >= 54)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect101Position8(target, TaskView(Read(frame, address), 24, 8)));
            frame = inputs.Execute(frame, new NativeBattleEffect101Color44(target, Read(frame, address).ReadDword(20)));
            frame = inputs.Execute(frame, new NativeBattleEffect101Stage(0, 0));
            return (frame, 2);
        }
        return (frame, 0);
    }
    public static NativeBattleModulePoolTask Read(NativeBattleEffect101Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return pool.Slots[checked((int)((address - pool.BankAddress) / pool.Stride))];
            }
        }
        throw new NotSupportedException("Effect101 task address has no current physical owner.");
    }

    public static NativeBattleEffect101Frame Write(NativeBattleEffect101Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return WithPool(frame, index, pool.WriteSlot(checked((int)((address - pool.BankAddress) / pool.Stride)), task));
            }
        }
        throw new NotSupportedException("Effect101 task write has no current physical owner.");
    }

    private static NativeBattleModulePoolTask CopyToTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }

    private static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, task.ReadRawPayloadSpan().Slice(offset - 12, length));
        if (task.ReadUnknownPayloadRanges() is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                int first = Math.Max(offset, range.Offset);
                int end = Math.Min(offset + length, range.Offset + range.Length);
                if (first < end)
                {
                    view = view.MarkUnknown(first - offset, end - first);
                }
            }
        }
        return view;
    }

    private static void Require(NativeBattleEffect101Frame frame)
    {
        if (frame.Effect.Binding.Effect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect101 requires its selected original binding.");
        }
        frame.State.Validate();
    }

    private static NativeBattleEffect101Frame WithPool(NativeBattleEffect101Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectParameters Parameters(NativeBattleEffect101Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Effect101 requires its captured native parameters.");
    private static int FirstContact(NativeBattleEffect101Frame frame, NativeBattleEffect101Inputs inputs) => inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect101 requires its current physical contacts.");
    private static uint Working(NativeBattleEffect101Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static uint ModelAddress(int actor) => actor is >= 0 and < 7 ? 0x1D972C0u + (uint)(actor * 156) : throw new NotSupportedException("Effect101 selected an absent actor.");
    private static int Actor(uint pointer) => pointer >= 0x1D972C0 && pointer < 0x1D972C0 + 7 * 156 && (pointer - 0x1D972C0) % 156 == 0 ? (int)((pointer - 0x1D972C0) / 156) : throw new NotSupportedException("Effect101 requires its current physical model pointer.");
}
