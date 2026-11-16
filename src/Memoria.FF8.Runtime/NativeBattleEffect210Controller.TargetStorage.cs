namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleEffect210TargetRecords(uint MainAddress, uint Address, int Count, NativeBattleEffectArena Rows)
{
    internal const int OriginalCount = 3;
    internal const ushort Stride = 24;
    internal int Length => checked((Count - OriginalCount) * Stride);
    internal bool FinalPoolAdmitted { get; init; }

    internal void Validate()
    {
        if (Count <= OriginalCount || Count > ushort.MaxValue || Length > NativeBattleEffectArena.Length || !NativeBattlePresentationTaskStorage.IsHostedBank(Address, Stride, Count - OriginalCount))
        {
            throw new InvalidDataException("Effect210 target overflow leaves its captured hosted extent.");
        }
        Rows.Validate();
        Rows.RequireRange(0, Length);
        Rows.ValidateExtent(Length);
    }

    internal bool Contains(uint address, int length)
    {
        if (address < Address || (ulong)address >= (ulong)Address + (uint)Length)
        {
            return false;
        }
        if (length < 0 || (ulong)address + (uint)length > (ulong)Address + (uint)Length)
        {
            throw new InvalidDataException("Effect210 target access exceeds its hosted record owner.");
        }
        return true;
    }
}

public static partial class NativeBattleEffect210Controller
{
    private static NativeBattleEffect210Frame ReserveTargetRecords(NativeBattleEffect210Frame frame, uint main, int count, NativeBattleActionPacketsState packets, NativeBattleEffect210Inputs inputs)
    {
        if (count <= NativeBattleEffect210TargetRecords.OriginalCount)
        {
            return frame with { State = frame.State with { TargetRecords = null } };
        }
        if (inputs.StorageContract != BattleContactTaskStorageContract.ExpandableArena)
        {
            throw new NotSupportedException("Effect210 original target records overlap its center and echo workspace above three contacts.");
        }
        int length = checked((count - NativeBattleEffect210TargetRecords.OriginalCount) * NativeBattleEffect210TargetRecords.Stride);
        if (length > NativeBattleEffectArena.Length)
        {
            throw new NotSupportedException("Effect210 captured targets exceed the hosted record extent.");
        }
        (NativeBattleEffectMemory memory, uint address) = NativeBattlePresentationTaskStorage.ReserveByteRange(frame.Memory, frame.Effect, packets.Roster, length);
        NativeBattleEffectArena rows = NativeBattleEffectArena.BorrowWorking(new NativeBattleWorkingMemory(length), 0, length);
        return frame with { Memory = memory, State = frame.State with { TargetRecords = new(main, address, count, rows) } };
    }

    private static int TargetRecordCount(NativeBattleEffect210Frame frame, uint main)
    {
        int count = Read(frame, main).ReadWord(14);
        NativeBattleEffect210TargetRecords? overflow = frame.State.TargetRecords;
        if (overflow is not null)
        {
            overflow.Validate();
            if (overflow.MainAddress != main || overflow.Count != count)
            {
                throw new InvalidDataException("Effect210 target records differ from their captured main owner or count.");
            }
        }
        else if (count > NativeBattleEffect210TargetRecords.OriginalCount)
        {
            throw new NotSupportedException("Effect210 requires its captured hosted target records.");
        }
        return count;
    }

    internal static uint TargetRecordAddress(NativeBattleEffect210Frame frame, uint main, int index)
    {
        int count = TargetRecordCount(frame, main);
        if ((uint)index >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return index < NativeBattleEffect210TargetRecords.OriginalCount
            ? checked(main + 16u + (uint)(index * NativeBattleEffect210TargetRecords.Stride))
            : checked(frame.State.TargetRecords!.Address + (uint)((index - NativeBattleEffect210TargetRecords.OriginalCount) * NativeBattleEffect210TargetRecords.Stride));
    }
}
