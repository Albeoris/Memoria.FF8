using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMasamuneResources(NativeBattleStaticMeshResource Flare, NativeBattleStaticMeshResource BurstA, NativeBattleStaticMeshResource BurstB, NativeBattleModuleResource RotatingSprite, NativeBattleModuleResource RingSprite)
{
    public void Validate()
    {
        Mesh(Flare, 0xCCF2DC, 2008);
        Mesh(BurstA, 0xCC72FC, 1520);
        Mesh(BurstB, 0xCC877C, 1768);
        Sprite(RotatingSprite, 0xCC7120, 6, 476);
        Sprite(RingSprite, 0xCC6DC8, 7, 428);
    }

    private static void Mesh(NativeBattleStaticMeshResource resource, uint address, int length)
    {
        if (resource is null || resource.Address != address || resource.Bytes.IsDefault || resource.Bytes.Length != length)
        {
            throw new InvalidDataException("Masamune requires its complete addressed mesh resource.");
        }
    }

    private static void Sprite(NativeBattleModuleResource resource, uint address, byte kind, int length)
    {
        if (resource is null || resource.Address != address || resource.Kind != kind || resource.Bytes.IsDefault || resource.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(8)) != 16)
        {
            throw new InvalidDataException("Masamune requires its complete addressed sprite resource.");
        }
        for (int phase = 0; phase < 16; phase++)
        {
            _ = resource.ProjectedFrame571C80(phase);
        }
    }
}
