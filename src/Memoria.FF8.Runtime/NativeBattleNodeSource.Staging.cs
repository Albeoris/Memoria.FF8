using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleNodeSource
{
    private readonly NativeBattleFileStaging? staging;
    private readonly uint stagingAddress;
    private readonly int stagingLength;
    private readonly ImmutableArray<NativeBattleEffectUnknownRegion> stagingUnknown;
    private readonly ImmutableDictionary<int, NativeBattleResourcePointer?>? stagingPointers;

    private NativeBattleNodeSource(NativeBattleFileStaging staging, uint address, int length, bool scoped)
    {
        NativeBattleFileStaging.RequirePhysicalRange(address, length);
        if (length <= 0 || length > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Native node source requires its bounded staging window.");
        }
        this.staging = scoped ? staging : staging.BorrowOwner();
        stagingAddress = address;
        stagingLength = length;
        storage = null!;
    }

    private NativeBattleNodeSource(NativeBattleFileStaging staging, uint address, ImmutableArray<byte> data, ImmutableArray<NativeBattleEffectUnknownRegion> unknown, ImmutableDictionary<int, NativeBattleResourcePointer?>? pointers)
    {
        this.staging = staging;
        stagingAddress = address;
        stagingLength = data.Length;
        payload = data;
        stagingUnknown = unknown;
        stagingPointers = pointers;
        snapshot = true;
        storage = null!;
    }

    internal bool IsStaging => staging is not null;
    internal NativeBattleFileStaging StagingAuthority => staging ?? throw new InvalidOperationException("Native node source is not staging-backed.");
    internal uint StagingAddress => stagingAddress;

    internal static NativeBattleNodeSource Borrow(NativeBattleFileStaging staging, uint address, int length) => new(staging, address, length, false);

    internal static NativeBattleNodeSource BorrowPrivateView(NativeBattleFileStaging staging, uint address, int length)
    {
        if (staging.Transaction is not { } transaction || transaction.ActiveOverlay.IsPublicLayer)
        {
            throw new InvalidOperationException("Scoped native node source requires its explicit private staging view.");
        }
        return new(staging, address, length, true);
    }

    internal NativeBattleNodeSource RebindStaging(NativeBattleFileStaging authority, uint address, int length)
    {
        if (length != Length)
        {
            throw new InvalidDataException("Native node rebind changes its retained physical extent.");
        }
        return Borrow(authority, address, length);
    }

    private uint RequireStagedRange(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > stagingLength - length)
        {
            throw new InvalidDataException("Native node read leaves its bounded staging source.");
        }
        return checked(stagingAddress + (uint)offset);
    }

    private void ReadStagedWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        uint address = RequireStagedRange(offset, destination.Length);
        if (destination.Length != known.Length)
        {
            throw new ArgumentException("Native node data and knownness destinations must match.", nameof(known));
        }
        if (snapshot)
        {
            staging!.ReadWithKnownness(address, destination, known);
        }
        else
        {
            staging!.ReadEffectiveWithKnownness(address, destination, known);
        }
    }

    private void ReadStagedScalar(int offset, Span<byte> destination)
    {
        Span<bool> known = stackalloc bool[4];
        ReadStagedWithKnownness(offset, destination, known[..destination.Length]);
        if (known[..destination.Length].Contains(false))
        {
            throw new NotSupportedException("Native node read intersects unresolved staging bytes.");
        }
    }

    private byte ReadStagedByte(int offset)
    {
        Span<byte> data = stackalloc byte[1];
        ReadStagedScalar(offset, data);
        return data[0];
    }

    private ushort ReadStagedUInt16(int offset)
    {
        Span<byte> data = stackalloc byte[2];
        ReadStagedScalar(offset, data);
        return BinaryPrimitives.ReadUInt16LittleEndian(data);
    }

    private uint ReadStagedUInt32(int offset)
    {
        Span<byte> data = stackalloc byte[4];
        ReadStagedScalar(offset, data);
        return BinaryPrimitives.ReadUInt32LittleEndian(data);
    }

    private void ReadStagedInto(int offset, Span<byte> destination)
    {
        RequireStagedRange(offset, destination.Length);
        Span<byte> data = stackalloc byte[256];
        Span<bool> known = stackalloc bool[256];
        for (int copied = 0; copied < destination.Length;)
        {
            int count = Math.Min(data.Length, destination.Length - copied);
            ReadStagedWithKnownness(offset + copied, data[..count], known[..count]);
            if (known[..count].Contains(false))
            {
                throw new NotSupportedException("Native node read intersects unresolved staging bytes.");
            }
            copied += count;
        }
        for (int copied = 0; copied < destination.Length;)
        {
            int count = Math.Min(data.Length, destination.Length - copied);
            ReadStagedWithKnownness(offset + copied, destination.Slice(copied, count), known[..count]);
            copied += count;
        }
    }

    internal IEnumerable<KeyValuePair<int, NativeBattleResourcePointer?>> EnumerateResourcePointers()
    {
        if (staging is null)
        {
            foreach (var entry in snapshot ? storage.ResourcePointers ?? ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty : storage.EnumerateResourcePointers())
            {
                yield return entry;
            }
            yield break;
        }
        ulong end = (ulong)stagingAddress + (uint)stagingLength;
        foreach (var entry in staging.EnumerateEffectivePointers())
        {
            if (entry.Key >= end || (ulong)entry.Key + 4 <= stagingAddress)
            {
                continue;
            }
            if (entry.Key < stagingAddress || (ulong)entry.Key + 4 > end)
            {
                throw new InvalidDataException("Native node provenance partially overlaps its staging boundary.");
            }
            yield return new(checked((int)(entry.Key - stagingAddress)), entry.Value);
        }
    }

    private NativeBattleNodeSource CaptureStaging(NativeBattleStorageCaptureContext context)
    {
        NativeBattleFileStaging frozen = context.Map(staging!);
        (byte[] data, bool[] known) = frozen.ReadSnapshot(stagingAddress, stagingLength);
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknown = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
        for (int index = 0; index < known.Length;)
        {
            if (known[index])
            {
                index++;
                continue;
            }
            int first = index++;
            while (index < known.Length && !known[index])
            {
                index++;
            }
            unknown.Add(new(first, index - first));
        }
        ImmutableDictionary<int, NativeBattleResourcePointer?>.Builder pointers = ImmutableDictionary.CreateBuilder<int, NativeBattleResourcePointer?>();
        foreach (var entry in EnumerateResourcePointers())
        {
            pointers.Add(entry.Key, entry.Value);
        }
        return new(frozen, stagingAddress, ImmutableCollectionsMarshal.AsImmutableArray(data), unknown.ToImmutable(), pointers.Count == 0 ? null : pointers.ToImmutable());
    }
}
