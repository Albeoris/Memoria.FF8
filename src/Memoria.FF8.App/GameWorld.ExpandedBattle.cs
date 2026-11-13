using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly bool expandedRandomBattles;

    private static BattleEnemyPlacement[]? ExpandRandomEnemyRoster(BattleEncounter encounter)
    {
        BattleEnemyPlacement[] source = encounter.Slots.Where(slot => slot.Enabled).ToArray();
        if (source.Length is < 1 or > 6 || source.Any(slot => !slot.IsEnemyModel || slot.NativeModelId == 255 || slot.Hidden || slot.Untargetable || slot.InitiallySuspended))
        {
            return null;
        }
        return Enumerable.Range(0, 6).Select(index => source[index % source.Length] with { Slot = index }).ToArray();
    }

    internal static void AttachExpandedEffects(BattleSession active, GameAssets game, BattleAssets assets)
    {
        BattleDefinition definition = active.ReadDefinition();
        Dictionary<string, BattleCombatantDefinition> definitions = definition.Combatants.ToDictionary(actor => actor.Id);
        BattleStageCamera camera = ExpandedBattleCamera();
        NativeBattleCameraPose pose = new(Point(camera.NativeEye), Point(camera.NativeTarget), checked((short)camera.FocalLength), 0);
        NativeBattleCameraProjection projection = new(NativeBattlePoseMatrices.CameraView(pose), checked((ushort)camera.FocalLength), 160, 108);
        NativeBattlePrimitiveTables tables = assets.ReadNativePrimitiveTables();
        var softwareTable = assets.ReadNativeSoftwarePrimitiveTable();
        var rules = game.ReadNativeBattleGraphicsRules();
        Dictionary<string, NativeBattleGraphicsResource> actorResources = definitions.ToDictionary(pair => pair.Key, pair => assets.ReadNativeGraphicsResource(pair.Value));
        Dictionary<string, NativeBattleGraphicsModel> posedActors = active.Combatants.ToDictionary(actor => actor.Id, CreateModel);
        active.AttachExpandedEffectFactory((caster, action, contacts) =>
        {
            NativeBattleGraphicsResource sourceResource = actorResources[caster.Id];
            NativeBattleGraphicsModel source = posedActors[caster.Id];
            NativeBattleEffectContactBridge<string> bridge = new(contacts.Select(contact => (contact.RecipientId, contact.Contact)));
            (NativeBattleActionPacketsState packets, ushort mask) = bridge.Prepare(action.Command, 1, action.ActionId, action.MagicId);
            ImmutableArray<NativeBattleForeignActor> targets = contacts.Where(contact => contact.RecipientId != caster.Id).Select(contact =>
            {
                BattleCombatant recipient = active.Combatants.Single(actor => actor.Id == contact.RecipientId);
                NativeBattleGraphicsModel model = posedActors[recipient.Id] with { Index = contact.Contact.ActorIndex };
                NativeBattlePoseSource targetPose = actorResources[recipient.Id].PoseSource!;
                NativeBattleSequenceState sequence = model.SequenceState!;
                NativeBattleEffectActorGeometry anchors = new(NativeBattlePoseMatrices.ReadBone(model, targetPose, 240, 0), NativeBattlePoseMatrices.ReadBone(model, targetPose, 241, 0), model.Transform!.Position28.Y, NativeBattlePoseMatrices.LocalBounds8DC870(targetPose, targetPose.Sample(sequence.Pose!.Value, sequence.PoseFrame114!.Value, sequence.PoseFlags109)));
                return new NativeBattleForeignActor(contact.Contact.ActorIndex, model.Transform, NativeBattlePoseMatrices.ReadAnchor(model, targetPose), (selector, fraction) => NativeBattlePoseMatrices.ReadBone(model, targetPose, selector, fraction))
                {
                    Facing = model.Transform.Angle36,
                    Geometry = new(model.GeometryWorldMatrix!, anchors, [])
                };
            }).ToImmutableArray();
            BattleDefinition resources = definition with
            {
                NativeGraphics = new(rules, [new(caster.Id, 0, sourceResource)], [])
                {
                    PrimitiveTables = tables,
                    SoftwarePrimitiveTable = softwareTable,
                    PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8()
                },
                NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([action.MagicId])
            };
            return new(new(resources, source, sourceResource, packets, new(0, 0, mask, 0, 0, 0), action.MagicId, NativeBattleEffectTextureState.FromColdProcess(), NativeCrtRandomState.FromColdProcess(), projection, targets));
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
                Color44 = (rules.SummonColors!.Normal & 0xFFFFFFu) | 0x32000000u,
                SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
            }, checked((byte)(actor.IsPlayer ? actorDefinition.ModelId : actorDefinition.ModelId + 16)), 0, ref random, actor.IsPlayer ? null : assets.ReadEnemyNativeInformation246(actorDefinition.ModelId));
            // Use the first installed idle pose, independently of model initialization scripts.
            NativeBattlePoseSource poseSource = resource.PoseSource!;
            model = NativeBattleGraphicsModels.SelectSequence(model, actor.IsPlayer ? (byte)1 : (byte)0);
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

    internal static BattleStageCamera ExpandedBattleCamera()
    {
        return new(new(5400, -4800, 6600), new(0, -350, 0), 340);
    }
}
