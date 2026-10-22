using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleElectricDischarge126Resources(NativeBattleGfAnimationSource Act, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleEffectHeapAllocation Camera, ImmutableArray<byte> DistanceTable, ImmutableArray<short> WaveTable, ImmutableArray<byte> ColorIndices, ImmutableArray<byte> BonePaths, uint ActorColor, uint Sound, uint InitialFlashThreshold)
{
    public void Validate()
    {
        if (Act.Address != 0x12417D8 || Act.Data.IsDefault || Act.Data.Length != 0xA10 || Camera.Identity != "FF8_EN:12421E8" || Camera.Data.IsDefault || Camera.Data.Length != 0x90 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0x90 || DistanceTable.IsDefault || DistanceTable.Length != 16384 || WaveTable.IsDefault || WaveTable.Length != 4096 || ColorIndices.IsDefault || ColorIndices.Length != 6 || BonePaths.IsDefault || BonePaths.Length != 0x780)
        {
            throw new InvalidDataException("Electric Discharge requires its complete original addressed resources.");
        }
        Sprite(SpriteA, 0x12415A8, 0x154);
        Sprite(SpriteB, 0x12416FC, 0xDC);
    }
    public NativeBattleModuleResource ReadSprite(uint address) => address switch
    {
        0x12415A8 => SpriteA,
        0x12416FC => SpriteB,
        _ => throw new NotSupportedException("Electric Discharge selected an unsupported original sprite.")
    };
    public short ReadPathWord(uint address)
    {
        if (address < NativeBattleElectricDischarge126Profile.BonePathsAddress || (address & 1) != 0 || address - NativeBattleElectricDischarge126Profile.BonePathsAddress > BonePaths.Length - 2)
        {
            throw new InvalidDataException("Electric Discharge bone path pointer leaves its original programme.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(BonePaths.AsSpan().Slice((int)(address - NativeBattleElectricDischarge126Profile.BonePathsAddress), 2));
    }
    private static void Sprite(NativeBattleModuleResource sprite, uint address, int length)
    {
        if (sprite.Address != address || sprite.Kind != 0 || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != 8)
        {
            throw new InvalidDataException("Electric Discharge requires its complete original projected sprite.");
        }
        for (int phase = 0; phase < 8; phase++)
        {
            _ = sprite.ProjectedFrame571C80(phase);
        }
    }
}
