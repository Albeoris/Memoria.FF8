using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect290Controller
{
    public static NativeBattleEffect290Frame Initialize(NativeBattleEffect290Frame frame, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect290 entry requires its inactive root bank.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect290 requires its executed texture initializer.");
        uint texture = checked(CommonAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, texture) };
        frame = inputs.Execute(frame, new NativeBattleEffect290UploadTim505E30(texture));
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect290 requires its actual parameters.");
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78) };
        uint caster = ModelAddress(parameters.Actor78);
        frame = frame with { State = frame.State.WriteDword(CasterGlobal, caster) };
        frame = inputs.Execute(frame, new NativeBattleEffect290ModelFlags(caster, 2, 0xFFF3, 0));
        frame = inputs.Execute(frame, new NativeBattleEffect290ModelFlags(caster, 1, 255, 8));
        NativeBattleEffectTaskPool root = CreatePool(frame, Pools[0], false);
        (root, _) = root.Append(RootCallback);
        frame = frame with { State = frame.State.DetachBank(0), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor } };
        frame = ClearDwords701200(frame, RootBank + 12, 2);
        return inputs.Execute(frame, new NativeBattleEffect290RegisterCamera5099A0(CameraAddress));
    }

    public static NativeBattleEffect290Frame Advance(NativeBattleEffect290Frame frame, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
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

    public static (NativeBattleEffect290Frame Frame, int Survivors) AdvancePool(NativeBattleEffect290Frame frame, int index, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect290Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect290Step AdvanceTask(NativeBattleEffect290Frame frame, uint address, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot675340(frame, address, resources, inputs),
        MainCallback => AdvanceMain6767B0(frame, address, resources, inputs),
        0x6756A0 => AdvanceScript6756A0(frame, address, resources, inputs),
        _ => (inputs.AdvanceLower ?? throw new NotSupportedException("Effect290 requires its selected lower callback provider."))(frame, address, resources, inputs)
    };

    private static NativeBattleEffectTaskPool CreatePool(NativeBattleEffect290Frame frame, NativeBattleCureFamilyPool shape, bool common)
    {
        uint bank = common ? checked(frame.State.ReadDword(ArenaGlobal) + shape.Bank) : shape.Bank;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, bank, shape.Stride, shape.Capacity);
        NativeBattleEffectArena source = common ? frame.Memory.Arena ?? throw new NotSupportedException("Effect290 requires current COMMON.") : frame.State.ReadBank(0);
        int start = common ? checked((int)(bank - CommonAddress)) : 0;
        for (int slot = 0; slot < shape.Capacity; slot++)
        {
            int offset = slot * shape.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(source, start + offset, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
        }
        return pool;
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect290 requires an actual physical model row.");
    private static void RequireBinding(NativeBattleEffect290Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect290 requires its selected entry.");
        }
    }
}
