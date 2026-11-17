using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed class NativeBattleHostedModelPackets
{
    private const string Model070 = "hosted-model-070";
    private const string Model094 = "hosted-model-094";
    private const int LogicalLength = int.MaxValue & ~3;
    private readonly NativeBattleWorkingMemory?[] banks = new NativeBattleWorkingMemory?[4];
    private readonly int[] cursors = new int[4];

    internal sealed record Snapshot(ImmutableArray<NativeBattleWorkingMemorySnapshot?> Banks, ImmutableArray<int> Cursors);

    internal Snapshot Capture()
    {
        return new(banks.Select(bank => bank?.Capture()).ToImmutableArray(), cursors.ToImmutableArray());
    }

    internal static NativeBattleHostedModelPackets Restore(Snapshot snapshot)
    {
        NativeBattleHostedModelPackets result = new();
        for (int index = 0; index < result.banks.Length; index++)
        {
            result.banks[index] = snapshot.Banks[index] is { } bank ? NativeBattleWorkingMemory.Restore(bank) : null;
            result.cursors[index] = snapshot.Cursors[index];
        }
        return result;
    }

    internal long AllocatedBytes => banks.Sum(bank => (long)(bank?.Capacity ?? 0));
    internal long StoredBytes => banks.Sum(bank => (long)(bank?.StoredLength ?? 0));

    internal static bool IsHostedReference(NativeBattlePrimitiveReference reference)
    {
        return reference.Bank is Model070 or Model094;
    }

    internal int GetCursor(NativeBattlePrimitiveProcess process)
    {
        return cursors[ProcessBank(process)];
    }

    internal void ResetFrame()
    {
        Array.Clear(cursors);
    }

    internal byte[] Read(NativeBattlePrimitiveReference reference, int count)
    {
        int bank = BankIndex(reference.Bank, reference.Buffer);
        ValidateRange(reference.ByteOffset, count);
        NativeBattleWorkingMemory memory = banks[bank] ?? throw new InvalidDataException("Hosted model packet has no retained byte owner.");
        byte[] bytes = new byte[count];
        memory.ReadInto(reference.ByteOffset, bytes);
        return bytes;
    }

    internal NativeBattleLogicalOrdering Append(NativeBattlePrimitiveProcess process, NativeBattleLogicalOrdering ordering, ImmutableArray<NativeBattleGeometryPrimitive> primitives, int? moduleDraw = null, string? textureSourceOwner = null)
    {
        return Append(process, ordering, (NativeBattleModelPrimitiveBatch)primitives, moduleDraw, textureSourceOwner);
    }

    internal NativeBattleLogicalOrdering Append(NativeBattlePrimitiveProcess process, NativeBattleLogicalOrdering ordering, NativeBattleModelPrimitiveBatch primitives, int? moduleDraw = null, string? textureSourceOwner = null)
    {
        if (primitives.IsDefault)
        {
            throw new InvalidDataException("Hosted model packets require an explicit primitive list.");
        }
        int bank = ProcessBank(process);
        int start = cursors[bank];
        ValidateProxyCapacity(ordering, primitives.Count);
        int end = start;
        int largestPacket = 0;
        bool prepareKnownWrites = primitives.UsesRawRows;
        int knownWriteRuns = 0;
        int knownWriteEnd = start;
        ImmutableArray<NativeBattlePrimitiveReference>.Builder? placements = primitives.UsesRawRows && !primitives.IsEmpty ? ImmutableArray.CreateBuilder<NativeBattlePrimitiveReference>(primitives.Count) : null;
        for (int index = 0; index < primitives.Count; index++)
        {
            ImmutableArray<NativeBattleGfPacketByteState>? mask = primitives.ReadPhysicalWriteMask(index);
            int length = ValidatePrimitive(primitives.ReadWords(index), mask);
            ValidateRange(end, length);
            placements?.Add(Reference(bank, end));
            end = checked(end + length);
            largestPacket = Math.Max(largestPacket, length);
            if (prepareKnownWrites)
            {
                bool quad = length == 40;
                prepareKnownWrites = (quad || length == 32) && mask == NativeBattleGeometryPackets.RawPhysicalWriteMask(quad);
                if (prepareKnownWrites)
                {
                    knownWriteRuns += quad ? 2 : 1;
                    knownWriteEnd = end - sizeof(ushort);
                }
            }
        }
        using NativeBattleLogicalOrdering.Builder links = new(ordering);
        if (primitives.IsEmpty)
        {
            return links.ToImmutable();
        }

        NativeBattlePlacedModelPrimitives? placed = placements is null ? null : new(primitives, placements.MoveToImmutable(), moduleDraw, textureSourceOwner);

        // One scratch packet per append. Only the working byte bank retains the encoded writes.
        Span<byte> scratch = largestPacket <= 256 ? stackalloc byte[largestPacket] : new byte[largestPacket];
        NativeBattleWorkingMemory? retained = banks[bank];
        NativeBattleWorkingMemory memory = retained ?? new(LogicalLength, initialKnownZero: false);
        memory.BeginCheckpoint();
        try
        {
            NativeBattleWorkingMemory.NativeBattlePreparedKnownWrites prepared = prepareKnownWrites
                ? memory.PrepareKnownWrites(start, knownWriteEnd - start, knownWriteRuns)
                : default;
            int cursor = start;
            for (int index = 0; index < primitives.Count; index++)
            {
                NativeBattleImmutableSlice<uint> words = primitives.ReadWords(index);
                if (placed is not null)
                {
                    links.PrependPlacedModel(placed, index);
                }
                else
                {
                    links.PrependModel(Reference(bank, cursor), primitives, index, primitives.ReadVertexPayload4(index).IsEmpty ? (ushort)1 : (ushort)0, moduleDraw, textureSourceOwner);
                }
                WriteLinkedPrimitive(memory, ref prepared, cursor, words, primitives.ReadPhysicalWriteMask(index), links.LastHeaderLink, scratch);
                cursor += words.Length * sizeof(uint);
            }
            return Commit(bank, end, links, memory);
        }
        catch
        {
            Rollback(memory, retained);
            throw;
        }
    }

    private static void WriteLinkedPrimitive(NativeBattleWorkingMemory memory, scoped ref NativeBattleWorkingMemory.NativeBattlePreparedKnownWrites prepared, int address, NativeBattleImmutableSlice<uint> words, ImmutableArray<NativeBattleGfPacketByteState>? physicalWriteMask, uint headerLink, scoped Span<byte> scratch)
    {
        if (physicalWriteMask is not { } mask)
        {
            // TODO(TD-009): Other logical emitters do not establish actual padding/rejected stores.
            BinaryPrimitives.WriteUInt32LittleEndian(scratch, headerLink);
            memory.Write(address, scratch[..3]);
            memory.MarkUnknown(address + 3, words.Length * sizeof(uint) - 3);
            return;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(scratch, (words[0] & 0xFF000000u) | (headerLink & 0xFFFFFFu));
        for (int index = 1; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(scratch[(index * sizeof(uint))..], words[index]);
        }

        int linkEnd = 3;
        while (linkEnd < mask.Length && mask[linkEnd] == NativeBattleGfPacketByteState.Known)
        {
            linkEnd++;
        }
        WriteKnownRun(memory, ref prepared, address, scratch[..linkEnd]);
        for (int offset = linkEnd; offset < mask.Length;)
        {
            int end = MaskRunEnd(mask, offset);
            if (mask[offset] == NativeBattleGfPacketByteState.Known)
            {
                WriteKnownRun(memory, ref prepared, address + offset, scratch[offset..end]);
            }
            else
            {
                ApplyMaskedRun(memory, address + offset, scratch[offset..end], mask[offset]);
            }
            offset = end;
        }
    }

    private static void WriteKnownRun(NativeBattleWorkingMemory memory, scoped ref NativeBattleWorkingMemory.NativeBattlePreparedKnownWrites prepared, int offset, scoped ReadOnlySpan<byte> bytes)
    {
        if (prepared.IsPrepared)
        {
            prepared.Write(offset, bytes);
        }
        else
        {
            memory.Write(offset, bytes);
        }
    }

    internal NativeBattleLogicalOrdering Append(NativeBattlePrimitiveProcess process, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, ushort flags20 = 0, int? moduleDraw = null)
    {
        int bank = ProcessBank(process);
        int start = cursors[bank];
        if (batch.Attempts.IsDefault || (batch.AllocationBytes & 3) != 0)
        {
            throw new InvalidDataException("Hosted model packet batch requires an explicit aligned allocation.");
        }
        ValidateRange(start, batch.AllocationBytes);
        int acceptedEnd = 0;
        int acceptedCount = 0;
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            if (attempt is null || attempt.Data.IsDefaultOrEmpty || (attempt.PacketLength & 3) != 0 || (attempt.ByteOffsetRelative & 3) != 0)
            {
                throw new InvalidDataException("Hosted model packet attempt requires explicit aligned packet bytes.");
            }
            ValidateRange(start, attempt.ByteOffsetRelative);
            ValidateRange(checked(start + attempt.ByteOffsetRelative), attempt.PacketLength);
            ValidateMask(attempt.WriteMask, attempt.PacketLength);
            if (attempt.Primitive is { } primitive)
            {
                int end = checked(attempt.ByteOffsetRelative + attempt.PacketLength);
                if (ValidatePrimitive(primitive.Words, primitive.PhysicalWriteMask) > attempt.PacketLength || end > batch.AllocationBytes)
                {
                    throw new InvalidDataException("Hosted linked packet leaves its accepted batch allocation.");
                }
                acceptedEnd = Math.Max(acceptedEnd, end);
                acceptedCount++;
            }
        }
        if (acceptedEnd != batch.AllocationBytes && !batch.NativeUnlinkedAllocation)
        {
            throw new InvalidDataException("Hosted packet batch accepted extent differs from its final cursor.");
        }
        ValidateProxyCapacity(ordering, acceptedCount);

        using NativeBattleLogicalOrdering.Builder links = new(ordering);
        ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            int address = checked(start + attempt.ByteOffsetRelative);
            for (int offset = 0; offset < attempt.PacketLength;)
            {
                int end = MaskRunEnd(attempt.WriteMask, offset);
                if (attempt.WriteMask[offset] != NativeBattleGfPacketByteState.Untouched)
                {
                    links.InvalidatePacketRange(Reference(bank, address + offset), end - offset);
                }
                offset = end;
            }
            stores.Add(new(address, attempt.Data, attempt.WriteMask));
            if (attempt.Primitive is { } primitive)
            {
                AddLink(links, Reference(bank, address), primitive, flags20, moduleDraw, null);
                stores.Add(new(address, ImmutableArray.CreateRange(NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink)), NativeBattleCommonPacketArena.KnownLinkMask));
            }
        }
        return Publish(bank, checked(start + batch.AllocationBytes), links, stores);
    }

    private NativeBattleLogicalOrdering Publish(int bank, int cursor, NativeBattleLogicalOrdering.Builder links, ImmutableArray<NativeBattleCommonPacketStore>.Builder stores)
    {
        if (stores.Count == 0)
        {
            NativeBattleLogicalOrdering result = links.ToImmutable();
            cursors[bank] = cursor;
            return result;
        }
        NativeBattleWorkingMemory? retained = banks[bank];
        NativeBattleWorkingMemory memory = retained ?? new(LogicalLength, initialKnownZero: false);
        memory.BeginCheckpoint();
        try
        {
            foreach (NativeBattleCommonPacketStore store in stores)
            {
                for (int offset = 0; offset < store.Mask.Length;)
                {
                    int end = MaskRunEnd(store.Mask, offset);
                    ApplyMaskedRun(memory, store.Offset + offset, store.Data.AsSpan(offset, end - offset), store.Mask[offset]);
                    offset = end;
                }
            }
            return Commit(bank, cursor, links, memory);
        }
        catch
        {
            Rollback(memory, retained);
            throw;
        }
    }

    private NativeBattleLogicalOrdering Commit(int bank, int cursor, NativeBattleLogicalOrdering.Builder links, NativeBattleWorkingMemory memory)
    {
        NativeBattleLogicalOrdering result = links.ToImmutable();
        memory.CommitCheckpoint();
        banks[bank] = memory;
        cursors[bank] = cursor;
        return result;
    }

    private static void Rollback(NativeBattleWorkingMemory memory, NativeBattleWorkingMemory? retained)
    {
        if (memory.HasCheckpoint)
        {
            memory.RollbackCheckpoint();
        }
        if (retained is null)
        {
            memory.Dispose();
        }
    }

    private static void ApplyMaskedRun(NativeBattleWorkingMemory memory, int address, ReadOnlySpan<byte> data, NativeBattleGfPacketByteState state)
    {
        switch (state)
        {
            case NativeBattleGfPacketByteState.Known:
                memory.Write(address, data);
                break;
            case NativeBattleGfPacketByteState.Unknown:
                memory.MarkUnknown(address, data.Length);
                break;
        }
    }

    private static void AddLink(NativeBattleLogicalOrdering.Builder links, NativeBattlePrimitiveReference reference, NativeBattleGeometryPrimitive primitive, ushort flags20, int? moduleDraw, string? textureSourceOwner)
    {
        links.PrependModel(reference, primitive, flags20, moduleDraw, textureSourceOwner);
    }

    private static int ValidatePrimitive(NativeBattleImmutableSlice<uint> words, ImmutableArray<NativeBattleGfPacketByteState>? physicalWriteMask)
    {
        if (words.IsDefaultOrEmpty || words.Length < 1 + (words[0] >> 24))
        {
            throw new InvalidDataException("Hosted model packet requires complete authored command words.");
        }
        int length = checked(words.Length * sizeof(uint));
        if (physicalWriteMask is { } mask)
        {
            ValidateMask(mask, length);
        }
        return length;
    }

    private static void ValidateMask(ImmutableArray<NativeBattleGfPacketByteState> mask, int length)
    {
        if (mask.IsDefault || mask.Length != length)
        {
            throw new InvalidDataException("Hosted model packet requires an explicit byte write mask matching its extent.");
        }
        foreach (NativeBattleGfPacketByteState state in mask)
        {
            if (state is not (NativeBattleGfPacketByteState.Known or NativeBattleGfPacketByteState.Unknown or NativeBattleGfPacketByteState.Untouched))
            {
                throw new InvalidDataException("Hosted model packet contains an invalid byte write mask state.");
            }
        }
    }

    private static int MaskRunEnd(ImmutableArray<NativeBattleGfPacketByteState> mask, int offset)
    {
        int end = offset + 1;
        while (end < mask.Length && mask[end] == mask[offset])
        {
            end++;
        }
        return end;
    }

    private static void ValidateProxyCapacity(NativeBattleLogicalOrdering ordering, int count)
    {
        int allocated = ordering.ProxyCursor ?? throw new InvalidDataException("Hosted model packets require an initialized ordering proxy cursor.");
        // The native render pass itself refuses a cursor at the physical ceiling.
        if (count > 0 && count >= NativeBattlePrimitiveOrdering.ProxyCapacity - allocated)
        {
            throw new InvalidOperationException("Hosted model packet append exceeds the renderable native proxy capacity.");
        }
    }

    private static void ValidateRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || count > LogicalLength - offset)
        {
            throw new InvalidDataException("Hosted model packet range exceeds its representable byte owner.");
        }
    }

    private static int ProcessBank(NativeBattlePrimitiveProcess process)
    {
        return BankIndex(process.PacketBank switch
        {
            "off_B6D070" => Model070,
            "off_B6D094" => Model094,
            _ => throw new InvalidDataException("Hosted model packets require the selected native draw bank.")
        }, process.PacketBuffer);
    }

    private static int BankIndex(string bank, byte buffer)
    {
        if (buffer > 1 || bank is not (Model070 or Model094))
        {
            throw new InvalidDataException("Hosted model packet requires its actual bank and draw buffer.");
        }
        return (bank == Model070 ? 0 : 2) + buffer;
    }

    private static NativeBattlePrimitiveReference Reference(int bank, int offset)
    {
        return new(bank < 2 ? Model070 : Model094, (byte)(bank & 1), offset);
    }
}
