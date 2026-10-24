namespace Memoria.FF8.Runtime;

public interface IMagicInventory
{
    int BankId { get; }
    IReadOnlyList<NativeMagicStock> Stock { get; }
    int Count(int spellId);
    int Capacity(int spellId);
}

public static class MagicInventoryResolver
{
    public static IMagicInventory Resolve(NativeCharacterVitals vitals, int physicalRow) => new Inventory(vitals, physicalRow);
    private sealed class Inventory(NativeCharacterVitals vitals, int row) : IMagicInventory
    {
        public int BankId => vitals.MagicSystem.SharedMagicStock ? -1 : row;
        public IReadOnlyList<NativeMagicStock> Stock => vitals.GetMagicInventory(row);
        public int Count(int spellId) => vitals.GetMagicCount(row, spellId);
        public int Capacity(int spellId) => vitals.GetMagicCapacity(row, spellId) == 0 ? 100 - Count(spellId) : 0;
    }
}

public sealed partial record NativeCharacterVitals
{
    public MagicSystemState MagicSystem { get; init; } = new();

    public IReadOnlyList<NativeMagicStock> GetMagicInventory(int physicalRow)
    {
        if ((uint)physicalRow >= 8)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalRow));
        }
        int bank = MagicSystem.SharedMagicStock ? 0 : physicalRow;
        IEnumerable<NativeMagicStock> stock = Records[bank].MagicInventory.Where(s => s.SpellId != 0 && s.Count != 0);
        if (bank == 0)
        {
            stock = stock.Concat(MagicSystem.RecordZeroExtension);
        }
        return Array.AsReadOnly(stock.ToArray());
    }

    public int GetMagicCount(int physicalRow, int spellId) => GetMagicInventory(physicalRow).FirstOrDefault(stock => stock.SpellId == spellId)?.Count ?? 0;

    public int GetMagicCapacity(int physicalRow, int spellId)
    {
        if (spellId is < 1 or > 56)
        {
            return 2;
        }
        int count = GetMagicCount(physicalRow, spellId);
        if (count != 0)
        {
            return count >= 100 ? 1 : 0;
        }
        return MagicSystem.SharedMagicStock || GetMagicInventory(physicalRow).Count < 32 ? 0 : 2;
    }

    public NativeCharacterVitals ChangeMagicStock(int physicalRow, int spellId, int delta, MagicOperationReason reason, out MagicStockMutation result)
    {
        if (spellId is < 1 or > 56 || !Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(spellId));
        }
        int bank = MagicSystem.SharedMagicStock ? 0 : physicalRow;
        int count = GetMagicCount(physicalRow, spellId);
        int requested = checked((int)Math.Abs((long)delta));
        int accepted = delta > 0 ? GetMagicCapacity(physicalRow, spellId) == 0 ? Math.Min(requested, 100 - count) : 0 : Math.Min(requested, count);
        result = new MagicStockMutation(MagicSystem.SharedMagicStock ? -1 : bank, spellId, requested, accepted, requested - accepted);
        if (accepted == 0)
        {
            return this;
        }
        NativeCharacterVitals next = SetPhysicalStock(bank, spellId, count + Math.Sign(delta) * accepted);
        MagicJunctionChange[] removed = [];
        if (delta < 0)
        {
            int? affectedRow = MagicSystem.SharedMagicStock ? null : bank;
            next = count == accepted ? MagicJunctionPolicy.Reconcile(next, out removed, affectedRow) : MagicJunctionPolicy.ClampHitPoints(next, affectedRow);
        }
        result = result with { AffectedRecords = Array.AsReadOnly(Enumerable.Range(0, 8).Where(row => row == bank || MagicSystem.SharedMagicStock && Records[row].Junction.MagicJunctions.Take(19).Contains((byte)spellId)).ToArray()), RemovedJunctions = Array.AsReadOnly(removed) };
        return next;
    }

    internal NativeCharacterVitals SetPhysicalStock(int bank, int spellId, int count)
    {
        NativeCharacterVitals next = DeepCopy();
        NativeMagicStock[] native = next.Records[bank].MagicInventory;
        int slot = Array.FindIndex(native, s => s.SpellId == spellId && s.Count != 0);
        List<NativeMagicStock> extension = next.MagicSystem.RecordZeroExtension.ToList();
        int extra = bank == 0 ? extension.FindIndex(s => s.SpellId == spellId) : -1;
        if (slot >= 0)
        {
            native[slot] = count == 0 ? new NativeMagicStock(0, 0) : new NativeMagicStock((byte)spellId, (byte)count);
        }
        else if (extra >= 0)
        {
            if (count == 0)
            {
                extension.RemoveAt(extra);
            }
            else
            {
                extension[extra] = new NativeMagicStock((byte)spellId, (byte)count);
            }
        }
        else if (count > 0)
        {
            slot = Array.FindIndex(native, s => s.SpellId == 0 || s.Count == 0);
            if (slot >= 0)
            {
                native[slot] = new NativeMagicStock((byte)spellId, (byte)count);
            }
            else if (bank == 0)
            {
                extension.Add(new NativeMagicStock((byte)spellId, (byte)count));
            }
            else
            {
                throw new InvalidOperationException("Personal magic bank has no free native slot.");
            }
        }
        return next with { MagicSystem = next.MagicSystem with { RecordZeroExtension = extension.ToArray() } };
    }

    public NativeCharacterVitals TransferMagicStock(int sourceRow, int targetRow, int spellId, int amount, out MagicStockMutation result)
    {
        if (MagicSystem.SharedMagicStock)
        {
            throw new InvalidOperationException("Magic transfer is unavailable with shared storage.");
        }
        if (amount < 0 || sourceRow == targetRow)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }
        int accepted = GetMagicCapacity(targetRow, spellId) == 0 ? Math.Min(amount, Math.Min(GetMagicCount(sourceRow, spellId), 100 - GetMagicCount(targetRow, spellId))) : 0;
        result = new MagicStockMutation(targetRow, spellId, amount, accepted, amount - accepted);
        if (accepted == 0)
        {
            return this;
        }
        NativeCharacterVitals next = ChangeMagicStock(sourceRow, spellId, -accepted, MagicOperationReason.Transfer, out _).ChangeMagicStock(targetRow, spellId, accepted, MagicOperationReason.Transfer, out _);
        return MagicCastingService.PropagatePersonalHistory(next, sourceRow, targetRow, spellId);
    }
}
