using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One process owner for the shared scripted-effect core, independent of the selected effect's task state.</summary>
public sealed record NativeBattleScriptedEffectMemory([property: JsonRequired] NativeBattleEffectArena Core)
{
    public const uint Address = 0x2797068;
    public const int Length = 0x1BB0;

    public static NativeBattleScriptedEffectMemory FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleScriptedEffectMemory Unknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length));

    public void Validate()
    {
        Core.Validate();
        Core.ValidateExtent(Length);
    }

    public byte[] Read(uint address, int length)
    {
        Validate();
        return Core.Read(Offset(address, length), length);
    }

    public (byte[] Bytes, bool[] Known) ReadSnapshot(uint address, int length)
    {
        Validate();
        return Snapshot(Offset(address, length), length);
    }

    public NativeBattleScriptedEffectMemory Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Validate();
        Core.Write(Offset(address, bytes.Length), bytes);
        return this;
    }

    public NativeBattleScriptedEffectMemory MarkUnknown(uint address, int length)
    {
        Validate();
        Core.MarkUnknown(Offset(address, length), length);
        return this;
    }

    public NativeBattleWaterHeader ReadHeader()
    {
        Validate();
        (byte[] bytes, bool[] known) = Snapshot(0, NativeBattleWaterHeader.Length);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public NativeBattleScriptedEffectMemory Copy(uint sourceAddress, uint destinationAddress, int length)
    {
        Validate();
        int source = Offset(sourceAddress, length);
        int destination = Offset(destinationAddress, length);
        if (source == destination)
        {
            return this;
        }
        if (source < destination + length && destination < source + length)
        {
            throw new NotSupportedException("Native scripted-effect core copies require disjoint source and destination fields.");
        }
        (byte[] bytes, bool[] known) = Snapshot(source, length);
        return WriteSnapshot(destinationAddress, bytes, known);
    }

    private (byte[] Bytes, bool[] Known) Snapshot(int offset, int length) => Core.ReadSnapshot(offset, length);

    public NativeBattleScriptedEffectMemory WithHeader(NativeBattleWaterHeader header)
    {
        header.Validate();
        return WriteSnapshot(Address, header.Bytes.AsSpan(), header.Known.AsSpan());
    }

    private NativeBattleScriptedEffectMemory WriteSnapshot(uint address, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> known)
    {
        NativeBattleScriptedEffectMemory result = Write(address, bytes);
        int index = 0;
        while (index < bytes.Length)
        {
            if (known[index])
            {
                index++;
                continue;
            }
            int first = index;
            while (index < bytes.Length && !known[index])
            {
                index++;
            }
            result = result.MarkUnknown(address + (uint)first, index - first);
        }
        return result;
    }

    private static int Offset(uint address, int length)
    {
        long offset = (long)address - Address;
        if (offset < 0 || length < 0 || offset > Length - length)
        {
            throw new InvalidDataException("Native scripted-effect access leaves its shared physical window.");
        }
        return (int)offset;
    }
}
