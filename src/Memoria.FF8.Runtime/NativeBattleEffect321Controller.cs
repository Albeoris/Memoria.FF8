using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect321Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect321Controller
{
    public static NativeBattleEffect321Frame Initialize(NativeBattleEffect321Frame frame, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect321 entry requires both inactive fixed banks.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect321 requires its executed texture initializer.");
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect321 requires its actual parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int first = CurrentHeader(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Effect321 requires its actual first contact pointer.");
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, checked(CommonAddress + (uint)offset)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, parameters.Actor78).WriteDword(TargetGlobal, packets.ReadContact(first).Primary.ActorIndex) };
        NativeBattleEffectTaskPool root = CreatePool(frame, Pools[0]);
        NativeBattleEffectTaskPool child = CreatePool(frame, Pools[1]);
        frame = frame with { State = frame.State.DetachBank(RootBank).DetachBank(ChildBank), Effect = frame.Effect with { Pools = [root, child], ReturnedDescriptor = RootDescriptor } };
        (frame, uint rootAddress) = AppendRequired(frame, RootCallback);
        frame = Write(frame, rootAddress, Read(frame, rootAddress).WriteWord(12, 0));
        (frame, uint mainAddress) = AppendRequired(frame, MainCallback);
        frame = Write(frame, mainAddress, Read(frame, mainAddress).WriteWord(12, 0));
        for (int index = 0; index < RecordCount; index++)
        {
            frame = frame with { State = frame.State.WriteDword(RecordsAddress + (uint)(index * RecordStride), 0) };
        }
        frame = inputs.Execute(frame, new NativeBattleEffect321RegisterCamera5099A0(CameraAddress));
        return inputs.Execute(frame, new NativeBattleEffect321UploadTim505E30(frame.State.ReadDword(InitializerGlobal)));
    }

    public static NativeBattleEffect321Frame Advance(NativeBattleEffect321Frame frame, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
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

    public static (NativeBattleEffect321Frame Frame, int Survivors) AdvancePool(NativeBattleEffect321Frame frame, int index, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect321Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect321Step AdvanceTask(NativeBattleEffect321Frame frame, uint address, NativeBattleEffect321Resources resources, NativeBattleEffect321Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot598D00(frame, address, resources, inputs),
        MainCallback => AdvanceMain597AB0(frame, address, resources, inputs),
        CleanupCallback => AdvanceCleanup598D60(frame, address, inputs),
        _ => NativeBattleEffect321Lower.Advance(frame, address, resources, inputs)
    };

    private static NativeBattleEffectTaskPool CreatePool(NativeBattleEffect321Frame frame, NativeBattleCureFamilyPool shape)
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

    public static (NativeBattleEffect321Frame Frame, uint Address) AppendRequired(NativeBattleEffect321Frame frame, uint callback)
    {
        uint descriptor = callback is RootCallback or CleanupCallback ? RootDescriptor : ChildDescriptor;
        (frame, uint address) = Append(frame, descriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect321 requires an available native task slot.");
        }
        return (frame, address);
    }

    private static NativeBattleActionHeaderSlot CurrentHeader(NativeBattleEffect321Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect321 requires its actual action header.");
        return packets.Headers[parameters.HeaderIndex7C];
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect321 requires an actual physical model row.");
    private static void RequireBinding(NativeBattleEffect321Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect321 requires its selected entry.");
        }
    }
}
