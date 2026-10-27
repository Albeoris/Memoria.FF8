using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect308Controller
{
    public static NativeBattleEffect308Frame Initialize(NativeBattleEffect308Frame frame, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect308 entry requires its inactive banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect308 requires its current parameters.");
        int textureOffset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect308 requires its executed initializer pointer.");
        uint texture = checked(CommonAddress + (uint)textureOffset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, texture) };
        frame = inputs.Execute(frame, new NativeBattleEffect308UploadTim505E30(texture));
        frame = frame with { State = frame.State.WriteDword(ArenaGlobal, CommonAddress).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, unchecked(0x1D972C0u + (uint)(parameters.Actor78 * 156))) };
        frame = frame with { State = frame.State.BindOriginalScriptSources(resources) };
        frame = inputs.Execute(frame, new NativeBattleEffect308ModelFlags(frame.State.ReadDword(CasterGlobal), 2, 0xFFF3, 0));
        NativeBattleCureFamilyPool shape = Pools[0];
        NativeBattleEffectTaskPool root = InitializePayload(shape, frame.State.ReadBank(0));
        (root, int slot) = root.Append(RootCallback);
        root = NativeBattleEffectTaskMemory.WriteBankSpan(root, slot * shape.Stride + 12, NativeBattleEffectArena.Clear(), 8);
        frame = frame with { State = frame.State.DetachBank(0), Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor } };
        return inputs.Execute(frame, new NativeBattleEffect308RegisterCamera5099A0(CameraAddress));
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

    private static NativeBattleEffect308Frame InitializeCommonBanks(NativeBattleEffect308Frame frame)
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

    public static NativeBattleEffect308Frame Advance(NativeBattleEffect308Frame frame, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
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

    public static (NativeBattleEffect308Frame Frame, int Survivors) AdvancePool(NativeBattleEffect308Frame frame, int index, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect308Step advanced = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = advanced.Frame;
            return new(frame.Effect.Pools[index], advanced.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect308Step AdvanceTask(NativeBattleEffect308Frame frame, uint address, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        return Read(frame, address).Callback switch
        {
            RootCallback => AdvanceRoot667720(frame, address, resources, inputs),
            MainCallback => AdvanceMain668DE0(frame, address, resources, inputs),
            ScriptCallback => AdvanceScript667990(frame, address, resources, inputs),
            _ => NativeBattleEffect308Lower.Advance(frame, address, resources, inputs)
        };
    }

    public static int Actor(uint modelAddress)
    {
        long offset = (long)modelAddress - 0x1D972C0;
        return offset >= 0 && offset % 156 == 0 && offset / 156 < 7 ? (int)(offset / 156) : throw new InvalidDataException("Effect308 requires an actual physical model pointer.");
    }

    private static void RequireBinding(NativeBattleEffect308Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect308 requires its selected entry.");
        }
    }
}
