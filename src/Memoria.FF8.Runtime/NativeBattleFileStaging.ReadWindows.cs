using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

internal readonly record struct NativeBattleStagingReadRange(uint Address, int Length);

internal sealed class NativeBattleStagingReadWindowLease
{
    internal readonly NativeBattleStagingDeltaMap<NativeBattleReadWindow> Pages = new();
    internal readonly NativeBattleStagingDeltaMap<NativeBattleReadWindowByte> InitialCells = new();
    internal readonly List<NativeBattleStagingReadRange> Zeros = [];
    internal readonly List<NativeBattleStagingReadRange> Unknown = [];
    internal readonly List<NativeBattleStagingReadRange> ForgottenZeros = [];
    internal readonly NativeBattleReadWindowBudget Budget = new();
    internal NativeBattleFileStagingWorkingOwner Owner = null!;
    internal NativeBattleFileStagingTransaction? Transaction;
    internal uint Address;
    internal int Length;
    internal long Generation;
    internal bool Active;

    internal void AddCell(uint address, byte value, bool known)
    {
        bool exists = InitialCells.TryGetValue(address, out _);
        if (!exists)
        {
            Budget.Require(1);
        }
        InitialCells[address] = new(value, known);
        if (!exists)
        {
            Budget.Accept(1);
        }
    }

    internal void Require(int offset, int length, long generation)
    {
        if (!Active || Generation != generation)
        {
            throw new ObjectDisposedException(nameof(NativeBattleStagingReadWindow));
        }
        Transaction?.RequireActive();
        if (offset < 0 || length < 0 || offset > Length - length)
        {
            throw new InvalidDataException("Native temporal staging read leaves its original window.");
        }
    }

    internal void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        for (int copied = 0; copied < destination.Length;)
        {
            uint address = checked(Address + (uint)offset + (uint)copied);
            uint pageAddress = address / NativeBattleFileStagingWorkingOwner.PageLength * NativeBattleFileStagingWorkingOwner.PageLength;
            int first = (int)(address - pageAddress);
            int count = Math.Min(destination.Length - copied, NativeBattleFileStagingWorkingOwner.PageLength - first);
            Span<byte> bytes = destination.Slice(copied, count);
            Span<bool> flags = known.Slice(copied, count);
            if (Pages.TryGetValue(pageAddress, out NativeBattleReadWindow page))
            {
                page.ReadWithKnownness(first - (int)Math.Max(0L, (long)Address - pageAddress), bytes, flags);
            }
            else
            {
                bytes.Clear();
                flags.Clear();
                NativeBattleTelemetry.Record(1035, NativeBattleTrafficKind.ZeroFill, count * 2L);
            }
            for (int index = 0; index < count; index++)
            {
                uint current = address + (uint)index;
                bool explicitKnown = flags[index];
                if (!explicitKnown && Covers(Zeros, current))
                {
                    bytes[index] = 0;
                    flags[index] = true;
                }
                if (InitialCells.TryGetValue(current, out NativeBattleReadWindowByte cell))
                {
                    bytes[index] = cell.Value;
                    flags[index] = cell.Known;
                }
                else if (Covers(Unknown, current) || !explicitKnown && Covers(ForgottenZeros, current))
                {
                    flags[index] = false;
                }
            }
            copied += count;
        }
    }

    private static bool Covers(List<NativeBattleStagingReadRange> ranges, uint address)
    {
        foreach (NativeBattleStagingReadRange range in ranges)
        {
            if (address >= range.Address && (ulong)address < (ulong)range.Address + (uint)range.Length)
            {
                return true;
            }
        }
        return false;
    }

    internal void Reset()
    {
        foreach (var entry in Pages)
        {
            entry.Value.Dispose();
        }
        Pages.Clear();
        Budget.Release(InitialCells.Count);
        InitialCells.Clear();
        Zeros.Clear();
        Unknown.Clear();
        ForgottenZeros.Clear();
        Transaction = null;
        Active = false;
    }
}

/// <summary>Original staging bytes, sparse overlay delta and loader-zero proof retained for one callback scope.</summary>
internal readonly struct NativeBattleStagingReadWindow : IDisposable
{
    private readonly NativeBattleStagingReadWindowLease? lease;
    private readonly long generation;

    internal NativeBattleStagingReadWindow(NativeBattleStagingReadWindowLease lease)
    {
        this.lease = lease;
        generation = lease.Generation;
    }

    private NativeBattleStagingReadWindowLease Require(int offset, int length)
    {
        if (lease is null)
        {
            throw new ObjectDisposedException(nameof(NativeBattleStagingReadWindow));
        }
        lease.Require(offset, length, generation);
        return lease;
    }

    public int Length => Require(0, 0).Length;
    internal int RetainedByteCount => Require(0, 0).Budget.Retained;

    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        NativeBattleStagingReadWindowLease current = Require(offset, destination.Length);
        if (destination.Length != known.Length)
        {
            throw new ArgumentException("Native staging temporal byte and knownness destinations must match.", nameof(known));
        }
        current.ReadWithKnownness(offset, destination, known);
    }

    public void ReadInto(int offset, Span<byte> destination)
    {
        NativeBattleStagingReadWindowLease current = Require(offset, destination.Length);
        Span<byte> scratch = stackalloc byte[256];
        Span<bool> known = stackalloc bool[256];
        for (int copied = 0; copied < destination.Length;)
        {
            int count = Math.Min(scratch.Length, destination.Length - copied);
            current.ReadWithKnownness(offset + copied, scratch[..count], known[..count]);
            if (known[..count].Contains(false))
            {
                throw new NotSupportedException("Native temporal staging read intersects original unknown bytes.");
            }
            copied += count;
        }
        for (int copied = 0; copied < destination.Length;)
        {
            int count = Math.Min(scratch.Length, destination.Length - copied);
            current.ReadWithKnownness(offset + copied, destination.Slice(copied, count), known[..count]);
            copied += count;
        }
    }

    private void ReadScalar(int offset, Span<byte> bytes)
    {
        Span<bool> known = stackalloc bool[4];
        ReadWithKnownness(offset, bytes, known[..bytes.Length]);
        if (known[..bytes.Length].Contains(false))
        {
            throw new NotSupportedException("Native temporal staging read intersects original unknown bytes.");
        }
    }

    public byte ReadByte(int offset)
    {
        Span<byte> bytes = stackalloc byte[1];
        ReadScalar(offset, bytes);
        return bytes[0];
    }

    public ushort ReadUInt16(int offset)
    {
        Span<byte> bytes = stackalloc byte[2];
        ReadScalar(offset, bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    public short ReadInt16(int offset) => unchecked((short)ReadUInt16(offset));

    public uint ReadUInt32(int offset)
    {
        Span<byte> bytes = stackalloc byte[4];
        ReadScalar(offset, bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    public int ReadInt32(int offset) => unchecked((int)ReadUInt32(offset));
    public void Dispose() => lease?.Owner.CloseReadWindow(lease, generation);
}

public sealed partial record NativeBattleFileStaging
{
    internal NativeBattleStagingReadWindow OpenReadWindow(uint address, int length, int maximumRetainedBytes = 1_048_576)
    {
        RequireOffsetRange(address, length);
        if (snapshot)
        {
            throw new InvalidOperationException("Immutable staging storage does not require a temporal observation window.");
        }
        return RequireOwner().OpenReadWindow(Transaction, address, length, maximumRetainedBytes);
    }
}

internal sealed partial class NativeBattleFileStagingWorkingOwner
{
    private readonly List<NativeBattleStagingReadWindowLease> readWindows = [];
    private readonly Stack<NativeBattleStagingReadWindowLease> availableReadWindows = [];
    private bool openingReadWindow;

    internal NativeBattleStagingReadWindow OpenReadWindow(NativeBattleFileStagingTransaction? transaction, uint address, int length, int maximumRetainedBytes)
    {
        RequireReadWindowMutation();
        transaction?.RequireActive();
        if (maximumRetainedBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRetainedBytes));
        }
        readWindows.EnsureCapacity(readWindows.Count + 1);
        availableReadWindows.EnsureCapacity(availableReadWindows.Count + readWindows.Count + (availableReadWindows.Count == 0 ? 1 : 0));
        NativeBattleStagingReadWindowLease lease = availableReadWindows.Count == 0 ? new() : availableReadWindows.Pop();
        lease.Owner = this;
        lease.Transaction = transaction;
        lease.Address = address;
        lease.Length = length;
        lease.Generation = checked(lease.Generation + 1);
        lease.Budget.Reset(maximumRetainedBytes);
        openingReadWindow = true;
        try
        {
            foreach (NativeBattleLoaderZeroRegion zero in zeros)
            {
                AddReadWindowRange(lease.Zeros, zero.Address, zero.Length, address, length);
            }
            transaction?.CaptureReadWindowPrefix(lease);
            ulong end = (ulong)address + (uint)length;
            for (ulong pageAddress = address / PageLength * PageLength; pageAddress < end && length != 0; pageAddress += PageLength)
            {
                if (!pages.TryGetValue((uint)pageAddress, out NativeBattleWorkingMemory? page))
                {
                    continue;
                }
                int first = (int)Math.Max(0L, (long)address - (long)pageAddress);
                int count = (int)(Math.Min(end, pageAddress + PageLength) - pageAddress) - first;
                NativeBattleReadWindow observation = page.OpenReadWindow(first, count, lease.Budget, maximumRetainedBytes);
                try
                {
                    lease.Pages[(uint)pageAddress] = observation;
                }
                catch
                {
                    observation.Dispose();
                    throw;
                }
            }
            transaction?.RequireActive();
            lease.Active = true;
            readWindows.Add(lease);
            return new(lease);
        }
        catch
        {
            lease.Reset();
            availableReadWindows.Push(lease);
            throw;
        }
        finally
        {
            openingReadWindow = false;
        }
    }

    internal static void AddReadWindowRange(List<NativeBattleStagingReadRange> output, uint address, int length, uint windowAddress, int windowLength)
    {
        ulong first = Math.Max(address, windowAddress);
        ulong end = Math.Min((ulong)address + (uint)length, (ulong)windowAddress + (uint)windowLength);
        if (first < end)
        {
            output.Add(new((uint)first, (int)(end - first)));
        }
    }

    internal void RequireReadWindowMutation()
    {
        if (openingReadWindow)
        {
            throw new InvalidOperationException("Native staging observation opening cannot be reentered by a mutation.");
        }
    }

    internal void PrepareReadWindowWrite(uint address, int length, bool materializedOnly = false)
    {
        RequireReadWindowMutation();
        ulong end = (ulong)address + (uint)length;
        for (ulong pageAddress = address / PageLength * PageLength; pageAddress < end && length != 0; pageAddress += PageLength)
        {
            if (pages.TryGetValue((uint)pageAddress, out NativeBattleWorkingMemory? page))
            {
                int first = (int)Math.Max(0L, (long)address - (long)pageAddress);
                ulong last = Math.Min(end, pageAddress + (uint)(materializedOnly ? page.StoredLength : PageLength));
                int count = (int)(last - pageAddress) - first;
                if (count > 0)
                {
                    page.PrepareReadWindowWrite(first, count);
                }
            }
        }
    }

    internal void CloseReadWindow(NativeBattleStagingReadWindowLease lease, long generation)
    {
        if (!lease.Active || lease.Generation != generation)
        {
            return;
        }
        if (openingReadWindow)
        {
            throw new InvalidOperationException("Native staging observation cannot close while another scope opens.");
        }
        readWindows.Remove(lease);
        lease.Reset();
        availableReadWindows.Push(lease);
    }

    internal void CloseReadWindows(NativeBattleFileStagingTransaction transaction)
    {
        for (int index = readWindows.Count - 1; index >= 0; index--)
        {
            NativeBattleStagingReadWindowLease lease = readWindows[index];
            if (ReferenceEquals(transaction, lease.Transaction))
            {
                CloseReadWindow(lease, lease.Generation);
            }
        }
    }
}

internal sealed partial class NativeBattleFileStagingOverlay
{
    internal void CaptureReadWindowPrefix(NativeBattleStagingReadWindowLease lease)
    {
        RequireLayerActive();
        parent?.CaptureReadWindowPrefix(lease);
        foreach (var entry in lease.InitialCells)
        {
            if (LastUnknown(entry.Key) != 0)
            {
                lease.AddCell(entry.Key, entry.Value.Value, false);
            }
        }
        ulong end = (ulong)lease.Address + (uint)lease.Length;
        int copied = 0;
        try
        {
            foreach (var entry in bytes)
            {
                if (entry.Key >= lease.Address && entry.Key < end)
                {
                    lease.AddCell(entry.Key, entry.Value.Data, entry.Value.Sequence >= LastUnknown(entry.Key));
                    copied++;
                }
            }
        }
        finally
        {
            if (copied != 0)
            {
                NativeBattleTelemetry.Record(1035, NativeBattleTrafficKind.CpuCopyPayload, copied * 2L);
            }
        }
        foreach (Unknown range in unknown)
        {
            NativeBattleFileStagingWorkingOwner.AddReadWindowRange(lease.Unknown, range.Address, range.Length, lease.Address, lease.Length);
        }
        foreach (NativeBattleLoaderZeroRegion range in forgottenZeros)
        {
            NativeBattleFileStagingWorkingOwner.AddReadWindowRange(lease.ForgottenZeros, range.Address, range.Length, lease.Address, lease.Length);
        }
    }
}
