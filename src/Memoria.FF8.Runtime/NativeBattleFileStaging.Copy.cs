namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    /// <summary>Raw bytes and knownness follow memmove; typed pointer metadata is not relocated.</summary>
    public NativeBattleFileStaging CopyMemory(uint source, uint destination, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        RequireWritableRange(source, length);
        RequireWritableRange(destination, length);
        uint lower = Math.Min(BaseAddress, Math.Min(source, destination));
        if ((ulong)Math.Max(source, destination) - lower + (uint)length > int.MaxValue)
        {
            throw new InvalidDataException("Native staging copy exceeds its offset representation.");
        }
        if (Transaction is null)
        {
            using NativeBattleFileStagingTransaction ownedTransaction = new(this);
            ownedTransaction.View.CopyMemory(source, destination, length);
            return ownedTransaction.Freeze(ownedTransaction.View);
        }
        // This is the actual emulated memory copy, not a working-owner conversion.
        (byte[] data, bool[] known) = ReadSnapshot(source, length);
        Rebase(lower);
        NativeBattleTelemetry.Record(1041, NativeBattleTrafficKind.LogicalMemoryCopy, length);
        MarkUnknown(destination, length);
        if (Transaction is { } transaction)
        {
            for (int index = 0; index < length;)
            {
                bool assigned = known[index];
                int first = index++;
                while (index < length && known[index] == assigned)
                {
                    index++;
                }
                transaction.WriteRaw(checked(destination + (uint)first), data.AsSpan(first, index - first), known.AsSpan(first, index - first));
            }
        }
        else
        {
            RequireOwner().WriteWithKnownness(destination, data, known);
        }
        return this;
    }
}
