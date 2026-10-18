using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshFlareResources(NativeBattleStaticMeshResource SourceA, NativeBattleStaticMeshResource SourceB)
{
    public void Validate()
    {
        ValidateMesh(SourceA);
        ValidateMesh(SourceB);
    }

    private static void ValidateMesh(NativeBattleStaticMeshResource mesh)
    {
        if (mesh is null || mesh.Bytes.IsDefault || mesh.Bytes.Length != 0x680 || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != 61)
        {
            throw new InvalidDataException("Native Gilgamesh flare requires its complete original61-vertex mesh.");
        }
        for (int group = 0; group < 8; group++)
        {
            int expected = group == 4 ? 4 : group == 5 ? 44 : 0;
            if (NativeBattleIceMeshVertices.Group(mesh.Bytes.AsSpan(), group).Count != expected)
            {
                throw new InvalidDataException("Native Gilgamesh flare requires its original Gouraud group directory.");
            }
        }
    }
}
