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
    private readonly List<(int First, int End)> observedCpuRanges = [];
    private const uint PartyCpuAddress = 0x196B018;

    internal NativeFieldRegistrationMemory(NativeBattleFileStaging source, uint first, uint end)
    {
        this.source = source.Rebase(Math.Min(first, source.BaseAddress));
        origin = this.source.BaseAddress;
        ulong last = Math.Max(end, this.source.KnownRanges.IsEmpty ? origin : (ulong)origin + (uint)this.source.KnownRanges[^1].Offset + (uint)this.source.KnownRanges[^1].Data.Length);
        int length = checked((int)(last - origin));
        (byte[] data, bool[] observed) = this.source.ReadSnapshot(origin, length);
        bytes = data;
        known = observed;
        changed = new bool[length];
    }

    internal void ObserveCpuBanks(NativeBattleModelCpuResources? cpu)
    {
        if (cpu is null)
        {
            return;
        }
        cpu.Validate();
        for (int bank = 0; bank < 3; bank++)
        {
            int first = Index(checked(PartyCpuAddress + (uint)bank * NativeBattleModelCpuBank.Length), NativeBattleModelCpuBank.Length);
            observedCpuRanges.Add((first, checked(first + NativeBattleModelCpuBank.Length)));
            Array.Fill(known, false, first, NativeBattleModelCpuBank.Length);
            foreach (NativeBattleEffectMemoryRegion range in cpu.Cpu.Banks[bank].KnownRanges)
            {
                range.Data.AsSpan().CopyTo(bytes.AsSpan(first + range.Offset));
                Array.Fill(known, true, first + range.Offset, range.Data.Length);
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
            if (!changed.AsSpan(first, NativeBattleModelCpuBank.Length).Contains(true))
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
            for (int offset = 0; offset < data.Length; offset++)
            {
                if (changed[first + offset])
                {
                    observed[offset] = known[first + offset];
                    data[offset] = bytes[first + offset];
                }
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
        List<(int First, int End)> publish = [.. observedCpuRanges];
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
            publish.Add((first, index));
        }
        publish.Sort((left, right) => left.First.CompareTo(right.First));
        NativeBattleFileStaging result = source;
        for (int range = 0; range < publish.Count; range++)
        {
            int index = publish[range].First;
            int end = publish[range].End;
            while (range + 1 < publish.Count && publish[range + 1].First <= end)
            {
                range++;
                end = Math.Max(end, publish[range].End);
            }
            while (index < end)
            {
                int first = index++;
                bool observed = known[first];
                while (index < end && known[index] == observed)
                {
                    index++;
                }
                uint address = checked(origin + (uint)first);
                int length = index - first;
                result = observed ? result.Write(address, bytes.AsSpan(first, length)) : result.MarkUnknown(address, length);
            }
        }
        foreach ((uint address, NativeBattleResourcePointer? pointer) in source.ResourcePointers)
        {
            long offset = (long)address - origin;
            int first = checked((int)Math.Clamp(offset, 0, changed.Length));
            int end = checked((int)Math.Clamp(offset + 4, 0, changed.Length));
            if (!changed.AsSpan(first, end - first).Contains(true))
            {
                result = result with { ResourcePointers = result.ResourcePointers.SetItem(address, pointer) };
            }
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
