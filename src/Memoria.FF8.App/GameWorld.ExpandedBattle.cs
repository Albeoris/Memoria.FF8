using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly int defaultExpandedRandomAllyCount;
    private readonly int defaultLargeRandomEnemyCount;
    internal int ConfiguredBattleAllyCount => session.State.GameplayPreferences.BattleAllyCount == 0 ? defaultExpandedRandomAllyCount : session.State.GameplayPreferences.BattleAllyCount;

    private void EnsureGuestBattleRecords()
    {
        if (ConfiguredBattleAllyCount == 11 && session.State.CharacterVitals is { Records.Length: NativeCharacterVitals.NativeRecordCount } vitals && !session.HasActiveBattle && session.State.PendingBattleEncounter is null)
        {
            session.SetCharacterVitals(vitals.WithGuestBattleRecords());
        }
    }

    private static BattleEnemyPlacement[]? ExpandRandomEnemyRoster(BattleEncounter encounter, int enemyCount)
    {
        BattleEnemyPlacement[] source = encounter.Slots.Where(slot => slot.Enabled).ToArray();
        if (source.Length < 1 || source.Length > enemyCount || source.Any(slot => !slot.IsEnemyModel || slot.NativeModelId == 255 || slot.Hidden || slot.Untargetable || slot.InitiallySuspended))
        {
            return null;
        }
        return Enumerable.Range(0, enemyCount).Select(index => source[index % source.Length] with { Slot = index }).ToArray();
    }

    internal static void AttachExpandedEffects(BattleSession active, GameAssets game, BattleAssets assets)
    {
        BattleDefinition definition = active.ReadDefinition();
        Dictionary<string, BattleCombatantDefinition> definitions = definition.Combatants.ToDictionary(actor => actor.Id);
        BattleStageCamera camera = ExpandedBattleCamera(active.Combatants.Count);
        NativeBattleCameraPose pose = new(Point(camera.NativeEye), Point(camera.NativeTarget), checked((short)camera.FocalLength), 0);
        NativeBattleCameraProjection projection = new(NativeBattlePoseMatrices.CameraView(pose), checked((ushort)camera.FocalLength), 160, 108);
        NativeBattlePrimitiveTables tables = assets.ReadNativePrimitiveTables();
        var softwareTable = assets.ReadNativeSoftwarePrimitiveTable();
        var rules = game.ReadNativeBattleGraphicsRules();
        Dictionary<string, NativeBattleGraphicsResource> actorResources = definitions.ToDictionary(pair => pair.Key, pair => assets.ReadNativeGraphicsResource(pair.Value));
        Dictionary<string, NativeBattleGraphicsModel> posedActors = active.Combatants.ToDictionary(actor => actor.Id, CreateModel);
        active.AttachExpandedEffectFactory((caster, action, sequence, contacts) =>
        {
            NativeBattleGraphicsResource sourceResource = actorResources[caster.Id];
            (NativeBattleGraphicsModel source, NativeBattleEffectTextureState textures) = NativeEffectActorTextures.Allocate(posedActors[caster.Id], sourceResource);
            ushort occupiedSlots = source.TextureAllocationMask.GetValueOrDefault();
            ImmutableArray<NativeBattleEffectModelBinding>.Builder nativeTargets = ImmutableArray.CreateBuilder<NativeBattleEffectModelBinding>();
            ImmutableArray<NativeBattleForeignActor>.Builder foreignTargets = ImmutableArray.CreateBuilder<NativeBattleForeignActor>();
            foreach (ExpandedBattleContact contact in contacts.Where(contact => contact.RecipientId != caster.Id))
            {
                NativeBattleGraphicsModel target = posedActors[contact.RecipientId] with { Index = contact.Contact.ActorIndex };
                NativeBattleGraphicsResource resource = actorResources[contact.RecipientId];
                if (action.MagicId is 1 or 25 or 39 or 50 or 139)
                {
                    (target, textures) = NativeEffectActorTextures.Allocate(target, resource, textures, occupiedSlots);
                    occupiedSlots |= target.TextureAllocationMask.GetValueOrDefault();
                    nativeTargets.Add(new(contact.RecipientId, target, resource));
                }
                else
                {
                    NativeBattlePoseSource targetPose = resource.PoseSource!;
                    NativeBattleSequenceState poseSequence = target.SequenceState!;
                    NativeBattleEffectActorGeometry anchors = new(NativeBattlePoseMatrices.ReadBone(target, targetPose, 240, 0), NativeBattlePoseMatrices.ReadBone(target, targetPose, 241, 0), target.Transform!.Position28.Y, NativeBattlePoseMatrices.LocalBounds8DC870(targetPose, targetPose.Sample(poseSequence.Pose!.Value, poseSequence.PoseFrame114!.Value, poseSequence.PoseFlags109)));
                    foreignTargets.Add(new(contact.Contact.ActorIndex, target.Transform, NativeBattlePoseMatrices.ReadAnchor(target, targetPose), (selector, fraction) => NativeBattlePoseMatrices.ReadBone(target, targetPose, selector, fraction))
                    {
                        Facing = target.Transform.Angle36,
                        BoneCount = targetPose.BoneCount,
                        Geometry = new(target.GeometryWorldMatrix!, anchors, [])
                    });
                }
            }
            NativeBattleEffectInitializerAsset common = definition.NativeCommonEffectTexture ?? throw new InvalidDataException("Expanded battle effects require the installed common texture.");
            (textures, _) = textures.EnqueueTim505E30(new(common.Name, common.Data));
            textures = textures.Drain505D20(NativeBattleEffectMemory.FromColdProcess().Arena).State;
            NativeBattleEffectContactBridge<string> bridge = new(contacts.Select(contact => (contact.RecipientId, contact.Packet)));
            (NativeBattleActionPacketsState packets, ushort mask) = bridge.Prepare(action.Command, sequence, action.ActionId, action.MagicId);
            BattleDefinition resources = definition with
            {
                NativeGraphics = new(rules, [new(caster.Id, 0, sourceResource)], [])
                {
                    PrimitiveTables = tables,
                    SoftwarePrimitiveTable = softwareTable,
                    PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8()
                },
                NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([action.MagicId]),
                NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory()
            };
            return new(new(resources, source, sourceResource, packets, new(0, 0, mask, 0, 0, 0), action.MagicId, textures, NativeCrtRandomState.FromColdProcess(), projection, foreignTargets.ToImmutable()) { CameraMode = NativeBattleEffectCameraMode.HostPoseWithEffectTranslation, NativeTargets = nativeTargets.ToImmutable() });
        });

        NativeBattleGraphicsModel CreateModel(BattleCombatant actor)
        {
            BattleCombatantDefinition actorDefinition = definitions[actor.Id];
            NativeBattleGraphicsResource resource = actorResources[actor.Id];
            BattleRosterSlot slot = actor.Slot ?? throw new InvalidDataException("Expanded actor has no roster position.");
            int sideCount = active.Combatants.Count(candidate => candidate.Slot?.Side == slot.Side);
            NativeBattleVector position = ExpandedBattleFormation.Position(slot.Position, sideCount, actor.IsPlayer);
            NativeGlobalRandomState random = new(0);
            NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
            {
                Transform = new(position, position, 0, actor.IsPlayer ? (short)0 : (short)2048, 4096),
                LinkedTransform = new(default, default),
                Color40 = rules.SummonColors!.Transition,
                Color44 = (rules.SummonColors!.Normal & 0xFFFFFFu) | 0x32000000u,
                SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
            }, checked((byte)(actor.IsPlayer ? actorDefinition.ModelId : actorDefinition.ModelId + 16)), 0, ref random, actor.IsPlayer ? null : assets.ReadEnemyNativeInformation246(actorDefinition.ModelId));
            // Use the first installed idle pose, independently of model initialization scripts.
            NativeBattlePoseSource poseSource = resource.PoseSource!;
            model = NativeBattleGraphicsModels.SelectSequence(model with { Rotation14 = model.Transform!.Angle36 }, actor.IsPlayer ? (byte)1 : (byte)0);
            model = model with { SequenceState = model.SequenceState! with { Pose = 0, PoseFrame114 = 1, DecodedPose = poseSource.Sample(0, 1, 0) } };
            model = NativeBattleBodyBoneMatrices.Publish508C90(model, poseSource);
            model = NativeBattlePoseMatrices.PublishShadowBounds5086F0(model, resource.PoseSource!);
            return model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        }

        static NativeBattleCameraPoint Point(Vector3 vector)
        {
            return new(checked((short)vector.X), checked((short)vector.Y), checked((short)vector.Z));
        }
    }

    internal static BattleStageCamera ExpandedBattleCamera(int actorCount)
    {
        return new(new(5400, -4800, 6600), new(0, -350, 0), actorCount > 16 ? 250 : actorCount > 10 ? 285 : 340);
    }
}
