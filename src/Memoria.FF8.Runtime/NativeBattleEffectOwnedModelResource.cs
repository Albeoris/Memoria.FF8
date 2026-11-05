using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>An effect-owned model source, independent of the battle actor resource banks.</summary>
public sealed record NativeBattleEffectOwnedModelResource(uint Address, ImmutableArray<byte> Bytes, NativeBattlePoseSource PoseSource, ImmutableArray<NativeBattleRawMesh> Geometry)
{
    public int Section(int index) => NativeBattleDatDirectory.Section(Bytes.AsSpan(), index);
    internal NativeBattleResourcePointer GeometryPointer() => new(null, checked(Address + (uint)Section(1)), Section(2) - Section(1));
}

public sealed record NativeBattleSearchModelResources(NativeBattleEffectOwnedModelResource Model, NativeBattleEffectHeapAllocation ModelTim, uint DefaultColor44);
