using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattlePetrifyStare127Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePetrifyStare127Controller
{
    public static NativeBattlePetrifyStare127Frame Initialize(NativeBattlePetrifyStare127Frame frame, NativeBattlePetrifyStare127Resources resources, NativeBattlePetrifyStare127Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        _ = FirstContact(frame, inputs);
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Petrify Stare requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Petrify Stare requires its actual texture queue.");
        (textures, _) = textures.EnqueueArenaTim505E30(Arena(frame), tim);
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteBytes(12, new byte[8]));
        NativeBattlePetrifyStare127State state = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(WorkingGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78));
        return frame with { State = state, Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
    }

    public static NativeBattlePetrifyStare127Frame Advance(NativeBattlePetrifyStare127Frame frame, NativeBattlePetrifyStare127Resources resources, NativeBattlePetrifyStare127Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 2) || frame.Effect.Pools[0].DescriptorAddress != RootDescriptor)
        {
            throw new InvalidDataException("Petrify Stare requires its original root and target pools.");
        }
        NativeBattlePetrifyStare127Frame current = frame;
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = WithPool(current, 0, pool);
            if (pool.Slots[slot].Callback != RootCallback)
            {
                throw new NotSupportedException("Petrify Stare reached an unsupported root callback.");
            }
            (current, int result) = Root(current, slot, resources, inputs);
            return new(current.Effect.Pools[0], result);
        });
        current = WithPool(current, 0, advanced.Pool);
        return current with { Effect = current.Effect with { ReturnedDescriptor = advanced.Survivors == 0 ? 0u : RootDescriptor } };
    }

    private static (NativeBattlePetrifyStare127Frame Frame, int Result) Root(NativeBattlePetrifyStare127Frame frame, int slot, NativeBattlePetrifyStare127Resources resources, NativeBattlePetrifyStare127Inputs inputs)
    {
        NativeBattleModulePoolTask root = frame.Effect.Pools[0].Slots[slot];
        bool first = root.ReadDword(16) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(Working(frame) + (uint)(first ? FirstPacketOffset : SecondPacketOffset))).WriteDword(StageDepthGlobal, 0) };
        frame = Put(frame, 0, slot, root.WriteDword(16, first ? 0u : 1u));
        if (root.ReadWord(12) == 1)
        {
            if (root.ReadByte(15) == 0)
            {
                frame = Put(frame, 0, slot, frame.Effect.Pools[0].Slots[slot].WriteByte(15, 1));
                NativeBattleEffectTaskPool targets = NativeBattleEffectTaskPool.Initialize(TargetDescriptor, Working(frame), TargetStride, 1);
                frame = frame with { Effect = frame.Effect with { Pools = [frame.Effect.Pools[0], targets] }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [targets]) };
            }
            int ordinal = frame.Effect.Pools[0].Slots[slot].ReadByte(14);
            NativeBattleEffectTaskPool targetPool = frame.Effect.Pools[1];
            if (ordinal <= inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].ReadByte(17) && targetPool.Slots.Any(task => (task.Flags & 1) == 0))
            {
                int actor = inputs.Packets.ReadContact(FirstContact(frame, inputs) + ordinal).Primary.ActorIndex;
                (targetPool, int targetSlot) = targetPool.Append(TargetCallback);
                NativeBattleModulePoolTask target = targetPool.Slots[targetSlot].WriteBytes(12, new byte[TargetStride - 12]).WriteWord(14, checked((ushort)ordinal)).WriteDword(0x20, ModelAddress(actor));
                NativeBattleGfAnimationWorkspace firstAct = NativeBattleGfAnimationWorkspace.Borrow(target, 0x24, 0x1C8).Initialize7016B0(resources.FirstAct, 0x1C8);
                NativeBattleGfAnimationWorkspace secondAct = NativeBattleGfAnimationWorkspace.Borrow(target, 0x1EC, 0x2D0).Initialize7016B0(resources.SecondAct, 0x2D0);
                target = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(NativeBattleEffectTaskMemory.PublishAnimationWorkspace(target, 0x24, firstAct), 0x1EC, secondAct);
                frame = WithPool(frame, 1, targetPool.WriteSlot(targetSlot, target));
                frame = Put(frame, 0, slot, frame.Effect.Pools[0].Slots[slot].WriteByte(14, unchecked((byte)(ordinal + 1))));
            }
        }
        int survivors = 0;
        if (frame.Effect.Pools[0].Slots[slot].ReadByte(15) != 0)
        {
            NativeBattlePetrifyStare127Frame current = frame;
            NativeBattleEffectTaskStep scanned = frame.Effect.Pools[1].Advance((pool, targetSlot) =>
            {
                current = WithPool(current, 1, pool);
                (current, int result) = Target(current, checked(pool.BankAddress + (uint)(targetSlot * pool.Stride)), resources, inputs);
                return new(current.Effect.Pools[1], result);
            });
            frame = WithPool(current, 1, scanned.Pool);
            survivors = scanned.Survivors;
        }
        frame = inputs.Execute(frame, new NativeBattlePetrifyStare127StageClear());
        root = frame.Effect.Pools[0].Slots[slot];
        if (root.ReadByte(15) != 0 && survivors == 0)
        {
            return (frame, 2);
        }
        short cycle = unchecked((short)(root.ReadWord(12) + 1));
        if (cycle >= 10)
        {
            cycle = 0;
        }
        return (Put(frame, 0, slot, root.WriteWord(12, unchecked((ushort)cycle))), 0);
    }

    private static (NativeBattlePetrifyStare127Frame Frame, int Result) Target(NativeBattlePetrifyStare127Frame frame, uint address, NativeBattlePetrifyStare127Resources resources, NativeBattlePetrifyStare127Inputs inputs)
    {
        NativeBattleModulePoolTask target = Read(frame, address);
        if (target.Callback != TargetCallback)
        {
            throw new NotSupportedException("Petrify Stare reached an unsupported target callback.");
        }
        int age = unchecked((short)target.ReadWord(12));
        int caster = Actor(frame.State.ReadDword(CasterGlobal));
        if (age < 10)
        {
            NativeBattlePetrifyStare127PointStep point = inputs.ReadBoneF0(frame, caster);
            frame = point.Frame;
            target = Read(frame, address).WriteWord(0x10, unchecked((ushort)point.Point.X)).WriteWord(0x12, unchecked((ushort)point.Point.Y)).WriteWord(0x14, unchecked((ushort)point.Point.Z));
            frame = Write(frame, address, target);
        }
        NativeBattleGraphicsModel casterModel = inputs.ReadModel(caster);
        short yaw = casterModel.Rotation14 ?? throw new NotSupportedException("Petrify Stare requires its caster's actual yaw WORD.");
        int scale = unchecked((short)(casterModel.Transform ?? throw new NotSupportedException("Petrify Stare requires its caster's actual transform.")).Scale38);
        short x = unchecked((short)(target.ReadWord(0x10) + (-NativeBattleCameraMath.Sine(yaw) >> 4)));
        short z = unchecked((short)(target.ReadWord(0x14) + (-NativeBattleCameraMath.Cosine(yaw) >> 4)));
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(target.ReadWord(0x12))).Write(4, BitConverter.GetBytes(z)).Write(8, BitConverter.GetBytes((-2048 - scale) >> 5)).Write(12, BitConverter.GetBytes(checked(Working(frame) + MorphOffset)));
        frame = inputs.Execute(frame, new NativeBattlePetrifyStare127AdvanceAct(address, 0, context));
        if (age == 4)
        {
            frame = inputs.Execute(frame, new NativeBattlePetrifyStare127Flash());
        }
        if ((uint)(age - 14) < 100)
        {
            target = Read(frame, address);
            int actor = Actor(target.ReadDword(0x20));
            if (age == 14)
            {
                NativeBattlePetrifyStare127AnchorStep anchor = inputs.ReadAnchor(frame, actor);
                frame = anchor.Frame;
                target = Read(frame, address).WriteWord(0x18, unchecked((ushort)anchor.Anchor.X)).WriteWord(0x1A, unchecked((ushort)anchor.Anchor.Y)).WriteWord(0x1C, unchecked((ushort)anchor.Anchor.Z)).WriteWord(0x1E, unchecked((ushort)anchor.Anchor.W));
                frame = Write(frame, address, target);
            }
            int targetScale = unchecked((short)(inputs.ReadModel(actor).Transform ?? throw new NotSupportedException("Petrify Stare requires its recipient's actual transform.")).Scale38);
            context = NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(0, target.ReadBytes(0x18, 8)).Write(8, BitConverter.GetBytes((-2048 - targetScale) >> 5)).Write(12, BitConverter.GetBytes(checked(Working(frame) + MorphOffset)));
            frame = inputs.Execute(frame, new NativeBattlePetrifyStare127AdvanceAct(address, 1, context));
        }
        if (age == 0)
        {
            NativeBattlePetrifyStare127AnchorStep anchor = inputs.ReadAnchor(frame, caster);
            frame = inputs.Execute(anchor.Frame, new NativeBattlePetrifyStare127Sound(resources.Sound, anchor.Anchor));
        }
        if (age == 42)
        {
            frame = inputs.Execute(frame, new NativeBattlePetrifyStare127Contact(FirstContact(frame, inputs) + unchecked((short)Read(frame, address).ReadWord(14))));
        }
        if (age > 48)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static NativeBattleModulePoolTask Read(NativeBattlePetrifyStare127Frame frame, uint address)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return pool.Slots[checked((int)((address - pool.BankAddress) / pool.Stride))];
            }
        }
        throw new NotSupportedException("Petrify Stare task address has no current physical owner.");
    }

    public static NativeBattlePetrifyStare127Frame Write(NativeBattlePetrifyStare127Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (address >= pool.BankAddress && address < pool.BankAddress + (uint)(pool.Stride * pool.Slots.Length) && (address - pool.BankAddress) % pool.Stride == 0)
            {
                return Put(frame, index, checked((int)((address - pool.BankAddress) / pool.Stride)), task);
            }
        }
        throw new NotSupportedException("Petrify Stare task write has no current physical owner.");
    }

    private static void RequireBinding(NativeBattlePetrifyStare127Frame frame)
    {
        if (frame.Effect.Binding.Effect != 127 || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Petrify Stare requires its selected original binding.");
        }
        frame.State.Validate();
    }

    private static NativeBattleEffectParameters Parameters(NativeBattlePetrifyStare127Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Petrify Stare requires its captured native parameters.");
    private static int FirstContact(NativeBattlePetrifyStare127Frame frame, NativeBattlePetrifyStare127Inputs inputs) => inputs.Packets.Headers[Parameters(frame).HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Petrify Stare requires its captured physical contact pointer.");
    private static NativeBattleEffectArena Arena(NativeBattlePetrifyStare127Frame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Petrify Stare requires its current working arena.");
    private static uint Working(NativeBattlePetrifyStare127Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static uint ModelAddress(int actor) => actor is >= 0 and < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new NotSupportedException("Petrify Stare selected an absent physical actor.");
    private static int Actor(uint pointer) => pointer >= 0x1D972C0 && pointer < 0x1D972C0 + 7 * 156 && (pointer - 0x1D972C0) % 156 == 0 ? checked((int)((pointer - 0x1D972C0) / 156)) : throw new NotSupportedException("Petrify Stare requires an actual physical actor pointer.");
    private static NativeBattlePetrifyStare127Frame WithPool(NativeBattlePetrifyStare127Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattlePetrifyStare127Frame Put(NativeBattlePetrifyStare127Frame frame, int pool, int slot, NativeBattleModulePoolTask task) => WithPool(frame, pool, frame.Effect.Pools[pool].WriteSlot(slot, task));
}
