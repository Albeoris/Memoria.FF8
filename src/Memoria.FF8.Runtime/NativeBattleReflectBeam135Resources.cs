using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleReflectBeam135Resources(NativeBattleGfAnimationSource MainAct, NativeBattleGfAnimationSource TargetAct, NativeBattleGfAnimationSource SecondaryAct, NativeBattleGfAnimationSource ImpactAct, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleStaticMeshResource StaticMesh, NativeBattleEffectHeapAllocation Camera, ImmutableArray<short> BoneSelectors, uint Sound)
{
    public void Validate()
    {
        CheckAct(MainAct, MainActAddress, 0x2878, 5, 0x2010, 8, 20);
        CheckAct(TargetAct, TargetActAddress, 0x1328, 2, 0x81C, 10, 24);
        CheckAct(SecondaryAct, SecondaryActAddress, 0x1B5C, 4, 0x15A0, 6, 13);
        CheckAct(ImpactAct, ImpactActAddress, 0x744, 1, 0x600, 2, 11);
        CheckSprite(SpriteA, SpriteAAddress, 0xC4, 7);
        CheckSprite(SpriteB, SpriteBAddress, 0xF4, 9);
        CheckSprite(SpriteC, SpriteCAddress, 0x1C0, 16);
        if (StaticMesh.Address != StaticMeshAddress || StaticMesh.Bytes.IsDefault || StaticMesh.Bytes.Length != 0x19C8 || Integer(StaticMesh.Bytes, 0) != 1768 || Integer(StaticMesh.Bytes, 4) != 220)
        {
            throw new InvalidDataException("Reflect Beam requires its complete original 220-vertex static mesh.");
        }
        if (Camera.Identity != "FF8_EN:1201D8C" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x641A0 || BoneSelectors.IsDefault || !BoneSelectors.SequenceEqual(new short[] { 29, 30, 32 }))
        {
            throw new InvalidDataException("Reflect Beam requires its original camera, sound and three bone selectors.");
        }
    }
    public NativeBattleGfAnimationSource Act(uint address) => address switch
    {
        MainActAddress => MainAct,
        TargetActAddress => TargetAct,
        SecondaryActAddress => SecondaryAct,
        ImpactActAddress => ImpactAct,
        _ => throw new NotSupportedException("Reflect Beam selected an unsupported ACT source.")
    };
    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        SpriteCAddress => SpriteC,
        _ => throw new NotSupportedException("Reflect Beam selected an unsupported sprite source.")
    };
    private static void CheckAct(NativeBattleGfAnimationSource source, uint address, int length, int shapes, int animation, int nodes, int duration)
    {
        if (source.Address != address || source.Data.IsDefault || source.Data.Length != length || Integer(source.Data, 0) != shapes || Integer(source.Data, 4) != animation || Integer(source.Data, animation) != nodes || Integer(source.Data, animation + 4) != duration)
        {
            throw new InvalidDataException("Reflect Beam requires its complete original ACT directory and programme.");
        }
    }
    private static void CheckSprite(NativeBattleModuleResource sprite, uint address, int length, int frames)
    {
        if (sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != frames)
        {
            throw new InvalidDataException("Reflect Beam requires its complete original sprite frames.");
        }
        for (int phase = 0; phase < frames; phase++)
        {
            _ = sprite.ProjectedFrame571C80(phase);
        }
    }
    private static int Integer(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
}
