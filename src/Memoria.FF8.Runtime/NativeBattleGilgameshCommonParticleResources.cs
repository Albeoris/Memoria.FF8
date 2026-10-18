using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshCommonParticleResources(NativeBattleModuleResource Sprite, NativeBattleStaticMeshResource NestedMesh)
{
    public void Validate()
    {
        if (Sprite is null || Sprite.Bytes.IsDefault || Sprite.Bytes.Length != 0x1AC || Sprite.Kind != 7 || BinaryPrimitives.ReadUInt16LittleEndian(Sprite.Bytes.AsSpan().Slice(8)) != 16 || NestedMesh is null || NestedMesh.Bytes.IsDefault || NestedMesh.Bytes.Length != 0x12E8)
        {
            throw new InvalidDataException("Native Gilgamesh common particles require their complete selected sprite and nested mesh.");
        }
        for (int phase = 0; phase < 16; phase++)
        {
            _ = Sprite.ProjectedFrame571C80(phase);
        }
    }
}
