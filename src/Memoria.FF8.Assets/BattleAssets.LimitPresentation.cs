using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public NativeBattleGraphicsResource ReadNativeLimitPresentationResource(BattleCombatantDefinition actor, BattleLimitFamily family, NativeBattleGraphicsResource equipment)
    {
        int sourceCharacter = BattleLimitFamilyRules.SourceCharacter(family);
        BattleModel sourceBody = LoadPlayer(sourceCharacter, sourceCharacter == actor.ModelId ? actor.Costume : 0);
        BattleModel motion = LoadPlayerForStyle(actor.ModelId, actor.Costume, sourceCharacter, actor.WeaponId);
        int? sourceWeapon = WeaponDefinitions.FirstOrDefault(weapon => weapon.CharacterId == sourceCharacter)?.Id;
        BattleModel sourceSequenceOwner = LoadPlayerSequenceOwner(sourceCharacter, sourceWeapon, sourceBody);
        NativeBattleGraphicsResource source = (sourceSequenceOwner.Sequences ?? throw new InvalidDataException("The selected Limit family has no original body sequence owner.")).NativeResource;
        int equippedStyle = actor.WeaponProfile?.ResourceOwner ?? (actor.WeaponId is int weapon ? ReadWeaponProfile(weapon).ResourceOwner : actor.ModelId);
        bool unarmed = family == BattleLimitFamily.Duel || BattleLimitFamilyRules.RequiredWeapon(family) == BattleWeaponCapabilities.None && sourceCharacter != equippedStyle;
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
        if (unarmed && geometry is { } unarmedBody)
        {
            BattleModel ownBody = LoadPlayer(actor.ModelId, actor.Costume);
            ImmutableArray<NativeBattleRawMesh> ownGeometry = actor.ModelId == 4 ? NativeBattleGeometryMetadata.FromMeshes(ownBody.Meshes.Select((mesh, index) => index == 0 ? SelectRinoaLauncherMount(mesh, ownBody, false) : mesh).ToArray()) : ownBody.NativeRenderGeometry ?? NativeBattleGeometryMetadata.FromMeshes(ownBody.Meshes);
            geometry = unarmedBody.SetItem(0, ownGeometry[0]);
        }
        NativeBattleGraphicsResource presentation = equipment with
        {
            SequenceSection = source.SequenceSection,
            SequenceSectionAddress = source.SequenceSectionAddress,
            SequenceSoundModelId = checked((byte)sourceCharacter),
            AnimationFrames = motion.Animations.Select(animation => checked((byte)animation.Frames.Count)).ToImmutableArray(),
            PoseSource = motion.NativePoseSource,
            HasActionCamera = sourceBody.NativeActionCameraBank is not null,
            ActionCameraBank = sourceBody.NativeActionCameraBank,
            Geometry = geometry,
            LinkedGeometry = unarmed ? [] : equipment.LinkedGeometry,
            LinkedGeometryUsesBodySkeleton = unarmed ? false : equipment.LinkedGeometryUsesBodySkeleton,
            LinkedPoseSource = unarmed ? null : equipment.LinkedPoseSource,
            LinkedAnimationFrames = unarmed ? [] : equipment.LinkedAnimationFrames,
            EquipmentPresentationRole = unarmed ? NativeBattleEquipmentPresentationRole.FreeHands : NativeBattleEquipmentPresentationRole.Equipped
        };
        presentation = BindActorSurfaces(presentation, actor, unarmed, family == BattleLimitFamily.Combine);
        presentation = BindLoadoutAnimationBanks(presentation, actor, LoadPlayer(actor.ModelId, actor.Costume), family);
        return BindLimitGunbladeMotion(presentation, actor, family, sourceCharacter, equippedStyle);
    }

    public ImmutableArray<NativeBattleLimitPresentationBinding> ReadNativeLimitPresentations(IReadOnlyList<BattleCombatantDefinition> actors, ImmutableArray<NativeBattleGraphicsBinding> standing)
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
