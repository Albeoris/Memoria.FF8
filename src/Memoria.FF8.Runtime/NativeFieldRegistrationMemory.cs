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
    private const int PageLength = NativeBattleFileStagingWorkingOwner.PageLength;
    private static readonly NativeBattleStagingPage KnownZeroPage = NativeBattleStagingPage.FromOwnedSnapshot(new byte[PageLength], Enumerable.Repeat(true, PageLength).ToArray());
    private static readonly NativeBattleStagingPage UnknownZeroPage = NativeBattleStagingPage.FromOwnedSnapshot(new byte[PageLength], new bool[PageLength]);
    private readonly Dictionary<int, Page> pages = [];
    private readonly Dictionary<uint, NativeBattleResourcePointer?> producedPointers = [];
    private readonly Dictionary<object, NativeFieldRegistrationMemory> resources = new(ReferenceEqualityComparer.Instance);

    private sealed class Page(int length, NativeBattleStagingPage content)
    {
        internal readonly int Length = length;
        internal NativeBattleStagingPage Content = content;
        internal readonly bool[] Changed = new bool[length];
        internal readonly bool[] Publish = new bool[length];

        internal NativeBattleWorkingMemory Writable()
        {
            if (Content.Working is null)
            {
                Content = new(Content.Materialize());
            }
            return Content.Working!;
        }
    }

    internal int MaterializedPageCount => pages.Values.Count(page => page.Content.Working is not null);

    private readonly List<(int First, int End)> observedCpuRanges = [];

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
        NativeBattlePartyCpuLayout layout = cpu.Cpu.EffectiveLayout;
        for (int bank = 0; bank < 3; bank++)
        {
            int first = Index(checked(layout.BaseAddress + (uint)bank * (uint)layout.BankLength), layout.BankLength);
            observedCpuRanges.Add((first, checked(first + layout.BankLength)));
            SetKnown(first, layout.BankLength, false, true);
            foreach (NativeBattleModelResourceSegment range in cpu.Cpu.Banks[bank].GuestSegments)
            {
                Bind(first + range.Offset, range.Source, range.SourceOffset, range.Length, false);
            }
        }
    }

    internal NativeBattleModelCpuResources? ReconcileCpuBanks(NativeBattleModelCpuResources? cpu)
    {
        NativeBattleModelCpuResources current = cpu ?? NativeBattleModelCpuResources.Unknown();
        current.Validate();
        NativeBattlePartyCpuLayout layout = current.Cpu.EffectiveLayout;
        ImmutableArray<NativeBattleModelCpuBank> banks = current.Cpu.Banks;
        Span<byte> currentBytes = stackalloc byte[PageLength];
        Span<bool> currentKnown = stackalloc bool[PageLength];
        for (int bank = 0; bank < 3; bank++)
        {
            int first = Index(checked(layout.BaseAddress + (uint)bank * (uint)layout.BankLength), layout.BankLength);
            if (!AnyChanged(first, layout.BankLength))
            {
                continue;
            }
            byte[] data = new byte[layout.BankLength];
            bool[] observed = new bool[data.Length];
            foreach (NativeBattleModelResourceSegment range in banks[bank].GuestSegments)
            {
                range.Data.Span.CopyTo(data.AsSpan(range.Offset));
                Array.Fill(observed, true, range.Offset, range.Data.Length);
            }
            for (int offset = 0; offset < data.Length;)
            {
                int index = first + offset;
                int count = Math.Min(data.Length - offset, PageLength - index % PageLength);
                if (pages.TryGetValue(index / PageLength, out Page? page))
                {
                    page.Content.ReadWithKnownness(index % PageLength, currentBytes[..count], currentKnown[..count]);
                    for (int item = 0; item < count; item++)
                    {
                        if (page.Changed[index % PageLength + item])
                        {
                            observed[offset + item] = currentKnown[item];
                            data[offset + item] = currentBytes[item];
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
            banks = banks.SetItem(bank, banks[bank] with { KnownRanges = ranges.ToImmutable() });
        }
        return banks.Equals(current.Cpu.Banks) ? cpu : current with { Cpu = current.Cpu with { Banks = banks } };
    }

    internal NativeBattleResourcePointer? ReadResourcePointer(uint address)
    {
        int index = Index(address, 4);
        if (producedPointers.TryGetValue(address, out NativeBattleResourcePointer? pointer))
        {
            return pointer;
        }
        return AnyChanged(index, 4) ? new(null, Read32(address)) : source.ReadResourcePointer(address);
    }

    internal void WriteResourcePointer(uint address, NativeBattleResourcePointer pointer)
    {
        if (pointer.StagingAddress is not uint numeric || pointer.Cpu is not null || pointer.CpuRow is not null || (!pointer.HasValidOwner() || pointer.FieldStorage is { } field && !field.IsValidAt(address)))
        {
            throw new InvalidDataException("Field registration table receipts require their actual numeric projection.");
        }
        Write32(address, numeric);
        producedPointers[address] = pointer;
    }

    private void InvalidateProducedPointers(int index, int count)
    {
        if (count == 0 || producedPointers.Count == 0)
        {
            return;
        }
        uint first = checked(origin + (uint)index);
        foreach (uint address in producedPointers.Keys.Where(address => address < (ulong)first + (uint)count && (ulong)address + 4 > first).ToArray())
        {
            producedPointers.Remove(address);
        }
    }

    private NativeFieldRegistrationMemory Select(NativeFieldResourceOwner? resource)
    {
        RequireActive();
        if (resource is null)
        {
            return this;
        }
        if (resource.Storage is null)
        {
            throw new InvalidDataException("Field cursor has no resource storage authority.");
        }
        object identity = resource.Storage.OwnerIdentity;
        if (!resources.TryGetValue(identity, out NativeFieldRegistrationMemory? memory))
        {
            if (prepared)
            {
                throw new InvalidOperationException("Prepared field registration cannot acquire another resource owner.");
            }
            resource.Validate();
            memory = new(transaction.EnlistFieldResource(resource), 0, checked((uint)resource.Length));
            resources.Add(identity, memory);
        }
        if (memory.length != resource.Length)
        {
            throw new InvalidDataException("Aliased field resource views disagree with their owner bounds.");
        }
        return memory;
    }

    internal byte Read8(NativeFieldResourceCursor cursor) => Select(cursor.Owner).Read8(cursor.Offset);
    internal ushort Read16(NativeFieldResourceCursor cursor) => Select(cursor.Owner).Read16(cursor.Offset);
    internal short ReadSigned16(NativeFieldResourceCursor cursor) => unchecked((short)Read16(cursor));
    internal uint Read32(NativeFieldResourceCursor cursor) => Select(cursor.Owner).Read32(cursor.Offset);
    internal byte[] Read(NativeFieldResourceCursor cursor, int length) => Select(cursor.Owner).Read(cursor.Offset, length);
    internal NativeFieldResourceCursor ReadPointer(NativeFieldResourceCursor cursor) => new(cursor.Owner, Read32(cursor));
    internal void Write(NativeFieldResourceCursor cursor, ReadOnlySpan<byte> bytes) => Select(cursor.Owner).Write(cursor.Offset, bytes);
    internal void Write8(NativeFieldResourceCursor cursor, byte value) => Select(cursor.Owner).Write8(cursor.Offset, value);
    internal void Write16(NativeFieldResourceCursor cursor, ushort value) => Select(cursor.Owner).Write16(cursor.Offset, value);
    internal void Write32(NativeFieldResourceCursor cursor, uint value) => Select(cursor.Owner).Write32(cursor.Offset, value);
    internal void Fill(NativeFieldResourceCursor cursor, int length, byte value) => Select(cursor.Owner).Fill(cursor.Offset, length, value);

    internal void WritePointer(NativeFieldResourceCursor cursor, NativeFieldResourceCursor target)
    {
        if (!ReferenceEquals(cursor.Owner, target.Owner))
        {
            throw new InvalidDataException("A local field pointer cannot cross its selected owner.");
        }
        Write32(cursor, target.Offset);
    }

    internal void Copy(uint source, NativeFieldResourceCursor destination, int length) => Copy(NativeFieldResourceCursor.Guest(source), destination, length);
    internal void Copy(NativeFieldResourceCursor source, uint destination, int length) => Copy(source, NativeFieldResourceCursor.Guest(destination), length);

    internal void Copy(NativeFieldResourceCursor source, NativeFieldResourceCursor destination, int length)
    {
        NativeFieldRegistrationMemory from = Select(source.Owner);
        NativeFieldRegistrationMemory to = Select(destination.Owner);
        if (ReferenceEquals(from, to))
        {
            from.Copy(source.Offset, destination.Offset, length);
            return;
        }
        int first = from.Index(source.Offset, length);
        int target = to.Index(destination.Offset, length);
        for (int copied = 0; copied < length;)
        {
            int position = first + copied;
            Page page = from.GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(length - copied, page.Length - local);
            page.Content = page.Content.Freeze();
            to.CopyImmutable(target + copied, page.Content, local, count);
            copied += count;
        }
    }

    internal void CopyConverted(uint source, NativeFieldResourceCursor destination, int length) => CopyConverted(NativeFieldResourceCursor.Guest(source), destination, length);

    internal void CopyConverted(NativeFieldResourceCursor source, NativeFieldResourceCursor destination, int length)
    {
        NativeFieldRegistrationMemory from = Select(source.Owner);
        NativeFieldRegistrationMemory to = Select(destination.Owner);
        to.RequireMutable();
        int first = from.Index(source.Offset, length);
        int target = to.Index(destination.Offset, length);
        Span<byte> data = stackalloc byte[Math.Min(length, PageLength)];
        Span<bool> known = stackalloc bool[data.Length];
        bool backward = ReferenceEquals(from, to) && target > first && target - first < length;
        for (int copied = 0; copied < length;)
        {
            int count = Math.Min(length - copied, data.Length);
            int offset = backward ? length - copied - count : copied;
            from.Load(first + offset, data[..count], known[..count]);
            to.StoreKnownness(target + offset, data[..count], known[..count]);
            copied += count;
        }
    }

    internal void ProjectGuestPointer(uint address, NativeFieldResourceCursor cursor)
    {
        if (cursor.Owner is null)
        {
            Write32(address, cursor.Offset);
        }
        else
        {
            Forget(address, 4);
        }
    }

    internal void ProjectResourceHeader(NativeFieldResourceCursor resource, uint address)
    {
        Copy(resource, address, 96);
        foreach (int offset in new[] { 0, 4, 8, 12, 16, 20, 24, 28, 32, 64, 84, 88 })
        {
            Forget(address + (uint)offset, 4);
        }
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
        Span<bool> observed = stackalloc bool[PageLength];
        for (int offset = 0; offset < data.Length;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(data.Length - offset, page.Length - local);
            page.Content.ReadWithKnownness(local, data.Slice(offset, count), observed[..count]);
            if (observed[..count].Contains(false))
            {
                throw new NotSupportedException($"Native field registration requires unresolved physical bytes at0x{address:X}.");
            }
            offset += count;
        }
    }

    internal void BindResource(uint address, ImmutableArray<byte> data, int sourceOffset, int length)
    {
        RequireMutable();
        if (data.IsDefault || sourceOffset < 0 || length < 0 || sourceOffset > data.Length - length)
        {
            throw new InvalidDataException("Field resource binding exceeds its immutable source section.");
        }
        Bind(Index(address, length), data, sourceOffset, length, true);
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
        InvalidateProducedPointers(index, length);
        Span<byte> bytes = stackalloc byte[PageLength];
        bytes.Fill(value);
        for (int offset = 0; offset < length;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(length - offset, page.Length - local);
            page.Writable().Write(local, bytes[..count]);
            Mark(page, local, count, true);
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
        bool backward = to > from && to - from < length;
        for (int copied = 0; copied < length;)
        {
            int count = Math.Min(length - copied, PageLength);
            int offset = backward ? length - copied - count : copied;
            int sourceIndex = from + offset;
            Page first = GetPage(sourceIndex);
            int local = sourceIndex % PageLength;
            int firstCount = Math.Min(count, first.Length - local);
            first.Content = first.Content.Freeze();
            NativeBattleStagingPage firstSource = first.Content;
            NativeBattleStagingPage? secondSource = null;
            if (firstCount < count)
            {
                Page second = GetPage(sourceIndex + firstCount);
                second.Content = second.Content.Freeze();
                secondSource = second.Content;
            }
            CopyImmutable(to + offset, firstSource, local, firstCount);
            if (secondSource is not null)
            {
                CopyImmutable(to + offset + firstCount, secondSource, 0, count - firstCount);
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
        foreach (NativeFieldRegistrationMemory resource in resources.Values)
        {
            resource.Prepare();
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
            page.Content = page.Content.Freeze();
            page.Content.Publish(source, checked(origin + (uint)first), local, end - first);
        }
        foreach ((uint address, NativeBattleResourcePointer? pointer) in producedPointers)
        {
            source.SetResourcePointerMetadata(address, pointer);
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
        foreach (NativeFieldRegistrationMemory resource in resources.Values)
        {
            resource.Finish();
        }
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
            foreach (NativeFieldRegistrationMemory resource in resources.Values)
            {
                resource.Dispose();
            }
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
            int count = Math.Min(PageLength, length - first);
            uint address = checked(origin + (uint)first);
            NativeBattleStagingPage content;
            if (source.TryBorrowResource(address, count, out ImmutableArray<byte> resource, out int offset))
            {
                content = NativeBattleStagingPage.Bind(null, 0, resource, offset, count);
            }
            else
            {
                (byte[] bytes, bool[] known) = source.ReadSnapshot(address, count);
                if (bytes.AsSpan().IndexOfAnyExcept((byte)0) < 0 && !known.AsSpan().Contains(false))
                {
                    content = KnownZeroPage;
                }
                else if (bytes.AsSpan().IndexOfAnyExcept((byte)0) < 0 && !known.AsSpan().Contains(true))
                {
                    content = UnknownZeroPage;
                }
                else
                {
                    content = NativeBattleStagingPage.FromOwnedSnapshot(bytes, known);
                }
            }
            page = new(count, content);
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
            int count = Math.Min(data.Length - offset, page.Length - local);
            page.Content.ReadWithKnownness(local, data.Slice(offset, count), observed.Slice(offset, count));
            offset += count;
        }
    }

    private static void Mark(Page page, int offset, int length, bool changed)
    {
        page.Publish.AsSpan(offset, length).Fill(true);
        if (changed)
        {
            page.Changed.AsSpan(offset, length).Fill(true);
        }
    }

    private void Bind(int index, ImmutableArray<byte> resource, int sourceOffset, int length, bool changed)
    {
        if (changed)
        {
            InvalidateProducedPointers(index, length);
        }
        for (int offset = 0; offset < length;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(length - offset, page.Length - local);
            page.Content = NativeBattleStagingPage.Bind(page.Content, local, resource, sourceOffset + offset, count);
            Mark(page, local, count, changed);
            offset += count;
        }
    }

    private void CopyImmutable(int index, NativeBattleStagingPage resource, int sourceOffset, int length)
    {
        InvalidateProducedPointers(index, length);
        for (int offset = 0; offset < length;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(length - offset, page.Length - local);
            if (local != 0 || count != page.Length)
            {
                page.Content = page.Content.Freeze();
            }
            page.Content = NativeBattleStagingPage.Copy(page.Content, local, resource, sourceOffset + offset, count);
            Mark(page, local, count, true);
            offset += count;
        }
    }

    private void Store(int index, ReadOnlySpan<byte> data, bool changed)
    {
        if (changed)
        {
            InvalidateProducedPointers(index, data.Length);
        }
        for (int offset = 0; offset < data.Length;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(data.Length - offset, page.Length - local);
            page.Writable().Write(local, data.Slice(offset, count));
            Mark(page, local, count, changed);
            offset += count;
        }
    }

    private void StoreKnownness(int index, ReadOnlySpan<byte> data, ReadOnlySpan<bool> known)
    {
        InvalidateProducedPointers(index, data.Length);
        for (int offset = 0; offset < data.Length;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int count = Math.Min(data.Length - offset, page.Length - local);
            page.Writable().WriteWithKnownness(local, data.Slice(offset, count), known.Slice(offset, count));
            Mark(page, local, count, true);
            offset += count;
        }
    }

    private void SetKnown(int index, int count, bool value, bool observedCpu)
    {
        Span<byte> bytes = stackalloc byte[PageLength];
        Span<bool> known = stackalloc bool[PageLength];
        known.Fill(value);
        for (int offset = 0; offset < count;)
        {
            int position = index + offset;
            Page page = GetPage(position);
            int local = position % PageLength;
            int take = Math.Min(count - offset, page.Length - local);
            if (value)
            {
                page.Content.ReadWithKnownness(local, bytes[..take], known[..take]);
                known[..take].Fill(true);
                page.Writable().WriteWithKnownness(local, bytes[..take], known[..take]);
            }
            else
            {
                page.Writable().MarkUnknown(local, take);
            }
            Mark(page, local, take, !observedCpu);
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
