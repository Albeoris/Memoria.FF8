using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshOpeningResources(NativeBattleStaticMeshResource BaseMesh, NativeBattleStaticMeshResource RingA, NativeBattleStaticMeshResource RingB, NativeBattleStaticMeshResource SpireA, NativeBattleStaticMeshResource SpireB)
{
    public void Validate()
    {
        ValidateMesh(BaseMesh, 0x11F0, 145, [0, 0, 0, 0, 16, 128, 0, 0]);
        ValidateMesh(RingA, 0xB08, 96, [0, 0, 0, 48, 0, 0, 0, 24]);
        ValidateMesh(RingB, 0x6E8, 72, [0, 0, 0, 48, 0, 0, 0, 0]);
        ValidateMesh(SpireA, 0xBF0, 97, [0, 0, 16, 80, 0, 0, 0, 0]);
        ValidateMesh(SpireB, 0xBF0, 97, [0, 0, 16, 80, 0, 0, 0, 0]);
    }

    private static void ValidateMesh(NativeBattleStaticMeshResource mesh, int extent, int vertices, int[] groups)
    {
        if (mesh is null || mesh.Bytes.IsDefault || mesh.Bytes.Length != extent || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()) != 8 + vertices * 8 || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != vertices)
        {
            throw new InvalidDataException("Native Gilgamesh opening requires its complete original mesh directory.");
        }
        for (int group = 0; group < 8; group++)
        {
            if (NativeBattleIceMeshVertices.Group(mesh.Bytes.AsSpan(), group).Count != groups[group])
            {
                throw new InvalidDataException("Native Gilgamesh opening mesh lost its original primitive groups.");
            }
        }
    }
}
