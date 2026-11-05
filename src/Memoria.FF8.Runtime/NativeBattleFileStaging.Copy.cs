using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    private readonly record struct ResourceMoveSlice(int Offset, ImmutableArray<byte> Source, int SourceOffset, int Length, ImmutableArray<bool> Known);

    /// <summary>Raw memmove with immutable source sharing and copy-on-write destination ownership; pointer receipts are invalidated.</summary>
    internal NativeBattleFileStaging CopyResourceMemory(uint source, uint destination, int length)
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
            ownedTransaction.View.CopyResourceMemory(source, destination, length);
            return ownedTransaction.Freeze(ownedTransaction.View);
        }
        List<ResourceMoveSlice> slices = [];
        for (int offset = 0; offset < length;)
        {
            int count = Math.Min(NativeBattleFileStagingWorkingOwner.PageLength, length - offset);
            uint address = checked(source + (uint)offset);
            if (TryBorrowResource(address, count, out ImmutableArray<byte> bytes, out int sourceOffset))
            {
                slices.Add(new(offset, bytes, sourceOffset, count, default));
            }
            else
            {
                (byte[] data, bool[] known) = ReadSnapshot(address, count);
                slices.Add(new(offset, ImmutableCollectionsMarshal.AsImmutableArray(data), 0, count, known.All(value => value) ? default : ImmutableCollectionsMarshal.AsImmutableArray(known)));
            }
            offset += count;
        }
        Rebase(lower);
        NativeBattleTelemetry.Record(1041, NativeBattleTrafficKind.LogicalMemoryCopy, length);
        MarkUnknown(destination, length);
        foreach (ResourceMoveSlice slice in slices)
        {
            if (slice.Known.IsDefault)
            {
                BindResource(checked(destination + (uint)slice.Offset), slice.Source, slice.SourceOffset, slice.Length);
                continue;
            }
            for (int index = 0; index < slice.Length;)
            {
                bool assigned = slice.Known[index];
                int first = index++;
                while (index < slice.Length && slice.Known[index] == assigned)
                {
                    index++;
                }
                uint target = checked(destination + (uint)(slice.Offset + first));
                if (assigned)
                {
                    BindResource(target, slice.Source, slice.SourceOffset + first, index - first);
                }
                else
                {
                    Transaction!.WriteRaw(target, slice.Source.AsSpan(slice.SourceOffset + first, index - first), slice.Known.AsSpan(first, index - first));
                }
            }
        }
        return this;
    }

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
