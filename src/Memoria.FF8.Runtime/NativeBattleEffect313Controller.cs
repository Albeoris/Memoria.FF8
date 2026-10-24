using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect313Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect313Controller
{
    public static NativeBattleEffect313Frame Initialize(NativeBattleEffect313Frame frame, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect313 entry requires both inactive fixed banks.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect313 requires its executed texture initializer.");
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect313 requires its actual parameters.");
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, checked(CommonAddress + (uint)offset)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, parameters.Actor78).WriteDword(ArenaGlobal, CommonAddress) };
        NativeBattleEffectTaskPool root = CreatePool(frame, Pools[0]);
        NativeBattleEffectTaskPool child = CreatePool(frame, Pools[1]);
        frame = frame with { State = frame.State.DetachBank(RootBank).DetachBank(ChildBank), Effect = frame.Effect with { Pools = [root, child], ReturnedDescriptor = RootDescriptor } };
        (frame, uint rootAddress) = AppendRequired(frame, RootCallback);
        frame = Write(frame, rootAddress, Read(frame, rootAddress).WriteWord(12, 0));
        (frame, uint mainAddress) = AppendRequired(frame, MainCallback);
        frame = Write(frame, mainAddress, Read(frame, mainAddress).WriteWord(12, 0));
        frame = inputs.Execute(frame, new NativeBattleEffect313RegisterCamera5099A0(CameraAddress));
        frame = inputs.Execute(frame, new NativeBattleEffect313UploadTim505E30(frame.State.ReadDword(InitializerGlobal)));
        for (int index = 0; index < RecordCount; index++)
        {
            frame = frame with { State = frame.State.WriteDword(RecordsAddress + (uint)(index * RecordStride), 0) };
        }
        return frame;
    }

    public static NativeBattleEffect313Frame Advance(NativeBattleEffect313Frame frame, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
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
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            frame = frame with { State = frame.State.AttachBank(pool.BankAddress, NativeBattleEffectTaskMemory.CaptureBank(pool)) };
        }
        return frame with { Effect = frame.Effect with { Pools = [], ReturnedDescriptor = 0 } };
    }

    public static (NativeBattleEffect313Frame Frame, int Survivors) AdvancePool(NativeBattleEffect313Frame frame, int index, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect313Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect313Step AdvanceTask(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot59CA20(frame, address, resources, inputs),
        MainCallback => AdvanceMain59B180(frame, address, resources, inputs),
        CleanupCallback => AdvanceCleanup59C9E0(frame, address, inputs),
        _ => NativeBattleEffect313Lower.Advance(frame, address, resources, inputs)
    };

    private static NativeBattleEffectTaskPool CreatePool(NativeBattleEffect313Frame frame, NativeBattleCureFamilyPool shape)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
        NativeBattleEffectArena source = frame.State.ReadBank(shape.Bank);
        for (int slot = 0; slot < shape.Capacity; slot++)
        {
            int offset = slot * shape.Stride + 12;
            pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, offset, CopyBytes(source, offset, NativeBattleEffectArena.Clear(), 0, shape.Stride - 12), shape.Stride - 12);
        }
        return pool;
    }

    public static (NativeBattleEffect313Frame Frame, uint Address) AppendRequired(NativeBattleEffect313Frame frame, uint callback)
    {
        uint descriptor = callback is RootCallback or CleanupCallback ? RootDescriptor : ChildDescriptor;
        (frame, uint address) = Append(frame, descriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect313 requires an available native task slot.");
        }
        return (frame, address);
    }

    private static NativeBattleActionHeaderSlot CurrentHeader(NativeBattleEffect313Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect313 requires its actual action header.");
        return packets.Headers[parameters.HeaderIndex7C];
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect313 requires an actual physical model row.");
    private static void RequireBinding(NativeBattleEffect313Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect313 requires its selected entry.");
        }
    }
}
