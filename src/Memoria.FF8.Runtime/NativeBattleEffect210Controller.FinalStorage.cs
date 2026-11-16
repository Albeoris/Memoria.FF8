using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect210Controller
{
    private static NativeBattleEffect210Frame PrepareFinalTargetPool(NativeBattleEffect210Frame frame, uint main, NativeBattleEffect210Inputs inputs)
    {
        int count = TargetRecordCount(frame, main);
        if (count <= NativeBattleEffect210TargetRecords.OriginalCount)
        {
            return frame;
        }
        NativeBattleEffect210TargetRecords records = frame.State.TargetRecords!;
        if (inputs.StorageContract != BattleContactTaskStorageContract.ExpandableArena || records.FinalPoolAdmitted || Signed(Read(frame, main), 12) != 243)
        {
            throw new InvalidDataException("Effect210 hosted final tasks require their sole captured age243 admission.");
        }
        NativeBattleCureFamilyPool original = CommonPools.Single(shape => shape.Descriptor == ActDescriptor);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(candidate => candidate.DescriptorAddress == ActDescriptor);
        if (pool.BankAddress != checked(frame.State.ReadDword(ArenaGlobal) + original.Bank) || pool.Stride != original.Stride || pool.Slots.Length != original.Capacity || pool.Head is not null || pool.Tail is not null || pool.Slots.Any(task => (task.Flags & 1) != 0))
        {
            throw new InvalidDataException("Effect210 final task growth requires its empty original bank.");
        }
        int bytes = checked(count * original.Stride);
        if (bytes > NativeBattleEffectArena.Length)
        {
            throw new NotSupportedException("Effect210 final tasks exceed the hosted bank extent.");
        }
        NativeBattleEffectArena retained = CopyBytes(NativeBattleEffectTaskMemory.CaptureBank(pool), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, bytes), 0, original.Stride * original.Capacity);
        (NativeBattleEffectMemory memory, uint bank) = NativeBattlePresentationTaskStorage.Reserve(frame.Memory, frame.Effect, inputs.ReadPackets(frame).Roster, original.Stride, count);
        frame = DetachCommonPool(frame with { Memory = memory }, ActDescriptor, false);
        NativeBattleEffectTaskPool hosted = InitializePayload(original with { Bank = bank, Capacity = checked((ushort)count) }, retained);
        return frame with { State = frame.State.DetachCommonQueue(ActDescriptor) with { TargetRecords = records with { FinalPoolAdmitted = true } }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(hosted) } };
    }

    private static NativeBattleEffect210Frame DetachHostedFinalPool(NativeBattleEffect210Frame frame, NativeBattleEffectTaskPool pool)
    {
        NativeBattleCureFamilyPool original = CommonPools.Single(shape => shape.Descriptor == ActDescriptor);
        NativeBattleEffect210TargetRecords records = frame.State.TargetRecords ?? throw new InvalidDataException("Effect210 hosted final tasks lost their captured owner.");
        if (!records.FinalPoolAdmitted || pool.DescriptorAddress != ActDescriptor || pool.Stride != original.Stride || pool.Slots.Length != records.Count || !NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, pool.Stride, pool.Slots.Length) || pool.Head is not null || pool.Tail is not null || pool.Slots.Any(task => (task.Flags & 1) != 0))
        {
            throw new InvalidDataException("Effect210 hosted final task detachment requires its fully drained admitted bank.");
        }
        uint nativeBank = checked(frame.State.ReadDword(ArenaGlobal) + original.Bank);
        int length = original.Stride * original.Capacity;
        NativeBattleEffectArena retained = CopyBytes(NativeBattleEffectTaskMemory.CaptureBank(pool), 0, NativeBattleEffectArena.Clear(), 0, length);
        for (int index = 0; index < original.Capacity; index++)
        {
            uint next = pool.Slots[index].Next is int successor && successor < original.Capacity ? checked(nativeBank + (uint)(successor * original.Stride)) : 0;
            retained.Write(index * original.Stride + 4, BitConverter.GetBytes(next));
        }
        CopyBytes(retained, 0, frame.Memory.Arena ?? throw new NotSupportedException("Effect210 requires its retained COMMON owner."), checked((int)(nativeBank - CommonAddress)), length);
        pool.Revoke();
        return frame with { State = frame.State.AttachCommonQueue(ActDescriptor, NativeBattleEffectArena.Clear()), Effect = frame.Effect with { Pools = frame.Effect.Pools.Remove(pool) } };
    }
}
