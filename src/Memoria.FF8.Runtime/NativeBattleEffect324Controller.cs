using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect324Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect324Controller
{
    public static NativeBattleEffect324Frame Initialize(NativeBattleEffect324Frame frame, NativeBattleEffect324Resources resources, NativeBattleEffect324Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect324 entry requires its inactive fixed banks.");
        }
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect324 requires its current parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int first = FirstContact(frame, packets);
        int target = (packets.Contacts[first] ?? throw new NotSupportedException("Effect324 requires its current first contact.")).Primary.ActorIndex;
        int initializerOffset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect324 requires its executed initializer.");
        uint initializer = checked(CommonAddress + (uint)initializerOffset);
        frame = frame with { State = frame.State.WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, parameters.Actor78).WriteDword(TargetGlobal, (uint)target).WriteDword(InitializerGlobal, initializer) };
        ImmutableArray<NativeBattleEffectTaskPool> pools = [];
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            pools = pools.Add(CreatePool(frame, shape));
            frame = frame with { State = frame.State.DetachBank(shape.Bank) };
        }
        frame = frame with { Effect = frame.Effect with { Pools = pools, ReturnedDescriptor = RootDescriptor } };
        frame = BirthTask(frame, RootDescriptor, RootCallback, out _);
        frame = BirthTask(frame, ChildDescriptor, MainCallback, out _);
        frame = InitializeRecords(frame);
        frame = inputs.Execute(frame, new NativeBattleEffect324RegisterCamera5099A0(CameraAddress));
        return inputs.Execute(frame, new NativeBattleEffect324UploadTim505E30(initializer));
    }

    public static NativeBattleEffect324Frame Advance(NativeBattleEffect324Frame frame, NativeBattleEffect324Resources resources, NativeBattleEffect324Inputs inputs)
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

    public static (NativeBattleEffect324Frame Frame, int Survivors) AdvancePool(NativeBattleEffect324Frame frame, int index, NativeBattleEffect324Resources resources, NativeBattleEffect324Inputs inputs)
    {
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = WithPool(frame, index, pool);
            NativeBattleEffect324Step result = AdvanceTask(frame, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            frame = result.Frame;
            return new(frame.Effect.Pools[index], result.Result);
        });
        return (WithPool(frame, index, step.Pool), step.Survivors);
    }

    public static NativeBattleEffect324Step AdvanceTask(NativeBattleEffect324Frame frame, uint address, NativeBattleEffect324Resources resources, NativeBattleEffect324Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot5979A0(frame, address, resources, inputs),
        MainCallback => AdvanceMain596C70(frame, address, inputs),
        _ => NativeBattleEffect324Lower.Advance(frame, address, resources, inputs)
    };

    public static NativeBattleEffect324Frame InitializeRecords(NativeBattleEffect324Frame frame)
    {
        for (int index = 0; index < RecordCount; index++)
        {
            frame = WriteBytes(frame, RecordsAddress + (uint)(index * RecordStride), NativeBattleEffectArena.Clear(), 4);
        }
        return frame;
    }

    private static NativeBattleEffectTaskPool CreatePool(NativeBattleEffect324Frame frame, NativeBattleCureFamilyPool shape)
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

    private static NativeBattleEffect324Frame BirthTask(NativeBattleEffect324Frame frame, uint descriptor, uint callback, out uint address)
    {
        (frame, address) = Append(frame, descriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect324 requires its mandatory task allocation.");
        }
        return Write(frame, address, Read(frame, address).WriteWord(12, 0));
    }

    public static uint ModelAddress(int actor) => (uint)actor < 7 ? checked(0x1D972C0u + (uint)(actor * 156)) : throw new InvalidDataException("Effect324 requires an actual physical model row.");

    private static int FirstContact(NativeBattleEffect324Frame frame, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect324 requires its current header.");
        return packets.Headers[parameters.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect324 requires its first contact pointer.");
    }

    private static void RequireBinding(NativeBattleEffect324Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect324 requires its selected entry.");
        }
    }
}
