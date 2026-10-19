using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278Resources(NativeBattleStaticMeshResource MirrorBoundsMesh)
{
    public NativeBattleEffectOwnedModelResource? CreatureModel { get; init; }
    public NativeVibrationBank? Vibration { get; init; }
    public NativeBattleStaticMeshResource? CreatureBoundsMesh { get; init; }
    public ImmutableArray<uint> Sounds { get; init; } = [];
    public ImmutableArray<byte> MirrorAlternateVertices { get; init; } = [];
    public NativeBattleStaticMeshResource? SecondaryMesh { get; init; }

    public void Validate()
    {
        if (MirrorBoundsMesh is null || MirrorBoundsMesh.Address != 0x10C0D08 || MirrorBoundsMesh.Bytes.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native Carbuncle requires its original mirror bounds model.");
        }
        _ = NativeBattleStaticMeshBounds.Read6D6070(MirrorBoundsMesh.Bytes.AsSpan());
    }
}
