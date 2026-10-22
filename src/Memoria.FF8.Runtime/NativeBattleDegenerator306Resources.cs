using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDegenerator306Resources(NativeBattleEffectHeapAllocation CasterDat, NativeBattleEffectHeapAllocation Camera, NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleStaticMeshResource MeshC, NativeBattleStaticMeshResource MeshD, NativeBattleStaticMeshResource CustomMesh, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, ImmutableArray<uint> MaterialCycle, uint Sound)
{
    public void Validate()
    {
        if (CasterDat.Identity != "FF8_EN:CE454C" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74 || Camera.Identity != "FF8_EN:CE4164" || Camera.Data.IsDefault || Camera.Data.Length != 1000 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 1000)
        {
            throw new InvalidDataException("Degenerator requires its complete caster binding and camera resources.");
        }
        int[] directory = [0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < directory.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(CasterDat.Data.AsSpan(), index) != directory[index])
            {
                throw new InvalidDataException("Degenerator requires its original three-section caster binding directory.");
            }
        }
        Mesh(MeshA, 0xCEAB94, 920, 26);
        Mesh(MeshB, 0xCEAF2C, 856, 30);
        Mesh(MeshC, 0xCE643C, 5616, 169);
        Mesh(MeshD, 0xCEA2E4, 2224, 81);
        Mesh(CustomMesh, 0xCE7A2C, 10424, 314);
        Sprite(SpriteA, 0xCE60C0, 60, 8, 1);
        Sprite(SpriteB, 0xCE62A8, 404, 0, 15);
        if (MaterialCycle.IsDefault || MaterialCycle.Length != 12 || Sound != 0x55775)
        {
            throw new InvalidDataException("Degenerator requires its complete twelve-step material cycle and original sound.");
        }
    }
    public NativeBattleStaticMeshResource ReadMesh(uint address) => address switch
    {
        0xCEAB94 => MeshA,
        0xCEAF2C => MeshB,
        0xCE643C => MeshC,
        0xCEA2E4 => MeshD,
        0xCE7A2C => CustomMesh,
        _ => throw new NotSupportedException("Degenerator selected an unsupported mesh resource.")
    };
    public NativeBattleModuleResource ReadSprite(uint address) => address switch
    {
        0xCE60C0 => SpriteA,
        0xCE62A8 => SpriteB,
        _ => throw new NotSupportedException("Degenerator selected an unsupported sprite resource.")
    };
    private static void Mesh(NativeBattleStaticMeshResource resource, uint address, int length, int vertices)
    {
        if (resource is null || resource.Address != address || resource.Bytes.IsDefault || resource.Bytes.Length != length || BinaryPrimitives.ReadInt32LittleEndian(resource.Bytes.AsSpan()) != 8 + vertices * 8 || BinaryPrimitives.ReadInt32LittleEndian(resource.Bytes.AsSpan().Slice(4)) != vertices)
        {
            throw new InvalidDataException("Degenerator requires its complete original addressed mesh.");
        }
    }
    private static void Sprite(NativeBattleModuleResource resource, uint address, int length, byte kind, int frames)
    {
        if (resource is null || resource.Address != address || resource.Bytes.IsDefault || resource.Bytes.Length != length || resource.Kind != kind || BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Degenerator requires its complete original projected sprite resource.");
        }
        for (int phase = 0; phase < frames; phase++)
        {
            _ = resource.ProjectedFrame571C80(phase);
        }
    }
}
