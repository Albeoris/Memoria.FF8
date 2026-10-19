namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278Resources(NativeBattleStaticMeshResource MirrorBoundsMesh)
{
    public void Validate()
    {
        if (MirrorBoundsMesh is null || MirrorBoundsMesh.Address != 0x10C0D08 || MirrorBoundsMesh.Bytes.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native Carbuncle requires its original mirror bounds model.");
        }
        _ = NativeBattleStaticMeshBounds.Read6D6070(MirrorBoundsMesh.Bytes.AsSpan());
    }
}
