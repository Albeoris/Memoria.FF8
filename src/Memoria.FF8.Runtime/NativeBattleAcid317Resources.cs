using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAcid317Resources(NativeBattleEffectHeapAllocation CasterDat, NativeBattleGfAnimationSource TargetAct, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleEffectHeapAllocation Camera, ImmutableArray<byte> DistanceTable, ImmutableArray<byte> MouthAnchor8, uint Sound)
{
    public void Validate()
    {
        if (CasterDat.Identity != "FF8_EN:FFE6B0" || CasterDat.Data.IsDefault || CasterDat.Data.Length != 0x1B74 || TargetAct.Address != 0x1000224 || TargetAct.Data.IsDefault || TargetAct.Data.Length != 0x1178 || Camera.Identity != "FF8_EN:10017A0" || Camera.Data.IsDefault || Camera.Data.Length != 0x328 || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0x328 || DistanceTable.IsDefault || DistanceTable.Length != 128 * 128 || MouthAnchor8.IsDefault || MouthAnchor8.Length != 8 || Sound != 0x55784)
        {
            throw new InvalidDataException("Acid requires its complete original addressed resources.");
        }
        int[] directory = [0x14, 0x1648, 0x1698, 0x1B74];
        for (int index = 0; index < directory.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(CasterDat.Data.AsSpan(), index) != directory[index])
            {
                throw new InvalidDataException("Acid requires its complete three-section caster binding.");
            }
        }
        Sprite(SpriteA, 0x100139C, 0x160, 13);
        Sprite(SpriteB, 0x10014FC, 0x1AC, 16);
        Sprite(SpriteC, 0x10016A8, 0xF8, 9);
    }
    public NativeBattleModuleResource ReadSprite(uint address) => address switch
    {
        0x100139C => SpriteA,
        0x10014FC => SpriteB,
        0x10016A8 => SpriteC,
        _ => throw new NotSupportedException("Acid selected an unsupported original sprite.")
    };
    private static void Sprite(NativeBattleModuleResource sprite, uint address, int length, int frames)
    {
        if (sprite.Address != address || sprite.Kind != 0 || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Acid requires its complete original projected sprite.");
        }
        for (int phase = 0; phase < frames; phase++)
        {
            _ = sprite.ProjectedFrame571C80(phase);
        }
    }
}
