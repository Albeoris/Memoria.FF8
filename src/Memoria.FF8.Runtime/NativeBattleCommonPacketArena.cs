using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCommonPacketWrite(NativeBattlePrimitiveProcess Process, NativeBattleLogicalOrdering Ordering);

/// <summary>Apply exact ordered common-heap stores, rejected attempts and accepted native OT links.</summary>
public static class NativeBattleCommonPacketArena
{
    internal static readonly ImmutableArray<NativeBattleGfPacketByteState> KnownLinkMask = [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known];
    private static readonly ImmutableArray<NativeBattleGfPacketByteState> UnknownLinkMask = [NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Unknown];
    private static readonly ImmutableArray<byte> UnknownLinkBytes = [0, 0, 0];

    public static NativeBattlePrimitiveProcess StorePrimitive(NativeBattlePrimitiveProcess process, int address, NativeBattleGeometryPrimitive primitive, bool linked)
    {
        return process.ApplyCommonWrites(PrimitiveStores(address, primitive, linked));
    }

    public static ImmutableArray<NativeBattleCommonPacketStore> PrimitiveStores(int address, NativeBattleGeometryPrimitive primitive, bool linked)
    {
        if (primitive.PhysicalWriteMask is not { } mask)
        {
            // TODO(TD-009): Other logical emitters do not yet establish actual padding/rejected stores.
            int length = checked(4 * primitive.Words.Length);
            return [new(address, ImmutableCollectionsMarshal.AsImmutableArray(new byte[length]), Enumerable.Repeat(NativeBattleGfPacketByteState.Unknown, length).ToImmutableArray())];
        }
        byte[] data = new byte[checked(4 * primitive.Words.Length)];
        for (int index = 0; index < primitive.Words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4 * index), primitive.Words[index]);
        }
        NativeBattleCommonPacketStore store = new(address, ImmutableCollectionsMarshal.AsImmutableArray(data), mask);
        return linked ? [store, new(address, UnknownLinkBytes, UnknownLinkMask)] : [store];
    }

    public static NativeBattleCommonPacketWrite Append(NativeBattlePrimitiveProcess process, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, ushort flags20 = 0, int? moduleDraw = null)
    {
        process.Validate();
        int start = process.ByteCursor ?? throw new NotSupportedException("Native common packet receipts require their allocated current cursor.");
        string bank = process.PacketBank ?? throw new NotSupportedException("Native common packet receipts require their allocated physical bank.");
        int capacity = bank == "off_B6D070" ? 114688 : 147456;
        if (batch.Attempts.IsDefault || batch.AllocationBytes < 0 || (batch.AllocationBytes & 3) != 0 || start > capacity - batch.AllocationBytes)
        {
            throw new InvalidDataException("Native common packet receipts require their bounded aligned final allocation.");
        }
        int acceptedEnd = 0;
        NativeBattleLogicalOrdering.Builder links = new(ordering);
        ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            if (attempt is null || attempt.Data.IsDefaultOrEmpty || attempt.WriteMask.IsDefault || attempt.ByteOffsetRelative < 0 || (attempt.ByteOffsetRelative & 3) != 0 || (attempt.PacketLength & 3) != 0)
            {
                throw new InvalidDataException("Native common packet receipt lacks its original aligned stores and mask.");
            }
            int address = checked(start + attempt.ByteOffsetRelative);
            links.InvalidatePacketRange(new(bank, process.PacketBuffer, address), attempt.PacketLength);
            stores.Add(new(address, attempt.Data, attempt.WriteMask));
            if (attempt.Primitive is not { } primitive)
            {
                continue;
            }
            int end = checked(attempt.ByteOffsetRelative + attempt.PacketLength);
            if (primitive.Words.Length * 4 != attempt.PacketLength || end > batch.AllocationBytes)
            {
                throw new InvalidDataException("Native linked common packet leaves its accepted allocation.");
            }
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(primitive.Bucket, new(bank, process.PacketBuffer, address), primitive.Words, flags20, primitive.VertexPayload4, moduleDraw);
            if (linked)
            {
                stores.Add(new(address, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).ToImmutableArray(), KnownLinkMask));
            }
            acceptedEnd = Math.Max(acceptedEnd, end);
        }
        if (acceptedEnd != batch.AllocationBytes)
        {
            throw new InvalidDataException("Native common packet accepted extent differs from its final physical cursor.");
        }
        return new(process.ApplyCommonWrites(stores.ToImmutable()) with { ByteCursor = checked(start + batch.AllocationBytes) }, links.ToImmutable());
    }
}
