using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect316Controller
{
    public static NativeBattleEffect316Frame Initialize(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect316 requires inactive task banks before entry.");
        }
        NativeBattleEffectArena retained = frame.State.ReadBank(RootBank);
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect316 requires its executed texture initializer.");
        uint texture = checked(CommonAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, texture) };
        frame = inputs.Execute(frame, new NativeBattleEffect316UploadTim505E30(texture));
        frame = inputs.Execute(frame, new NativeBattleEffect316UploadTim505E30(EmbeddedTimAddress));
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect316 requires its actual current parameters.");
        uint model = ModelAddress(parameters.Actor78);
        frame = frame with { State = frame.State.WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, model).WriteDword(WorkGlobal, CommonAddress) };
        frame = inputs.Execute(frame, new NativeBattleEffect316ModelFlags(model, 2, 0xFFF3, 0));
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        root = NativeBattleEffectTaskMemory.WriteBankSpan(root, 12, CopyBytes(retained, 12, NativeBattleEffectArena.Clear(), 0, RootStride - 12), RootStride - 12);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor }, State = frame.State.DetachBank(RootBank) };
        frame = ClearDwords6DA380(frame, RootBank + 12, 2);
        for (int physical = 0; physical < 3; physical++)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect316ModelFlags(ModelAddress(physical), 2, ushort.MaxValue, 4));
        }
        return inputs.Execute(frame, new NativeBattleEffect316RegisterCamera5099A0(CameraAddress));
    }

    public static NativeBattleEffect316Frame Advance(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        ValidatePools(frame);
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    public static NativeBattleEffect316Step AdvanceTask(NativeBattleEffect316Frame frame, uint address, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot661190(frame, address, resources, inputs),
        MainCallback => AdvanceMain661350(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect316 reached an unsupported task callback.")
    };

    public static (NativeBattleEffect316Frame Frame, int Survivors) AdvancePool(NativeBattleEffect316Frame frame, int index, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect316Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect316Frame InitializeMainBank(NativeBattleEffect316Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Effect316 requires its inactive COMMON main bank.");
        }
        uint bank = frame.State.ReadDword(WorkGlobal);
        if (bank != CommonAddress)
        {
            throw new InvalidDataException("Effect316 main bank requires the current fixed COMMON base.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect316 requires its current COMMON owner.");
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, bank, MainStride, 1);
        main = NativeBattleEffectTaskMemory.WriteBankSpan(main, 12, CopyBytes(arena, 12, NativeBattleEffectArena.Clear(), 0, MainStride - 12), MainStride - 12);
        ImmutableArray<NativeBattleEffectTaskPool> pools = frame.Effect.Pools.Add(main);
        return frame with { Effect = frame.Effect with { Pools = pools }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, pools) };
    }

    private static NativeBattleEffect316Frame DetachBanks(NativeBattleEffect316Frame frame)
    {
        NativeBattleEffectTaskPool root = frame.Effect.Pools[0];
        if (root.Head is not null)
        {
            throw new InvalidDataException("Effect316 cannot detach an active root.");
        }
        frame = frame with { State = frame.State.AttachBank(RootBank, NativeBattleEffectTaskMemory.CaptureBank(root)) };
        if (frame.Effect.Pools.Length == 2)
        {
            NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, MainDescriptor);
            frame = frame with { Memory = transfer.Memory, Effect = transfer.Effect };
        }
        return frame with { Effect = frame.Effect with { Pools = [] } };
    }

    private static void ValidatePools(NativeBattleEffect316Frame frame)
    {
        if (frame.Effect.Pools.Length is not (1 or 2) || frame.State.FixedBanks[RootBank] is not null)
        {
            throw new InvalidDataException("Effect316 requires exclusive active task banks.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            uint descriptor = index == 0 ? RootDescriptor : MainDescriptor;
            uint bank = index == 0 ? RootBank : CommonAddress;
            int stride = index == 0 ? RootStride : MainStride;
            if (pool.DescriptorAddress != descriptor || pool.BankAddress != bank || pool.Stride != stride || pool.Slots.Length != 1)
            {
                throw new InvalidDataException("Effect316 task bank leaves its native dimensions.");
            }
            pool.Validate();
        }
    }

    private static void RequireBinding(NativeBattleEffect316Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect316 requires its actual selected entry.");
        }
    }

    private static uint ModelAddress(int physical) => (uint)physical < 7 ? checked(0x1D972C0u + (uint)(physical * 156)) : throw new InvalidDataException("Effect316 requires its actual physical caster.");
}
