using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleControl18ResourceFace(NativeBattleResourcePointer Source, bool Quad, ImmutableArray<NativeBattleVector> Vertices);

/// <summary>Call-scoped polygon receipts derived from the live arena records; no separate persisted owner exists.</summary>
internal static class NativeBattleControl18ResourceRecords
{
    internal static ImmutableDictionary<int, NativeBattleResourcePointer> Require(ImmutableDictionary<int, NativeBattleResourcePointer>? pointers, int length)
    {
        ImmutableDictionary<int, NativeBattleResourcePointer> selected = pointers ?? ImmutableDictionary<int, NativeBattleResourcePointer>.Empty;
        if (selected.Any(entry => entry.Key < 0 || entry.Key % 68 != 0 || entry.Key > length - 68 || entry.Value is null || !entry.Value.HasValidOwner() || entry.Value.CpuRow is not null || entry.Value.Cpu is { IsOwnedResource: true } && entry.Value.CpuPublication is null))
        {
            throw new InvalidDataException("Native breakup polygon receipts require their exact live record slots and source owners.");
        }
        return selected;
    }

    internal static bool Active(ReadOnlySpan<byte> bytes, int offset, ImmutableDictionary<int, NativeBattleResourcePointer> pointers)
    {
        return pointers.ContainsKey(offset) || BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4)) != 0;
    }
}
