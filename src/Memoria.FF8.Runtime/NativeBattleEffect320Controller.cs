using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect320Controller
{
    public static NativeBattleEffect320Frame Initialize(NativeBattleEffect320Frame frame, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect320 entry requires its inactive banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect320 requires its current parameters.");
        int textureOffset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect320 requires its executed initializer pointer.");
        uint texture = checked(CommonAddress + (uint)textureOffset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, texture) };
        frame = inputs.Execute(frame, new NativeBattleEffect320UploadTim505E30(texture));
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, unchecked(0x1D972C0u + (uint)(parameters.Actor78 * 156))) };
        if (frame.State.MutableAnchorSourceBound is null)
        {
            throw new NotSupportedException("Effect320 unknown mutable source requires an explicit original-owner migration.");
        }
        if (frame.State.MutableAnchorSourceBound == false)
        {
            frame = frame with { State = frame.State.WriteWord(MutableAnchorAddress, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(MutableAnchorAddress, 2))) with { MutableAnchorSourceBound = true } };
        }
        NativeBattleCureFamilyPool shape = Pools[0];
        NativeBattleEffectTaskPool root = InitializePayload(shape, frame.State.ReadBank(0));
        (root, int slot) = root.Append(RootCallback);
        root = NativeBattleEffectTaskMemory.WriteBankSpan(root, slot * shape.Stride + 12, NativeBattleEffectArena.Clear(), 8);
        frame = frame with { State = frame.State.DetachBank(0), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor } };
        return inputs.Execute(frame, new NativeBattleEffect320RegisterCamera5099A0(CameraAddress));
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

    private static NativeBattleEffect320Frame InitializeCommonBanks(NativeBattleEffect320Frame frame)
    {
        foreach (NativeBattleCureFamilyPool relative in CommonPools)
        {
            NativeBattleCureFamilyPool shape = relative with { Bank = checked(frame.State.ReadDword(ArenaGlobal) + relative.Bank) };
            NativeBattleEffectArena bank = ReadBytes(frame, shape.Bank, shape.Stride * shape.Capacity);
            NativeBattleEffectTaskPool pool = InitializePayload(shape, bank);
            frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) }, State = frame.State.DetachCommonQueue(shape.Descriptor) };
        }
        return frame;
    }

    public static NativeBattleEffect320Frame Advance(NativeBattleEffect320Frame frame, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
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

    public static (NativeBattleEffect320Frame Frame, int Survivors) AdvancePool(NativeBattleEffect320Frame frame, int index, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect320Step advanced = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = advanced.Frame;
            return new(frame.Effect.Pools[index], advanced.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect320Step AdvanceTask(NativeBattleEffect320Frame frame, uint address, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot65BCC0(frame, address, resources, inputs),
            MainCallback => AdvanceMain65BE90(frame, address, resources, inputs),
            _ => NativeBattleEffect320Lower.Advance(frame, address, resources, inputs)
        };
    }

    public static int Actor(uint modelAddress)
    {
        long offset = (long)modelAddress - 0x1D972C0;
        return offset >= 0 && offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Effect320 requires an actual physical model pointer.");
    }

    private static void RequireBinding(NativeBattleEffect320Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect320 requires its selected entry.");
        }
    }
}
