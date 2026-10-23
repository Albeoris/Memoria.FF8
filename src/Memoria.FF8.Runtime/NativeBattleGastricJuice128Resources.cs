using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGastricJuice128Resources(NativeBattleGfAnimationSource MainAct, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<byte> DistanceTable)
{
    public void Validate()
    {
        RequireAct(MainAct, MainActAddress, 0x1178);
        RequireSprite(SpriteA, SpriteAAddress, 0xF8, 9);
        RequireSprite(SpriteB, SpriteBAddress, 0x160, 13);
        RequireSprite(SpriteC, SpriteCAddress, 0x1AC, 16);
        if (Camera.Identity != "FF8_EN:1233630" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x641DD)
        {
            throw new InvalidDataException("Gastric Juice requires its complete original camera and sound.");
        }
        if (DistanceTable.IsDefault || DistanceTable.Length != 0x4000)
        {
            throw new InvalidDataException("Gastric Juice requires its full original distance table.");
        }
    }
    public NativeBattleGfAnimationSource Act(uint address) => address switch
    {
        MainActAddress => MainAct,
        _ => throw new NotSupportedException("Gastric Juice selected an unsupported ACT source.")
    };
    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        SpriteCAddress => SpriteC,
        _ => throw new NotSupportedException("Gastric Juice selected an unsupported sprite source.")
    };
    private static void RequireAct(NativeBattleGfAnimationSource source, uint address, int length)
    {
        if (source.Address != address || source.Data.IsDefault || source.Data.Length != length)
        {
            throw new InvalidDataException("Gastric Juice requires its complete original addressed ACT source.");
        }
    }
    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Gastric Juice requires its complete original sprite source.");
        }
        for (int index = 0; index < count; index++)
        {
            _ = sprite.ProjectedFrame571C80(index);
        }
    }
}
