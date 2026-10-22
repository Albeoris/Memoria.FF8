using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleElectrocute275Resources(NativeBattleEffectHeapAllocation CasterDat, NativeBattleGfAnimationSource Act, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleEffectHeapAllocation Camera, ImmutableArray<byte> DistanceTable, ImmutableArray<short> WaveTable, ImmutableArray<byte> ColorIndices, ImmutableArray<byte> BonePaths, uint ActorColor, uint Sound, uint InitialFlashThreshold)
{
    public void Validate()
    {
        if (CasterDat.Identity != "FF8_EN:10DA574" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74 || Act.Address != 0x10D9B64 || Act.Data.IsDefault || Act.Data.Length != 0xA10 || Camera.Identity != "FF8_EN:10DC0E8" || Camera.Data.IsDefault || Camera.Data.Length != 0x1E8 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0x1E8 || DistanceTable.IsDefault || DistanceTable.Length != 16384 || WaveTable.IsDefault || WaveTable.Length != 4096 || ColorIndices.IsDefault || ColorIndices.Length != 6 || BonePaths.IsDefault || BonePaths.Length != 0x6A8)
        {
            throw new InvalidDataException("Electrocute requires its complete original addressed resources.");
        }
        int[] directory = [0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < directory.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(CasterDat.Data.AsSpan(), index) != directory[index])
            {
                throw new InvalidDataException("Electrocute requires its original three-section caster binding.");
            }
        }
        Sprite(SpriteA, 0x10D9934, 0x154);
        Sprite(SpriteB, 0x10D9A88, 0xDC);
    }
    public NativeBattleModuleResource ReadSprite(uint address) => address switch
    {
        0x10D9934 => SpriteA,
        0x10D9A88 => SpriteB,
        _ => throw new NotSupportedException("Electrocute selected an unsupported original sprite.")
    };
    public short ReadPathWord(uint address)
    {
        if (address < NativeBattleElectrocute275Profile.BonePathsAddress || (address & 1) != 0 || address - NativeBattleElectrocute275Profile.BonePathsAddress > BonePaths.Length - 2)
        {
            throw new InvalidDataException("Electrocute bone path pointer leaves its original programme.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(BonePaths.AsSpan().Slice((int)(address - NativeBattleElectrocute275Profile.BonePathsAddress), 2));
    }
    private static void Sprite(NativeBattleModuleResource sprite, uint address, int length)
    {
        if (sprite.Address != address || sprite.Kind != 0 || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != 8)
        {
            throw new InvalidDataException("Electrocute requires its complete original projected sprite.");
        }
        for (int phase = 0; phase < 8; phase++)
        {
            _ = sprite.ProjectedFrame571C80(phase);
        }
    }
}
