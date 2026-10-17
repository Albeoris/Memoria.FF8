using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A single registration transaction over the shared sparse memory; copies preserve unknown bytes.</summary>
internal sealed class NativeFieldRegistrationMemory
{
    private readonly NativeBattleFileStaging source;
    private readonly uint origin;
    private readonly byte[] bytes;
    private readonly bool[] known;
    private readonly bool[] changed;

    internal NativeFieldRegistrationMemory(NativeBattleFileStaging source, uint first, uint end)
    {
        this.source = source.Rebase(Math.Min(first, source.BaseAddress));
        origin = this.source.BaseAddress;
        ulong last = Math.Max(end, this.source.KnownRanges.IsEmpty ? origin : (ulong)origin + (uint)this.source.KnownRanges[^1].Offset + (uint)this.source.KnownRanges[^1].Data.Length);
        int length = checked((int)(last - origin));
        bytes = new byte[length];
        known = new bool[length];
        changed = new bool[length];
        foreach (NativeBattleEffectMemoryRegion range in this.source.KnownRanges)
        {
            range.Data.AsSpan().CopyTo(bytes.AsSpan(range.Offset));
            Array.Fill(known, true, range.Offset, range.Data.Length);
        }
    }

    internal byte Read8(uint address)
    {
        RequireKnown(address, 1);
        return bytes[Index(address, 1)];
    }

    internal ushort Read16(uint address)
    {
        RequireKnown(address, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(Index(address, 2), 2));
    }

    internal short ReadSigned16(uint address) => unchecked((short)Read16(address));

    internal uint Read32(uint address)
    {
        RequireKnown(address, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(Index(address, 4), 4));
    }

    internal ushort? ReadOptional16(uint address)
    {
        int index = Index(address, 2);
        return known[index] && known[index + 1] ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index, 2)) : null;
    }

    internal byte[] Read(uint address, int length)
    {
        RequireKnown(address, length);
        return bytes.AsSpan(Index(address, length), length).ToArray();
    }

    internal void Write(uint address, ReadOnlySpan<byte> data)
    {
        int index = Index(address, data.Length);
        data.CopyTo(bytes.AsSpan(index, data.Length));
        Array.Fill(known, true, index, data.Length);
        Array.Fill(changed, true, index, data.Length);
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
        int index = Index(address, length);
        Array.Fill(bytes, value, index, length);
        Array.Fill(known, true, index, length);
        Array.Fill(changed, true, index, length);
    }

    internal void Forget(uint address, int length)
    {
        int index = Index(address, length);
        Array.Fill(known, false, index, length);
        Array.Fill(changed, true, index, length);
    }

    internal void Copy(uint sourceAddress, uint destination, int length)
    {
        int from = Index(sourceAddress, length);
        int to = Index(destination, length);
        bytes.AsSpan(from, length).CopyTo(bytes.AsSpan(to, length));
        known.AsSpan(from, length).CopyTo(known.AsSpan(to, length));
        Array.Fill(changed, true, to, length);
    }

    internal NativeBattleFileStaging Finish()
    {
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder ranges = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        for (int index = 0; index < known.Length;)
        {
            if (!known[index])
            {
                index++;
                continue;
            }
            int first = index++;
            while (index < known.Length && known[index])
            {
                index++;
            }
            ranges.Add(new(first, bytes.AsSpan(first, index - first).ToArray().ToImmutableArray()));
        }
        NativeBattleFileStaging result = source with { KnownRanges = ranges.ToImmutable() };
        for (int index = 0; index < changed.Length;)
        {
            if (!changed[index])
            {
                index++;
                continue;
            }
            int first = index++;
            while (index < changed.Length && changed[index])
            {
                index++;
            }
            result = result.InvalidateResourcePointers(checked(origin + (uint)first), index - first);
        }
        return result;
    }

    private int Index(uint address, int length)
    {
        if (address < origin || length < 0 || (ulong)address - origin + (uint)length > (uint)bytes.Length)
        {
            throw new InvalidDataException("Native field registration left its shared address domain.");
        }
        return checked((int)(address - origin));
    }

    private void RequireKnown(uint address, int length)
    {
        int index = Index(address, length);
        if (known.AsSpan(index, length).Contains(false))
        {
            throw new NotSupportedException($"Native field registration requires unresolved physical bytes at0x{address:X}.");
        }
    }
}
