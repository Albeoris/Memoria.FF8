using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect63Controller
{
    public static NativeBattleEffect63Frame Initialize(NativeBattleEffect63Frame frame, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect63 entry requires inactive task banks.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect63 requires its executed texture initializer.");
        uint texture = checked(CommonAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, texture) };
        frame = inputs.Execute(frame, new NativeBattleEffect63UploadTim505E30(texture));
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect63 requires its actual action parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int first = packets.Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect63 requires its actual first contact.");
        frame = frame with { State = frame.State.WriteDword(WorkGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(parameters.Actor78)).WriteDword(TargetGlobal, ModelAddress(packets.ReadContact(first).Primary.ActorIndex)) };
        NativeBattleEffectTaskPool root = CreatePool(frame, FixedPools[0], false);
        (root, _) = root.Append(RootCallback);
        frame = frame with { State = frame.State.DetachBank(0), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor } };
        frame = ClearDwords701200(frame, RootBank + 12, 1);
        return inputs.Execute(frame, new NativeBattleEffect63RegisterCamera5099A0(CameraAddress));
    }

    public static NativeBattleEffect63Frame Advance(NativeBattleEffect63Frame frame, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        if (survivors != 0)
        {
            return frame;
        }
        frame = frame with { State = frame.State.AttachBank(0, NativeBattleEffectTaskMemory.CaptureBank(frame.Effect.Pools[0])) };
        foreach (uint descriptor in frame.Effect.Pools.Skip(1).Select(pool => pool.DescriptorAddress).ToArray())
        {
            NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
            frame = frame with { Memory = transfer.Memory, Effect = transfer.Effect };
        }
        return frame with { Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static (NativeBattleEffect63Frame Frame, int Survivors) AdvancePool(NativeBattleEffect63Frame frame, int index, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect63Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect63Step AdvanceTask(NativeBattleEffect63Frame frame, uint address, NativeBattleEffect63Resources resources, NativeBattleEffect63Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot6E6CB0(frame, address, resources, inputs),
        MainCallback => AdvanceMain6E6E10(frame, address, resources, inputs),
        _ => NativeBattleEffect63Lower.Advance(frame, address, resources, inputs)
    };

    private static NativeBattleEffectTaskPool CreatePool(NativeBattleEffect63Frame frame, NativeBattleCureFamilyPool shape, bool common)
    {
        uint bank = common ? checked(frame.State.ReadDword(WorkGlobal) + shape.Bank) : shape.Bank;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, bank, shape.Stride, shape.Capacity);
        NativeBattleEffectArena source = common ? frame.Memory.Arena ?? throw new NotSupportedException("Effect63 requires current COMMON.") : frame.State.ReadBank(0);
        int start = common ? checked((int)(bank - CommonAddress)) : 0;
        for (int slot = 0; slot < shape.Capacity; slot++)
        {
            int offset = slot * shape.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(source, start + offset, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
        }
        return pool;
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect63 requires an actual physical model row.");

    private static void RequireBinding(NativeBattleEffect63Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect63 requires its selected entry.");
        }
    }
}
