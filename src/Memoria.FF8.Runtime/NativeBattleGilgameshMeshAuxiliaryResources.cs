using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshMeshAuxiliaryRow(short Delay, short Y, short ScaleSource, short YScaleBase);
public sealed record NativeBattleGilgameshMeshAuxiliaryResources(NativeBattleStaticMeshResource RisingMesh, NativeBattleStaticMeshResource DelayedMesh, ImmutableArray<NativeBattleGilgameshMeshAuxiliaryRow> Rows)
{
    public void Validate()
    {
        ValidateMesh(RisingMesh, 0x1A8, 18, 10);
        ValidateMesh(DelayedMesh, 0xCE8, 120, 96);
        if (Rows.IsDefault || Rows.Length != 3 || Rows.Any(row => row is null))
        {
            throw new InvalidDataException("Native Gilgamesh delayed mesh initializer requires its three original four-WORD rows.");
        }
    }

    private static void ValidateMesh(NativeBattleStaticMeshResource mesh, int extent, int vertices, int faces)
    {
        if (mesh is null || mesh.Bytes.IsDefault || mesh.Bytes.Length != extent || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()) != 8 + vertices * 8 || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != vertices)
        {
            throw new InvalidDataException("Native Gilgamesh auxiliary mesh requires its complete original vertex directory.");
        }
        for (int group = 0; group < 8; group++)
        {
            if (NativeBattleIceMeshVertices.Group(mesh.Bytes.AsSpan(), group).Count != (group == 3 ? faces : 0))
            {
                throw new InvalidDataException("Native Gilgamesh auxiliary mesh requires its original GT4 group.");
            }
        }
    }
}
