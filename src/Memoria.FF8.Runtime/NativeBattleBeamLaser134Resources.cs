using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBeamLaser134Resources(NativeBattleEffectHeapAllocation Camera, ImmutableArray<NativeBattleGfAnimationSource> Acts, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleStaticMeshResource Mesh, uint Sound)
{
    public void Validate()
    {
        if (Camera.Identity != "FF8_EN:1207680" || Camera.Data.IsDefault || Camera.Data.Length != 0x128 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0x128 || Sound != 0x61B82 || Acts.IsDefault || Acts.Length != 5 || Mesh.Address != 0x1202214 || Mesh.Bytes.IsDefault || Mesh.Bytes.Length != 0x16C8)
        {
            throw new InvalidDataException("Beam Laser requires its complete original camera, sound, mesh and ACT resources.");
        }
        uint[] addresses = [0x12038DC, 0x1204004, 0x1205270, 0x12065F0, 0x1207150];
        int[] lengths = [0x728, 0x126C, 0x1380, 0xB60, 0x52C];
        for (int index = 0; index < Acts.Length; index++)
        {
            if (Acts[index].Address != addresses[index] || Acts[index].Data.IsDefault || Acts[index].Data.Length != lengths[index])
            {
                throw new InvalidDataException("Beam Laser requires all five original addressed ACT sources.");
            }
        }
        RequireSprite(SpriteA, 0x1201EBC);
        RequireSprite(SpriteB, 0x1202068);
    }
    public NativeBattleGfAnimationSource Act(uint address)
    {
        Validate();
        return Acts.SingleOrDefault(source => source.Address == address) ?? throw new NotSupportedException("Beam Laser selected an unsupported ACT source.");
    }
    private static void RequireSprite(NativeBattleModuleResource source, uint address)
    {
        if (source.Address != address || source.Bytes.IsDefault || source.Bytes.Length != 0x1AC || BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(8)) != 16)
        {
            throw new InvalidDataException("Beam Laser requires its complete original projected sprites.");
        }
        for (int frame = 0; frame < 16; frame++)
        {
            _ = source.ProjectedFrame571C80(frame);
        }
    }
}
