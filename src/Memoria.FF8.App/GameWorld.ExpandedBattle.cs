using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly int defaultExpandedRandomAllyCount;
    private readonly int? legacyExpandedEnemyCount;
    private readonly int defaultAdditionalEnemyCount;
    internal int ConfiguredBattleAllyCount => session.State.GameplayPreferences.BattleAllyCount == 0 ? defaultExpandedRandomAllyCount : session.State.GameplayPreferences.BattleAllyCount;
    internal int EffectiveBattleAllyCount => BattlePartySelectionPolicy.ResolveCapacity(session.State, ConfiguredBattleAllyCount == 0 ? 3 : ConfiguredBattleAllyCount);

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
        int cameraActorCount = definition.UsesOriginalEnemySlots && definition.AddedEnemySources is { Count: > 0 }
            ? definition.Combatants.Count(actor => actor.InitialActive)
            : active.Combatants.Count;
        BattleStageCamera camera = ExpandedBattleCamera(cameraActorCount);
        NativeBattleCameraPose pose = new(Point(camera.NativeEye), Point(camera.NativeTarget), checked((short)camera.FocalLength), 0);
        NativeBattleCameraProjection projection = new(NativeBattlePoseMatrices.CameraView(pose), checked((ushort)camera.FocalLength), 160, 108);
        NativeBattlePrimitiveTables tables = assets.ReadNativePrimitiveTables();
        var softwareTable = assets.ReadNativeSoftwarePrimitiveTable();
        var rules = game.ReadNativeBattleGraphicsRules();
        Dictionary<(string Id, int? FormModel), NativeBattleGraphicsResource> actorResources = [];
        active.AttachExpandedEffectFactory((caster, action, sequence, contacts, formMode) =>
        {
            bool guardianSummon = action is { Command: 254, KernelSection: 2 } && formMode is null;
            bool activatesActor = definition.UsesOriginalEnemySlots && action is { Command: 8, KernelSection: 3, ActionId: 287, MagicId: 20 };
            bool originalGraphic = activatesActor || action.Command == 252 && definition.UsesOriginalEnemySlots;
            int? casterForm = formMode == GuardianFormInvocationMode.EntryOnly ? BattleGuardianFormProfiles.Find(active.GetGuardianForm(caster.Id)!.GuardianForceId)!.ModelId : active.GetGuardianFormVisual(caster.Id);
            NativeBattleGraphicsResource sourceResource = GetResource(caster.Id, casterForm);
            byte sourceIndex = originalGraphic ? PhysicalIndex(caster) : (byte)0;
            (NativeBattleGraphicsModel source, NativeBattleEffectTextureState textures) = NativeEffectActorTextures.Allocate(CreateModel(caster, sourceResource, casterForm) with { Index = sourceIndex }, sourceResource);
            ushort occupiedSlots = source.TextureAllocationMask.GetValueOrDefault();
            ImmutableArray<NativeBattleEffectModelBinding>.Builder nativeTargets = ImmutableArray.CreateBuilder<NativeBattleEffectModelBinding>();
            ImmutableArray<NativeBattleForeignActor>.Builder foreignTargets = ImmutableArray.CreateBuilder<NativeBattleForeignActor>();
            if (originalGraphic)
            {
                foreach (BattleCombatant participant in active.Combatants.Where(actor => actor != caster && actor.NativeActorIndex.HasValue))
                {
                    NativeBattleGraphicsResource resource = GetResource(participant.Id, null);
                    NativeBattleGraphicsModel model = CreateModel(participant, resource, null) with { Index = PhysicalIndex(participant) };
                    if (!participant.IsActive)
                    {
                        model = model with { Flags0 = 0 };
                    }
                    else if (!participant.IsPlayer && participant.ModelId == caster.ModelId)
                    {
                        model = model with { BodyTextureSlots = source.BodyTextureSlots, LinkedTextureSlots = source.LinkedTextureSlots, TextureAllocationMask = source.TextureAllocationMask };
                    }
                    else
                    {
                        (model, textures) = NativeEffectActorTextures.Allocate(model, resource, textures, occupiedSlots);
                        occupiedSlots |= model.TextureAllocationMask.GetValueOrDefault();
                    }
                    nativeTargets.Add(new(participant.Id, model, resource)
                    {
                        CanActivate = activatesActor && participant.IsActive && participant is { Hidden: true, Suspended: true, Untargetable: true },
                        IsParty = participant.IsPlayer
                    });
                }
            }
            foreach (ExpandedBattleContact contact in contacts.Where(contact => !originalGraphic && contact.RecipientId != caster.Id).DistinctBy(contact => contact.RecipientId))
            {
                int? recipientForm = active.GetGuardianFormVisual(contact.RecipientId);
                NativeBattleGraphicsResource resource = GetResource(contact.RecipientId, recipientForm);
                BattleCombatant recipient = active.Combatants.Single(actor => actor.Id == contact.RecipientId);
                NativeBattleGraphicsModel target = CreateModel(recipient, resource, recipientForm) with { Index = contact.Contact.ActorIndex };
                if (action is { Command: 6, KernelSection: -1 } or { Command: 8, KernelSection: 3, ActionId: 175 } || action.MagicId is 1 or 25 or 39 or 50 or 139 || definition.NativeEffectCatalog.Any(binding => binding.Effect == action.MagicId && binding.Entry == NativeBattleLaserEye71Profile.Entry))
                {
                    (target, textures) = NativeEffectActorTextures.Allocate(target, resource, textures, occupiedSlots);
                    occupiedSlots |= target.TextureAllocationMask.GetValueOrDefault();
                    nativeTargets.Add(new(contact.RecipientId, target, resource) { IsParty = recipient.IsPlayer });
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
                        CanReact = guardianSummon,
                        AtStandingPose = guardianSummon ? target.Sequence is 0 or 1 : null,
                        StatusFlags = target.Flags8,
                        IsParty = recipient.IsPlayer,
                        Geometry = new(target.GeometryWorldMatrix!, anchors, [])
                    });
                }
            }
            NativeBattleEffectInitializerAsset common = definition.NativeCommonEffectTexture ?? throw new InvalidDataException("Expanded battle effects require the installed common texture.");
            (textures, _) = textures.EnqueueTim505E30(new(common.Name, common.Data));
            textures = textures.Drain505D20(NativeBattleEffectMemory.FromColdProcess().Arena).State;
            NativeBattleEffectContactBridge<string> bridge = new(contacts.Select(contact => (contact.RecipientId, contact.Packet)));
            (NativeBattleActionPacketsState packets, ushort mask) = bridge.Prepare(action.Command, sequence, action.ActionId, action.MagicId, sourceIndex);
            NativeBattleGraphicsBinding sourceBinding = new(caster.Id, source.ModelId4!.Value, sourceResource);
            ImmutableArray<NativeBattleGraphicsBinding> modelBindings = activatesActor
                ? [sourceBinding, .. nativeTargets.Select(target => new NativeBattleGraphicsBinding(target.ActorId, target.Model.ModelId4!.Value, target.Resource))]
                : [sourceBinding];
            BattleDefinition resources = definition with
            {
                NativeGraphics = new(rules, modelBindings, [])
                {
                    PrimitiveTables = tables,
                    SoftwarePrimitiveTable = softwareTable,
                    PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8()
                },
                NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([action.MagicId]),
                NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory()
            };
            NativeBattleEffectCameraMode cameraMode = formMode is not null || guardianSummon || originalGraphic ? NativeBattleEffectCameraMode.NativeCinematic : NativeBattleEffectCameraMode.HostPoseWithEffectTranslation;
            byte parameterFlags = action.Command == 247 ? (byte)1 : (byte)0;
            return new(new(resources, source, sourceResource, packets, new(sourceIndex, parameterFlags, mask, 0, 0, 0) { Parameter80 = 0, Parameter82 = 0 }, action.MagicId, textures, NativeCrtRandomState.FromColdProcess(), projection, foreignTargets.ToImmutable())
            {
                CameraMode = cameraMode,
                HostCameraPose = pose,
                GuardianFormMode = formMode,
                GuardianSummon = guardianSummon,
                SourceIsParty = caster.IsPlayer,
                NativeTargets = nativeTargets.ToImmutable()
            });
        });

        static byte PhysicalIndex(BattleCombatant actor)
        {
            return checked((byte)(actor.NativeActorIndex ?? throw new InvalidDataException("The original graphic requires its allocated native actor row.")));
        }

        NativeBattleGraphicsResource GetResource(string id, int? formModel)
        {
            var key = (id, formModel);
            if (!actorResources.TryGetValue(key, out NativeBattleGraphicsResource? resource))
            {
                resource = formModel is int modelId ? assets.ReadEnemyNativeGraphicsResource(modelId) : assets.ReadNativeGraphicsResource(active.GetCombatantDefinition(id));
                actorResources.Add(key, resource);
            }
            return resource;
        }

        NativeBattleGraphicsModel CreateModel(BattleCombatant actor, NativeBattleGraphicsResource resource, int? formModel)
        {
            BattleRosterSlot slot = actor.Slot ?? throw new InvalidDataException("Expanded actor has no roster position.");
            int sideCount = actor.IsPlayer ? definition.Combatants.Count(candidate => candidate.IsPlayer) : definition.Combatants.Count(candidate => !candidate.IsPlayer);
            NativeBattleVector position = definition.UsesOriginalEnemySlots && !actor.IsPlayer
                ? definition.AddedEnemySources is { } added && actor.SceneSlot is int sceneSlot && added.ContainsKey(sceneSlot)
                    ? ExpandedBattleFormation.AddedEnemyPosition(sceneSlot, added.Count)
                    : assets.LoadEncounter(definition.EncounterId!.Value).Slots.Single(placement => placement.Slot == actor.SceneSlot).NativePosition ?? throw new InvalidDataException("Original enemy lost its authored position.")
                : ExpandedBattleFormation.Position(slot.Position, sideCount, actor.IsPlayer);
            NativeGlobalRandomState random = new(0);
            NativeBattlePoseSource poseSource = resource.PoseSource!;
            short scale = formModel is null ? (short)4096 : unchecked((short)(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(poseSource.Skeleton.AsSpan().Slice(2)) * 4));
            int modelId = formModel ?? actor.ModelId;
            bool characterModel = actor.IsPlayer && formModel is null;
            BattleStatusPresentation status = active.ReadStatusPresentation(actor.Id);
            NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0, 0)
            {
                Transform = new(position, position, 0, actor.IsPlayer ? (short)0 : (short)2048, scale),
                LinkedTransform = new(default, default),
                Color40 = rules.SummonColors!.Transition,
                Color44 = (rules.SummonColors!.Normal & 0xFFFFFFu) | 0x32000000u,
                SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
            }, checked((byte)(characterModel ? modelId : modelId + 16)), 0, ref random, characterModel ? null : assets.ReadEnemyNativeInformation246(modelId));
            // Use the first installed idle pose, independently of model initialization scripts.
            model = NativeBattleGraphicsModels.SelectSequence(model with { Rotation14 = model.Transform!.Angle36, Flags8 = rules.Project(status.Saved, status.Battle) }, characterModel ? (byte)1 : (byte)0);
            model = model with { SequenceState = model.SequenceState! with { Pose = 0, PoseFrame114 = 1, PoseLimit115 = resource.AnimationFrames[0], DecodedPose = poseSource.Sample(0, 1, 0) } };
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
