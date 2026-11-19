using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public NativeBattleGraphicsResource ReadNativeLimitPresentationResource(BattleCombatantDefinition actor, BattleLimitFamily family, NativeBattleGraphicsResource equipment)
    {
        int sourceCharacter = BattleLimitFamilyRules.SourceCharacter(family);
        BattleModel sourceBody = LoadPlayer(sourceCharacter);
        BattleModel motion = LoadPlayerForStyle(actor.ModelId, actor.Costume, sourceCharacter);
        int? sourceWeapon = WeaponDefinitions.FirstOrDefault(weapon => weapon.CharacterId == sourceCharacter)?.Id;
        BattleModel sourceSequenceOwner = LoadPlayerSequenceOwner(sourceCharacter, sourceWeapon, sourceBody);
        NativeBattleGraphicsResource source = (sourceSequenceOwner.Sequences ?? throw new InvalidDataException("The selected Limit family has no original body sequence owner.")).NativeResource;
        bool unarmed = family == BattleLimitFamily.Duel;
        ImmutableArray<NativeBattleRawMesh>? geometry = equipment.Geometry;
        if (unarmed && equipment.LinkedGeometryUsesBodySkeleton == true && geometry is { } body)
        {
            int weaponObject = equipment.SharedWeaponBodyObjectIndex ?? 1;
            if ((uint)weaponObject >= body.Length)
            {
                throw new InvalidDataException("The unarmed action's embedded equipment object exceeds its body geometry.");
            }
            geometry = body.SetItem(weaponObject, new([], []) { PackedVertexGroups = [], BoneGroups = [] });
        }
        NativeBattleGraphicsResource presentation = equipment with
        {
            SequenceSection = source.SequenceSection,
            SequenceSectionAddress = source.SequenceSectionAddress,
            AnimationFrames = motion.Animations.Select(animation => checked((byte)animation.Frames.Count)).ToImmutableArray(),
            PoseSource = motion.NativePoseSource,
            HasActionCamera = sourceBody.NativeActionCameraBank is not null,
            ActionCameraBank = sourceBody.NativeActionCameraBank,
            Geometry = geometry,
            LinkedGeometry = unarmed ? [] : equipment.LinkedGeometry,
            LinkedGeometryUsesBodySkeleton = unarmed ? false : equipment.LinkedGeometryUsesBodySkeleton
        };
        return presentation;
    }

    private ImmutableArray<NativeBattleLimitPresentationBinding> ReadNativeLimitPresentations(IReadOnlyList<BattleCombatantDefinition> actors, ImmutableArray<NativeBattleGraphicsBinding> standing)
    {
        ImmutableArray<NativeBattleLimitPresentationBinding>.Builder bindings = ImmutableArray.CreateBuilder<NativeBattleLimitPresentationBinding>();
        foreach (BattleCombatantDefinition actor in actors.Where(actor => actor.IsPlayer && (actor.NativeLimitFamilies is not null || actor.WeaponProfile is { } weapon && weapon.ResourceOwner != actor.ModelId)))
        {
            NativeBattleGraphicsResource equipment = standing.Single(binding => binding.ActorId == actor.Id).Resource;
            foreach (BattleNativeLimitFamilyDefinition family in actor.NativeLimitFamilies ?? BattleLimitFamilyRules.NativeFamilies(actor.ModelId).Select(ReadNativeLimitFamily).ToArray())
            {
                bindings.Add(new(actor.Id, family.Family, ReadNativeLimitPresentationResource(actor, family.Family, equipment)));
            }
        }
        return bindings.ToImmutable();
    }
}
