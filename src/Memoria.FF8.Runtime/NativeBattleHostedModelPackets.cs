using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed class NativeBattleHostedModelPackets
{
    private const string Model070 = "hosted-model-070";
    private const string Model094 = "hosted-model-094";
    private const int LogicalLength = int.MaxValue & ~3;
    private readonly NativeBattleWorkingMemory?[] banks = new NativeBattleWorkingMemory?[4];
    private readonly int[] cursors = new int[4];

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
        if (primitives.IsDefault)
        {
            throw new InvalidDataException("Hosted model packets require an explicit primitive list.");
        }
        int bank = ProcessBank(process);
        int start = cursors[bank];
        ValidateProxyCapacity(ordering, primitives.Length);
        int end = start;
        foreach (NativeBattleGeometryPrimitive primitive in primitives)
        {
            int length = ValidatePrimitive(primitive);
            ValidateRange(end, length);
            end = checked(end + length);
        }

        ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>(primitives.Length);
        using NativeBattleLogicalOrdering.Builder links = new(ordering);
        int cursor = start;
        foreach (NativeBattleGeometryPrimitive primitive in primitives)
        {
            AddLink(links, Reference(bank, cursor), primitive, primitive.VertexPayload4.IsEmpty ? (ushort)1 : (ushort)0, moduleDraw, textureSourceOwner);
            stores.Add(NativeBattleCommonPacketArena.CreateLinkedPrimitiveStore(cursor, primitive, links.LastHeaderLink));
            cursor += primitive.Words.Length * sizeof(uint);
        }
        return Publish(bank, end, links, stores);
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
                if (ValidatePrimitive(primitive) > attempt.PacketLength || end > batch.AllocationBytes)
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
                    switch (store.Mask[offset])
                    {
                        case NativeBattleGfPacketByteState.Known:
                            memory.Write(store.Offset + offset, store.Data.AsSpan(offset, end - offset));
                            break;
                        case NativeBattleGfPacketByteState.Unknown:
                            memory.MarkUnknown(store.Offset + offset, end - offset);
                            break;
                    }
                    offset = end;
                }
            }
            NativeBattleLogicalOrdering result = links.ToImmutable();
            memory.CommitCheckpoint();
            banks[bank] = memory;
            cursors[bank] = cursor;
            return result;
        }
        catch
        {
            if (memory.HasCheckpoint)
            {
                memory.RollbackCheckpoint();
            }
            if (retained is null)
            {
                memory.Dispose();
            }
            throw;
        }
    }

    private static void AddLink(NativeBattleLogicalOrdering.Builder links, NativeBattlePrimitiveReference reference, NativeBattleGeometryPrimitive primitive, ushort flags20, int? moduleDraw, string? textureSourceOwner)
    {
        links.Prepend(primitive.Bucket, reference, primitive.Words, flags20, primitive.VertexPayload4, moduleDraw, primitive.ProjectedCoordinates, textureSourceOwner, primitive.PilotGeometry, primitive.PresentationActorId, primitive.ScreenCoverage, primitive.SceneCapture, primitive.ModelTextureOwner);
    }

    private static int ValidatePrimitive(NativeBattleGeometryPrimitive primitive)
    {
        if (primitive is null || primitive.Words.IsDefaultOrEmpty || primitive.Words.Length < 1 + (primitive.Words[0] >> 24))
        {
            throw new InvalidDataException("Hosted model packet requires complete authored command words.");
        }
        int length = checked(primitive.Words.Length * sizeof(uint));
        if (primitive.PhysicalWriteMask is { } mask)
        {
            ValidateMask(mask, length);
        }
        return length;
    }

    private static void ValidateMask(ImmutableArray<NativeBattleGfPacketByteState> mask, int length)
    {
        if (mask.IsDefault || mask.Length != length || mask.Any(value => value is not (NativeBattleGfPacketByteState.Known or NativeBattleGfPacketByteState.Unknown or NativeBattleGfPacketByteState.Untouched)))
        {
            throw new InvalidDataException("Hosted model packet requires an explicit byte write mask matching its extent.");
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
