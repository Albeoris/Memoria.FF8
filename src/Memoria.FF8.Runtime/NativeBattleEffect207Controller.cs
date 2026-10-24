using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect207Controller
{
    public static NativeBattleEffect207Frame Initialize(NativeBattleEffect207Frame frame, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect207 entry requires its inactive banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect207 requires its current parameters.");
        int textureOffset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect207 requires its executed initializer pointer.");
        uint texture = checked(CommonAddress + (uint)textureOffset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, texture) };
        frame = inputs.Execute(frame, new NativeBattleEffect207UploadTim505E30(texture));
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, unchecked(0x1D972C0u + (uint)(parameters.Actor78 * 156))) };
        frame = WriteBytes(frame, MutableMeshAddress, NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(OriginalMeshAddress, MutableMeshLength)), MutableMeshLength);
        NativeBattleCureFamilyPool shape = Pools[0];
        NativeBattleEffectTaskPool root = InitializePayload(shape, frame.State.ReadBank(0));
        (root, int slot) = root.Append(RootCallback);
        root = NativeBattleEffectTaskMemory.WriteBankSpan(root, slot * shape.Stride + 12, NativeBattleEffectArena.Clear(), 8);
        frame = frame with { State = frame.State.DetachBank(0), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor } };
        return inputs.Execute(frame, new NativeBattleEffect207RegisterCamera5099A0(CameraAddress));
    }

    private static NativeBattleEffectTaskPool InitializePayload(NativeBattleCureFamilyPool shape, NativeBattleEffectArena bank)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
        for (int slot = 0; slot < shape.Capacity; slot++)
        {
            int offset = slot * shape.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(bank, offset, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
        }
        return pool;
    }

    private static NativeBattleEffect207Frame InitializeCommonBanks(NativeBattleEffect207Frame frame)
    {
        foreach (NativeBattleCureFamilyPool relative in CommonPools.Reverse())
        {
            NativeBattleCureFamilyPool shape = relative with { Bank = checked(frame.State.ReadDword(ArenaGlobal) + relative.Bank) };
            NativeBattleEffectArena bank = ReadBytes(frame, shape.Bank, shape.Stride * shape.Capacity);
            NativeBattleEffectTaskPool pool = InitializePayload(shape, bank);
            frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) }, State = frame.State.DetachCommonQueue(shape.Descriptor) };
        }
        return frame;
    }

    public static NativeBattleEffect207Frame Advance(NativeBattleEffect207Frame frame, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
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
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            if (!frame.Effect.Pools.Any(pool => pool.DescriptorAddress == shape.Descriptor))
            {
                continue;
            }
            frame = frame with { State = frame.State.AttachCommonQueue(shape.Descriptor, ReadBytes(frame, shape.Descriptor, 8)) };
            NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, shape.Descriptor);
            frame = frame with { Memory = transfer.Memory, Effect = transfer.Effect };
        }
        frame = frame with { State = frame.State.AttachBank(0, NativeBattleEffectTaskMemory.CaptureBank(frame.Effect.Pools[0])) };
        return frame with { Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static (NativeBattleEffect207Frame Frame, int Survivors) AdvancePool(NativeBattleEffect207Frame frame, int index, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect207Step advanced = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = advanced.Frame;
            return new(frame.Effect.Pools[index], advanced.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect207Step AdvanceTask(NativeBattleEffect207Frame frame, uint address, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot6A3B70(frame, address, resources, inputs),
        MainCallback => AdvanceMain6A3D90(frame, address, resources, inputs),
        _ => NativeBattleEffect207Lower.Advance(frame, address, resources, inputs)
    };

    public static int Actor(uint modelAddress)
    {
        long offset = (long)modelAddress - 0x1D972C0;
        return offset >= 0 && offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Effect207 requires an actual physical model pointer.");
    }

    private static void RequireBinding(NativeBattleEffect207Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect207 requires its selected entry.");
        }
    }
}
