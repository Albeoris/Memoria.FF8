using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect64Controller
{
    public static NativeBattleEffect64Frame Initialize(NativeBattleEffect64Frame frame, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Effect64 requires its exclusive inactive root bank.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect64 requires its executed texture initializer.");
        uint texture = checked(CommonAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, texture) };
        frame = inputs.Execute(frame, new NativeBattleEffect64UploadTim505E30(texture));
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect64 requires its current parameters.");
        uint model = ModelAddress(parameters.Actor78);
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, model) };
        frame = inputs.Execute(frame, new NativeBattleEffect64ModelFlags(model, 0xFFF3, 0));
        frame = inputs.Execute(frame, new NativeBattleEffect64ModelMeshMask(model, uint.MaxValue));
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootBankLength, 1);
        root = NativeBattleEffectTaskMemory.WriteBankSpan(root, 12, CopyBytes(frame.State.RootBankStorage!, 12, NativeBattleEffectArena.Clear(), 0, RootBankLength - 12), RootBankLength - 12);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor }, State = frame.State with { RootBankStorage = null } };
        frame = ClearDwords701200(frame, RootBank + 12, 2);
        for (int actor = 0; actor < 3; actor++)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect64ModelFlags(ModelAddress(actor), ushort.MaxValue, 4));
        }
        return inputs.Execute(frame, new NativeBattleEffect64RegisterCamera(resources.Camera));
    }

    public static NativeBattleEffect64Frame Advance(NativeBattleEffect64Frame frame, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
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

    public static NativeBattleEffect64Step AdvanceTask(NativeBattleEffect64Frame frame, uint address, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot6E5260(frame, address, resources, inputs),
        MainCallback => AdvanceMain6E53F0(frame, address, resources, inputs),
        0x6E5DD0 => NativeBattleEffect64Script.Advance6E5DD0(frame, address, resources, inputs),
        0x6E6310 => NativeBattleEffect64Programme.Advance6E6310(frame, address, resources, inputs),
        0x6E67F0 => NativeBattleEffect64Particles.Advance6E67F0(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect64 reached an unsupported task callback.")
    };

    public static (NativeBattleEffect64Frame Frame, int Survivors) AdvancePool(NativeBattleEffect64Frame frame, int index, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect64Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect64Frame InitializeSecondaryBanks(NativeBattleEffect64Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Effect64 requires inactive COMMON banks before initialization.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect64 requires its current COMMON owner.");
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        for (int index = 1; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, checked(CommonAddress + shape.Bank), shape.Stride, shape.Capacity);
            for (int slot = 0; slot < pool.Slots.Length; slot++)
            {
                int source = checked((int)shape.Bank + slot * pool.Stride + 12);
                NativeBattleEffectArena retained = CopyBytes(arena, source, NativeBattleEffectArena.Clear(), 0, pool.Stride - 12);
                pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, slot * pool.Stride + 12, retained, pool.Stride - 12);
            }
            pools.Add(pool);
        }
        ImmutableArray<NativeBattleEffectTaskPool> attached = pools.ToImmutable();
        return frame with { Effect = frame.Effect with { Pools = attached }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, attached) };
    }

    private static NativeBattleEffect64Frame DetachBanks(NativeBattleEffect64Frame frame)
    {
        NativeBattleEffectTaskPool root = frame.Effect.Pools[0];
        if (root.Head is not null || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect64 root retirement requires its exclusive inactive destination.");
        }
        frame = frame with { State = frame.State with { RootBankStorage = NativeBattleEffectTaskMemory.CaptureBank(root) } };
        foreach (uint descriptor in frame.Effect.Pools.Skip(1).Select(pool => pool.DescriptorAddress).ToArray())
        {
            NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
            frame = frame with { Memory = transfer.Memory, Effect = transfer.Effect };
        }
        return frame with { Effect = frame.Effect with { Pools = [] } };
    }

    private static void ValidatePools(NativeBattleEffect64Frame frame)
    {
        if (frame.Effect.Pools.Length is not (1 or 5) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect64 requires its exclusive active task banks.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            uint bank = index == 0 ? RootBank : checked(CommonAddress + shape.Bank);
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != bank || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect64 task bank leaves its physical dimensions.");
            }
            pool.Validate();
        }
    }

    private static void RequireBinding(NativeBattleEffect64Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect64 requires its actual selected entry.");
        }
    }

    private static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect64 requires its actual physical caster.");
}
