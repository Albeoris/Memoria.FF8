using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A single registration transaction over the shared sparse memory; copies preserve unknown bytes.</summary>
internal sealed class NativeFieldRegistrationMemory : IDisposable
{
    private readonly NativeBattleFileStaging source;
    private readonly NativeBattleFileStagingTransaction transaction;
    private readonly bool ownsTransaction;
    private readonly int checkpointDepth;
    private bool completed;
    private bool prepared;
    private bool disposed;
    private readonly uint origin;
    private readonly int length;
    private const int PageLength = 0x4000;
    private readonly Dictionary<int, Page> pages = [];

    private sealed class Page(int length)
    {
        internal readonly byte[] Bytes = new byte[length];
        internal readonly bool[] Known = new bool[length];
        internal readonly bool[] Changed = new bool[length];
        internal readonly bool[] Publish = new bool[length];
    }
    private readonly List<(int First, int End)> observedCpuRanges = [];
    private const uint PartyCpuAddress = 0x196B018;

    internal NativeFieldRegistrationMemory(NativeBattleFileStaging source, uint first, uint end)
    {
        ownsTransaction = source.Transaction is null;
        transaction = source.Transaction ?? new NativeBattleFileStagingTransaction(source);
        if (!ownsTransaction)
        {
            checkpointDepth = transaction.BeginNestedCheckpoint();
        }
        this.source = transaction.View;
        try
        {
            this.source.Rebase(Math.Min(first, source.BaseAddress));
            origin = this.source.BaseAddress;
            ulong last = Math.Max(end, this.source.KnownExtentEnd);
            length = checked((int)(last - origin));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void ObserveCpuBanks(NativeBattleModelCpuResources? cpu)
    {
        RequireMutable();
        if (cpu is null)
        {
            return;
        }
        cpu.Validate();
        for (int bank = 0; bank < 3; bank++)
        {
            int first = Index(checked(PartyCpuAddress + (uint)bank * NativeBattleModelCpuBank.Length), NativeBattleModelCpuBank.Length);
            observedCpuRanges.Add((first, checked(first + NativeBattleModelCpuBank.Length)));
            SetKnown(first, NativeBattleModelCpuBank.Length, false, true);
            foreach (NativeBattleEffectMemoryRegion range in cpu.Cpu.Banks[bank].KnownRanges)
            {
                Store(first + range.Offset, range.Data.AsSpan(), false);
            }
        }
    }

    internal NativeBattleModelCpuResources? ReconcileCpuBanks(NativeBattleModelCpuResources? cpu)
    {
        NativeBattleModelCpuResources current = cpu ?? NativeBattleModelCpuResources.Unknown();
        ImmutableArray<NativeBattleModelCpuBank> banks = current.Cpu.Banks;
        for (int bank = 0; bank < 3; bank++)
        {
            int first = Index(checked(PartyCpuAddress + (uint)bank * NativeBattleModelCpuBank.Length), NativeBattleModelCpuBank.Length);
            if (!AnyChanged(first, NativeBattleModelCpuBank.Length))
            {
                continue;
            }
            byte[] data = new byte[NativeBattleModelCpuBank.Length];
            bool[] observed = new bool[data.Length];
            foreach (NativeBattleEffectMemoryRegion range in banks[bank].KnownRanges)
            {
                range.Data.AsSpan().CopyTo(data.AsSpan(range.Offset));
                Array.Fill(observed, true, range.Offset, range.Data.Length);
            }
            for (int offset = 0; offset < data.Length;)
            {
                int index = first + offset;
                int count = Math.Min(data.Length - offset, PageLength - index % PageLength);
                if (pages.TryGetValue(index / PageLength, out Page? page))
                {
                    for (int item = 0; item < count; item++)
                    {
                        int local = index % PageLength + item;
                        if (page.Changed[local])
                        {
                            observed[offset + item] = page.Known[local];
                            data[offset + item] = page.Bytes[local];
                        }
                    }
                }
                offset += count;
            }
            ImmutableArray<NativeBattleEffectMemoryRegion>.Builder ranges = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
            for (int offset = 0; offset < data.Length;)
            {
                if (!observed[offset])
                {
                    offset++;
                    continue;
                }
                int start = offset++;
                while (offset < data.Length && observed[offset])
                {
                    offset++;
                }
                ranges.Add(new(start, data.AsSpan(start, offset - start).ToArray().ToImmutableArray()));
            }
            banks = banks.SetItem(bank, new(ranges.ToImmutable()));
        }
        return banks.Equals(current.Cpu.Banks) ? cpu : current with { Cpu = current.Cpu with { Banks = banks } };
    }

    internal byte Read8(uint address)
    {
        Span<byte> data = stackalloc byte[1];
        ReadKnown(address, data);
        return data[0];
    }

    internal ushort Read16(uint address)
    {
        Span<byte> data = stackalloc byte[2];
        ReadKnown(address, data);
        return BinaryPrimitives.ReadUInt16LittleEndian(data);
    }

    internal short ReadSigned16(uint address) => unchecked((short)Read16(address));

    internal uint Read32(uint address)
    {
        Span<byte> data = stackalloc byte[4];
        ReadKnown(address, data);
        return BinaryPrimitives.ReadUInt32LittleEndian(data);
    }

    internal ushort? ReadOptional16(uint address)
    {
        int index = Index(address, 2);
        Span<byte> data = stackalloc byte[2];
        Span<bool> observed = stackalloc bool[2];
        Load(index, data, observed);
        return observed.Contains(false) ? null : BinaryPrimitives.ReadUInt16LittleEndian(data);
    }

    internal byte[] Read(uint address, int length)
    {
        _ = Index(address, length);
        byte[] data = new byte[length];
        ReadKnown(address, data);
        return data;
    }

    private void ReadKnown(uint address, Span<byte> data)
    {
        int index = Index(address, data.Length);
        for (int offset = 0; offset < data.Length;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(data.Length - offset, page.Bytes.Length - local);
            if (page.Known.AsSpan(local, count).Contains(false))
            {
                throw new NotSupportedException($"Native field registration requires unresolved physical bytes at0x{address:X}.");
            }
            page.Bytes.AsSpan(local, count).CopyTo(data[offset..]);
            offset += count;
        }
    }

    internal void Write(uint address, ReadOnlySpan<byte> data)
    {
        RequireMutable();
        Store(Index(address, data.Length), data, true);
    }

    internal void Write8(uint address, byte value) => Write(address, [value]);

    internal void Write16(uint address, ushort value)
    {
        Span<byte> data = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(data, value);
        Write(address, data);
    }

    internal void Write32(uint address, uint value)
    {
        Span<byte> data = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, value);
        Write(address, data);
    }

    internal void Fill(uint address, int length, byte value)
    {
        RequireMutable();
        int index = Index(address, length);
        for (int offset = 0; offset < length;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(length - offset, page.Bytes.Length - local);
            page.Bytes.AsSpan(local, count).Fill(value);
            page.Known.AsSpan(local, count).Fill(true);
            page.Changed.AsSpan(local, count).Fill(true);
            page.Publish.AsSpan(local, count).Fill(true);
            offset += count;
        }
    }

    internal void Forget(uint address, int length)
    {
        RequireMutable();
        int index = Index(address, length);
        SetKnown(index, length, false, false);
    }

    internal void Copy(uint sourceAddress, uint destination, int length)
    {
        RequireMutable();
        int from = Index(sourceAddress, length);
        int to = Index(destination, length);
        // Copy toward the source when ranges overlap, preserving both bytes and unknownness.
        Span<byte> data = stackalloc byte[Math.Min(length, PageLength)];
        Span<bool> observed = stackalloc bool[data.Length];
        bool backward = to > from && to - from < length;
        for (int copied = 0; copied < length;)
        {
            int count = Math.Min(length - copied, data.Length);
            int offset = backward ? length - copied - count : copied;
            Load(from + offset, data[..count], observed[..count]);
            Store(to + offset, data[..count], true);
            for (int item = 0; item < count;)
            {
                int position = to + offset + item;
                Page page = GetPage(position);
                int local = position % PageLength;
                int take = Math.Min(count - item, page.Bytes.Length - local);
                observed.Slice(item, take).CopyTo(page.Known.AsSpan(local, take));
                item += take;
            }
            copied += count;
        }
    }

    internal NativeBattleFileStaging Prepare()
    {
        RequireActive();
        if (prepared)
        {
            return source;
        }
        List<(int First, int End)> publish = [];
        foreach ((int number, Page page) in pages)
        {
            for (int index = 0; index < page.Publish.Length;)
            {
                if (!page.Publish[index])
                {
                    index++;
                    continue;
                }
                int first = index++;
                while (index < page.Publish.Length && page.Publish[index])
                {
                    index++;
                }
                publish.Add((number * PageLength + first, number * PageLength + index));
            }
        }
        publish.Sort((left, right) => left.First.CompareTo(right.First));
        // Keep provenance receipts before any store can invalidate the shared metadata.
        List<KeyValuePair<uint, NativeBattleResourcePointer?>> receipts = [];
        if (observedCpuRanges.Count != 0)
        {
            foreach ((uint address, NativeBattleResourcePointer? pointer) in source.EnumerateResourcePointers())
            {
                long offset = (long)address - origin;
                int first = checked((int)Math.Clamp(offset, 0, length));
                int end = checked((int)Math.Clamp(offset + 4, 0, length));
                if (AnyChanged(first, end - first))
                {
                    continue;
                }
                foreach ((int observedFirst, int observedEnd) in observedCpuRanges)
                {
                    if (offset < observedEnd && offset + 4 > observedFirst)
                    {
                        receipts.Add(new(address, pointer));
                        break;
                    }
                }
            }
        }
        for (int range = 0; range < publish.Count; range++)
        {
            int first = publish[range].First;
            int end = publish[range].End;
            Page page = GetPage(first);
            int local = first % PageLength;
            source.WriteWithKnownness(checked(origin + (uint)first), page.Bytes.AsSpan(local, end - first), page.Known.AsSpan(local, end - first));
        }
        foreach ((uint address, NativeBattleResourcePointer? pointer) in receipts)
        {
            source.SetResourcePointerMetadata(address, pointer);
        }
        prepared = true;
        return source;
    }

    internal NativeBattleFileStaging Finish()
    {
        Prepare();
        NativeBattleFileStaging result;
        if (ownsTransaction)
        {
            result = transaction.Freeze(source);
        }
        else
        {
            transaction.CommitNestedCheckpoint(checkpointDepth);
            result = source;
        }
        completed = true;
        return result;
    }

    private void RequireActive()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (completed)
        {
            throw new InvalidOperationException("Native field registration has already published its completed changes.");
        }
    }

    private void RequireMutable()
    {
        RequireActive();
        if (prepared)
        {
            throw new InvalidOperationException("Native field registration must finish its prepared publication before further stores.");
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            if (ownsTransaction)
            {
                transaction.Dispose();
            }
            else if (!completed)
            {
                transaction.RollbackNestedCheckpoint(checkpointDepth);
            }
        }
    }

    private int Index(uint address, int length)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (address < origin || length < 0 || (ulong)address - origin + (uint)length > (uint)this.length)
        {
            throw new InvalidDataException("Native field registration left its shared address domain.");
        }
        return checked((int)(address - origin));
    }

    private Page GetPage(int index)
    {
        int number = index / PageLength;
        if (!pages.TryGetValue(number, out Page? page))
        {
            int first = number * PageLength;
            page = new(Math.Min(PageLength, length - first));
            source.ReadWithKnownness(checked(origin + (uint)first), page.Bytes, page.Known);
            pages.Add(number, page);
        }
        return page;
    }

    private void Load(int index, Span<byte> data, Span<bool> observed)
    {
        for (int offset = 0; offset < data.Length;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(data.Length - offset, page.Bytes.Length - local);
            page.Bytes.AsSpan(local, count).CopyTo(data[offset..]);
            page.Known.AsSpan(local, count).CopyTo(observed[offset..]);
            offset += count;
        }
    }

    private void Store(int index, ReadOnlySpan<byte> data, bool changed)
    {
        for (int offset = 0; offset < data.Length;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(data.Length - offset, page.Bytes.Length - local);
            data.Slice(offset, count).CopyTo(page.Bytes.AsSpan(local, count));
            page.Known.AsSpan(local, count).Fill(true);
            page.Publish.AsSpan(local, count).Fill(true);
            if (changed)
            {
                page.Changed.AsSpan(local, count).Fill(true);
            }
            offset += count;
        }
    }

    private void SetKnown(int index, int count, bool value, bool observedCpu)
    {
        for (int offset = 0; offset < count;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int take = Math.Min(count - offset, page.Bytes.Length - local);
            page.Known.AsSpan(local, take).Fill(value);
            page.Publish.AsSpan(local, take).Fill(true);
            if (!observedCpu)
            {
                page.Changed.AsSpan(local, take).Fill(true);
            }
            offset += take;
        }
    }

    private bool AnyChanged(int index, int count)
    {
        for (int offset = 0; offset < count;)
        {
            int position = index + offset;
            int local = position % PageLength;
            int take = Math.Min(count - offset, PageLength - local);
            if (pages.TryGetValue(position / PageLength, out Page? page) && page.Changed.AsSpan(local, take).Contains(true))
            {
                return true;
            }
            offset += take;
        }
        return false;
    }
}
