using static Memoria.FF8.Runtime.NativeBattleLaserEye71Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLaserEye71Controller
{
    public static NativeBattleLaserEye71Frame Initialize(NativeBattleLaserEye71Frame frame, NativeBattleLaserEye71Resources resources, NativeBattleLaserEye71Inputs inputs)
    {
        if (frame.Effect.Binding.Effect != 71 || frame.Effect.Binding.Entry != Entry)
        {
            throw new InvalidDataException("Laser Eye requires its original entry binding.");
        }
        resources.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Laser Eye requires its captured action parameters.");
        int source = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Laser Eye requires its executed texture initializer.");
        _ = ArenaOffset(checked(ArenaAddress + (uint)source), WorkingBytes);
        NativeBattleCureFamilyPool rootPool = Pools[0];
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(rootPool.Descriptor, rootPool.Bank, rootPool.Stride, rootPool.Capacity)]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, RootCallback, 100);
        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Laser Eye requires the selected header's contact pointer.");
        byte targets = header.ReadByte(16);
        byte groups = header.ReadByte(17);
        byte maximum = unchecked((byte)(targets - 1));
        if (maximum < groups)
        {
            maximum = groups;
        }
        NativeBattleModulePoolTask root = appended.Hierarchy.Read(appended.Address).WriteDword(0xC, 0x1D99A78).WriteByte(0x2C, header.ReadByte(0)).WriteByte(0x2D, inputs.Packets.ReadContact(first).Primary.ActorIndex).WriteWord(0x5A, targets).WriteWord(0x58, groups).WriteByte(0x2F, maximum);
        frame = frame with { Effect = frame.Effect with { Pools = appended.Hierarchy.Write(appended.Address, root).Pools, ReturnedDescriptor = Pools[0].Descriptor, DrawArenaOffset = null }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)source)).WriteDword(0x25FCDCC, 0).WriteDword(0x25FAAE4, 0) };
        if ((parameters.Flags79 & 1) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleLaserEye71RegisterCamera(resources.Camera));
            frame = inputs.Execute(frame, new NativeBattleLaserEye71EnqueueTim(source));
        }
        uint start = checked(ArenaAddress + (uint)source);
        return frame with { State = frame.State.WriteDword(0x25FCD84, start).WriteDword(0x25FC17C, start + DrawBankLength).WriteDword(0x25FCD80, start + DrawBankLength).WriteDword(0x25FCDD0, start + 2 * DrawBankLength).WriteDword(0x25FC178, start + 2 * DrawBankLength), Effect = frame.Effect with { Pools = frame.Effect.Pools.AddRange(Pools.Skip(1).Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity))) } };
    }

    public static NativeBattleLaserEye71Frame Advance(NativeBattleLaserEye71Frame frame, NativeBattleLaserEye71Resources resources, NativeBattleLaserEye71Inputs inputs)
    {
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        NativeBattleLaserEye71Frame current = frame;
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((pool, slot) =>
        {
            current = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(0, pool) } };
            NativeBattleLaserEye71TaskStep step = Advance(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            current = step.Frame;
            return new(current.Effect.Pools[0], step.Result);
        });
        return current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(0, roots.Pool), ReturnedDescriptor = roots.Survivors == 0 ? 0u : Pools[0].Descriptor } };
    }

    public static NativeBattleLaserEye71TaskStep Advance(NativeBattleLaserEye71Frame frame, uint address, NativeBattleLaserEye71Resources resources, NativeBattleLaserEye71Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot(frame, address, resources, inputs),
        TargetCallback => AdvanceTarget(frame, address, inputs),
        BindingCallback => AdvanceBinding(frame, address, resources, inputs),
        BeamCallback => AdvanceBeam(frame, address, inputs),
        StageInCallback or StageOutCallback => AdvanceStage(frame, address, inputs),
        NodeCallback => inputs.AdvanceNodes(frame, address),
        ParticleCallback => inputs.AdvanceParticles96(frame, address),
        _ => throw new NotSupportedException("Laser Eye reached an unsupported native callback.")
    };
}
