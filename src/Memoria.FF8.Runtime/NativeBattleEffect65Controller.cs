using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Controller
{
    public static NativeBattleEffect65Frame Initialize(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null || frame.State.ChildQueueStorage is null)
        {
            throw new InvalidDataException("Effect65 requires its exclusive inactive task owners.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect65 requires its executed texture initializer.");
        uint texture = checked(ArenaAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, texture) };
        frame = inputs.Execute(frame, new NativeBattleEffect65UploadTim505E30(texture));
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect65 requires its actual parameters.");
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, ArenaAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(parameters.Actor78)) };
        NativeBattleEffectTaskPool root = CreatePool(frame, Pools[0], false);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor }, State = frame.State with { RootBankStorage = null } };
        return ClearDwords701200(frame, RootBank + 12, 2);
    }

    public static NativeBattleEffect65Frame Advance(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
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

    public static NativeBattleEffect65Step AdvanceTask(NativeBattleEffect65Frame frame, uint address, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot6E03F0(frame, address, resources, inputs),
        MainCallback => AdvanceMain6E07D0(frame, address, resources, inputs),
        ChildCallback => AdvanceChild6E50A0(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect65 reached an unsupported callback.")
    };

    public static (NativeBattleEffect65Frame Frame, int Survivors) AdvancePool(NativeBattleEffect65Frame frame, int index, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect65Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect65Frame InitializeMainBank(NativeBattleEffect65Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Effect65 main bank must have its inactive COMMON owner.");
        }
        NativeBattleEffectTaskPool main = CreatePool(frame, Pools[1], true);
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(main) }, Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [main]) };
    }

    public static NativeBattleEffect65Frame InitializeChildBank(NativeBattleEffect65Frame frame)
    {
        if (frame.Effect.Pools.Length != 2 || frame.State.ChildQueueStorage is null)
        {
            throw new InvalidDataException("Effect65 child initialization requires its inactive descriptor and bank.");
        }
        NativeBattleEffectTaskPool child = CreatePool(frame, Pools[2], true);
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(child) }, State = frame.State.DetachChildQueue(), Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [child]) };
    }

    private static NativeBattleEffectTaskPool CreatePool(NativeBattleEffect65Frame frame, NativeBattleCureFamilyPool shape, bool common)
    {
        uint bank = common ? checked(ArenaAddress + shape.Bank) : shape.Bank;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, bank, shape.Stride, shape.Capacity);
        NativeBattleEffectArena source = common ? frame.Memory.Arena ?? throw new NotSupportedException("Effect65 requires its current COMMON owner.") : frame.State.RootBankStorage ?? throw new NotSupportedException("Effect65 requires its inactive root bank.");
        int baseOffset = common ? checked((int)shape.Bank) : 0;
        for (int slot = 0; slot < pool.Slots.Length; slot++)
        {
            int offset = slot * pool.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(source, baseOffset + offset, NativeBattleEffectArena.Clear(), 0, pool.Stride - 12), pool.Stride - 12);
        }
        return pool;
    }

    private static NativeBattleEffect65Frame DetachBanks(NativeBattleEffect65Frame frame)
    {
        NativeBattleEffectTaskPool root = frame.Effect.Pools[0];
        if (root.Head is not null || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect65 root retirement requires its inactive bank destination.");
        }
        frame = frame with { State = frame.State with { RootBankStorage = NativeBattleEffectTaskMemory.CaptureBank(root) } };
        if (frame.State.ChildQueueStorage is null)
        {
            frame = frame with { State = frame.State.AttachChildQueue(ReadBytes(frame, ChildDescriptor, ChildQueueLength)) };
        }
        foreach (uint descriptor in frame.Effect.Pools.Skip(1).Select(pool => pool.DescriptorAddress).ToArray())
        {
            NativeBattleEffectTaskTransfer detached = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
            frame = frame with { Memory = detached.Memory, Effect = detached.Effect };
        }
        return frame with { Effect = frame.Effect with { Pools = [] } };
    }

    private static void ValidatePools(NativeBattleEffect65Frame frame)
    {
        if (frame.Effect.Pools.Length is < 1 or > 3 || frame.State.RootBankStorage is not null || (frame.Effect.Pools.Length == 3) == (frame.State.ChildQueueStorage is not null))
        {
            throw new InvalidDataException("Effect65 lost its exclusive physical pool owners.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (pool.DescriptorAddress != shape.Descriptor || pool.BankAddress != (index == 0 ? RootBank : ArenaAddress + shape.Bank) || pool.Stride != shape.Stride || pool.Slots.Length != shape.Capacity)
            {
                throw new InvalidDataException("Effect65 task bank leaves its physical dimensions.");
            }
            pool.Validate();
        }
    }

    private static void RequireBinding(NativeBattleEffect65Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect65 requires its actual selected entry.");
        }
    }
    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect65 requires an actual physical model.");
}
