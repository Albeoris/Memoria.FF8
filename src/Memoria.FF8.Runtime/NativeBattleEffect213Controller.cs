using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect213Controller
{
    public static NativeBattleEffect213Frame Initialize(NativeBattleEffect213Frame frame, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect213 entry requires its inactive root bank.");
        }
        (NativeBattleEffect213State state, NativeBattleFileStaging staging) = frame.State.BindSources(RequireStaging(frame), resources);
        frame = frame with { State = state, Staging = staging };
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect213 requires its executed texture initializer.");
        uint texture = checked(CommonAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, texture) };
        frame = inputs.Execute(frame, new NativeBattleEffect213UploadTim505E30(texture));
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect213 requires its actual parameters.");
        frame = frame with { State = frame.State.WriteDword(CasterGlobal, ModelAddress(parameters.Actor78)).WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78) };
        NativeBattleEffectTaskPool root = CreatePool(frame, Pools[0], false);
        (root, _) = root.Append(RootCallback);
        frame = frame with { State = frame.State.DetachBank(0), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor } };
        frame = ClearDwords701200(frame, RootBank + 12, 2);
        return inputs.Execute(frame, new NativeBattleEffect213RegisterCamera5099A0(CameraAddress));
    }

    public static NativeBattleEffect213Frame Advance(NativeBattleEffect213Frame frame, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
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

    public static (NativeBattleEffect213Frame Frame, int Survivors) AdvancePool(NativeBattleEffect213Frame frame, int index, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect213Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect213Step AdvanceTask(NativeBattleEffect213Frame frame, uint address, NativeBattleEffect213Resources resources, NativeBattleEffect213Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot69EB70(frame, address, resources, inputs),
        MainCallback => AdvanceMain69EF60(frame, address, resources, inputs),
        _ => NativeBattleEffect213Lower.Advance(frame, address, resources, inputs)
    };

    private static NativeBattleEffectTaskPool CreatePool(NativeBattleEffect213Frame frame, NativeBattleCureFamilyPool shape, bool common)
    {
        uint bank = common ? checked(frame.State.ReadDword(ArenaGlobal) + shape.Bank) : shape.Bank;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, bank, shape.Stride, shape.Capacity);
        NativeBattleEffectArena source = common ? frame.Memory.Arena ?? throw new NotSupportedException("Effect213 requires current COMMON.") : frame.State.ReadBank(0);
        int start = common ? checked((int)(bank - CommonAddress)) : 0;
        for (int slot = 0; slot < shape.Capacity; slot++)
        {
            int offset = slot * shape.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(source, start + offset, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
        }
        return pool;
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect213 requires an actual physical model row.");
    private static void RequireBinding(NativeBattleEffect213Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect213 requires its selected entry.");
        }
    }
}
