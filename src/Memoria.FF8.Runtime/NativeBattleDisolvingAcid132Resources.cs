using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDisolvingAcid132Resources(NativeBattleGfAnimationSource PrimaryAct, NativeBattleGfAnimationSource SecondaryAct, NativeBattleGfAnimationSource ImpactAct, NativeBattleModuleResource TemplateSprite, NativeBattleModuleResource SpriteA, NativeBattleModuleResource SpriteB, NativeBattleModuleResource SpriteC, NativeBattleEffectHeapAllocation Camera, uint Sound, ImmutableArray<byte> EmissionVectors, ImmutableArray<short> Envelope, ImmutableArray<byte> DistanceTable)
{
    public void Validate()
    {
        RequireAct(PrimaryAct, PrimaryActAddress, 0xCA0);
        RequireAct(SecondaryAct, SecondaryActAddress, 0xCA0);
        RequireAct(ImpactAct, ImpactActAddress, 0x578);
        RequireSprite(TemplateSprite, TemplateSpriteAddress, 0xDC, 8);
        RequireSprite(SpriteA, SpriteAAddress, 0xDC, 8);
        RequireSprite(SpriteB, SpriteBAddress, 0xF8, 9);
        RequireSprite(SpriteC, SpriteCAddress, 0x144, 12);
        if (Camera.Identity != "FF8_EN:1214E1C" || Camera.Data.IsDefault || Camera.Data.Length != CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != CameraLength || Sound != 0x6419A)
        {
            throw new InvalidDataException("Disolving Acid requires its complete original camera and sound.");
        }
        if (EmissionVectors.IsDefault || EmissionVectors.Length != 80 || Envelope.IsDefault || Envelope.Length != 25 || DistanceTable.IsDefault || DistanceTable.Length != 0x4000)
        {
            throw new InvalidDataException("Disolving Acid requires its complete original emission rows, envelope and distance table.");
        }
    }
    public NativeBattleGfAnimationSource Act(uint address) => address switch
    {
        PrimaryActAddress => PrimaryAct,
        SecondaryActAddress => SecondaryAct,
        ImpactActAddress => ImpactAct,
        _ => throw new NotSupportedException("Disolving Acid selected an unsupported ACT source.")
    };
    public NativeBattleModuleResource Sprite(uint address) => address switch
    {
        TemplateSpriteAddress => TemplateSprite,
        SpriteAAddress => SpriteA,
        SpriteBAddress => SpriteB,
        SpriteCAddress => SpriteC,
        _ => throw new NotSupportedException("Disolving Acid selected an unsupported sprite source.")
    };
    private static void RequireAct(NativeBattleGfAnimationSource source, uint address, int length)
    {
        if (source.Address != address || source.Data.IsDefault || source.Data.Length != length)
        {
            throw new InvalidDataException("Disolving Acid requires its complete original addressed ACT source.");
        }
    }
    private static void RequireSprite(NativeBattleModuleResource sprite, uint address, int length, int count)
    {
        if (sprite.Address != address || sprite.Bytes.IsDefault || sprite.Bytes.Length != length || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != count)
        {
            throw new InvalidDataException("Disolving Acid requires its complete original sprite source.");
        }
        for (int index = 0; index < count; index++)
        {
            _ = sprite.ProjectedFrame571C80(index);
        }
    }
}
