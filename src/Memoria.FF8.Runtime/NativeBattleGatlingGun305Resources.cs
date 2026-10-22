using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGatlingGun305Resources(NativeBattleEffectHeapAllocation CasterDat, NativeBattleEffectHeapAllocation Camera, NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleStaticMeshResource MeshC, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, uint Sound)
{
    public void Validate()
    {
        if (CasterDat.Identity != "FF8_EN:CEB5E8" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74 || Camera.Identity != "FF8_EN:CEB2DC" || Camera.Data.IsDefault || Camera.Data.Length != 780 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 780)
        {
            throw new InvalidDataException("Gatling Gun requires its complete caster binding and camera resources.");
        }
        int[] directory = [0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < directory.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(CasterDat.Data.AsSpan(), index) != directory[index])
            {
                throw new InvalidDataException("Gatling Gun requires its original three-section caster binding directory.");
            }
        }
        Mesh(MeshA, 0xCED698, 768, 25);
        Mesh(MeshB, 0xCED998, 712, 24);
        Mesh(MeshC, 0xCEDC60, 712, 24);
        Sprite(SpriteA, 0xCED358, 428, 0, 16);
        Sprite(SpriteB, 0xCED15C, 508, 0, 16);
        Sprite(SpriteC, 0xCED504, 404, 0, 15);
        if (Sound != 0x61AD6)
        {
            throw new InvalidDataException("Gatling Gun requires its original sound descriptor value.");
        }
    }
    public NativeBattleStaticMeshResource ReadMesh(uint address) => address switch
    {
        0xCED698 => MeshA,
        0xCED998 => MeshB,
        0xCEDC60 => MeshC,
        _ => throw new NotSupportedException("Gatling Gun selected an unsupported mesh resource.")
    };
    public NativeBattleModuleResource ReadSprite(uint address) => address switch
    {
        0xCED358 => SpriteA,
        0xCED15C => SpriteB,
        0xCED504 => SpriteC,
        _ => throw new NotSupportedException("Gatling Gun selected an unsupported sprite resource.")
    };
    private static void Mesh(NativeBattleStaticMeshResource resource, uint address, int length, int vertices)
    {
        if (resource is null || resource.Address != address || resource.Bytes.IsDefault || resource.Bytes.Length != length || BinaryPrimitives.ReadInt32LittleEndian(resource.Bytes.AsSpan()) != 8 + vertices * 8 || BinaryPrimitives.ReadInt32LittleEndian(resource.Bytes.AsSpan().Slice(4)) != vertices)
        {
            throw new InvalidDataException("Gatling Gun requires its complete original addressed mesh.");
        }
    }
    private static void Sprite(NativeBattleModuleResource resource, uint address, int length, byte kind, int frames)
    {
        if (resource is null || resource.Address != address || resource.Bytes.IsDefault || resource.Bytes.Length != length || resource.Kind != kind || BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Gatling Gun requires its complete original projected sprite resource.");
        }
        for (int phase = 0; phase < frames; phase++)
        {
            _ = resource.ProjectedFrame571C80(phase);
        }
    }
}
