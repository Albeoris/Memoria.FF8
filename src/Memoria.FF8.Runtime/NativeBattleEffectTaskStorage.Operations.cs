using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleTaskBankOwner
{
    private sealed class LocalCheckpointCount
    {
        public int Value;
    }

    private static readonly ConditionalWeakTable<NativeBattleWorkingMemory, LocalCheckpointCount> localCheckpointCounts = new();

    internal static bool HasLocalCheckpointFor(NativeBattleWorkingMemory storage) => localCheckpointCounts.TryGetValue(storage, out LocalCheckpointCount? count) && count.Value != 0;
    internal static void RegisterCallerCheckpoint(NativeBattleWorkingMemory storage) => localCheckpointCounts.GetValue(storage, static _ => new()).Value++;
    internal static void UnregisterCallerCheckpoint(NativeBattleWorkingMemory storage)
    {
        LocalCheckpointCount count = localCheckpointCounts.GetValue(storage, static _ => new());
        if (count.Value <= 0)
        {
            throw new InvalidOperationException("Native caller scope lost its physical registry entry.");
        }
        count.Value--;
    }

    private void RegisterLocalCheckpoint() => localCheckpointCounts.GetValue(Storage, static _ => new()).Value++;
    private void UnregisterLocalCheckpoint()
    {
        LocalCheckpointCount count = localCheckpointCounts.GetValue(Storage, static _ => new());
        if (count.Value <= 0)
        {
            throw new InvalidOperationException("Native task local scope lost its physical registry entry.");
        }
        count.Value--;
    }

    private sealed class BindingParticipant(NativeBattleTaskBankOwner owner, NativeBattleWorkingMemory previous) : INativeBattleCheckpointParticipant
    {
        private readonly record struct Receipt(NativeBattleWorkingMemory Storage, int Offset, bool Bound);
        private readonly List<Receipt> receipts = [];
        private bool suspended;

        public void BeginCheckpoint() => receipts.Add(new(owner.Storage, owner.StorageOffset, owner.arenaBound));

        internal void SuspendPrevious()
        {
            if (!ReferenceEquals(previous, owner.Storage))
            {
                previous.SuspendOwnership();
                suspended = true;
            }
        }

        public void CommitCheckpoint()
        {
            RequireReceipt();
            receipts.RemoveAt(receipts.Count - 1);
            if (receipts.Count == 0 && suspended)
            {
                previous.CompleteSuspension();
                suspended = false;
            }
        }

        public void RollbackCheckpoint()
        {
            RequireReceipt();
            Receipt receipt = receipts[^1];
            receipts.RemoveAt(receipts.Count - 1);
            owner.Storage = receipt.Storage;
            owner.StorageOffset = receipt.Offset;
            owner.arenaBound = receipt.Bound;
            if (suspended && ReferenceEquals(owner.Storage, previous))
            {
                previous.ResumeOwnership();
                suspended = false;
            }
        }

        private void RequireReceipt()
        {
            if (receipts.Count == 0)
            {
                throw new InvalidOperationException("Native task binding lost its ownership checkpoint receipt.");
            }
        }
    }
}
