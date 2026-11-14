using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class ExpandedBattlePresentationHost(GameAssets game, BattleAssets assets) : IExpandedBattlePresentationHost
{
    private readonly ExpandedBattleSceneFrameBuilder sceneFrames = new(assets);
    private readonly NativeBattlePrimitiveTables tables = assets.ReadNativePrimitiveTables();
    private readonly NativeSoftwarePrimitiveTable softwareTable = assets.ReadNativeSoftwarePrimitiveTable();
    private readonly NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
    private readonly Dictionary<(bool IsPlayer, int Model, int Costume, int? Weapon), NativeBattleGraphicsResource> actorResources = [];

    public ExpandedBattleSceneFrame ReadSceneFrame(BattleSession owner) => sceneFrames.ReadSceneFrame(owner);

    public NativeBattleEffectPresentation PrepareEffect(BattleSession owner, BattleCombatant caster, BattleNativeAction action, byte sequence, ImmutableArray<ExpandedBattleContact> contacts, GuardianFormInvocationMode? formMode)
    {
        BattleDefinition definition = owner.ReadDefinition();
        ExpandedBattleSceneFrame frame = ReadSceneFrame(owner);
        bool guardianSummon = action is { Command: 254, KernelSection: 2 } && formMode is null;
        bool activatesActor = definition.UsesOriginalEnemySlots && action is { Command: 8, KernelSection: 3, ActionId: 287, MagicId: 20 };
        bool originalGraphic = activatesActor || action.Command == 252 && definition.UsesOriginalEnemySlots;
        int? casterForm = formMode == GuardianFormInvocationMode.EntryOnly ? BattleGuardianFormProfiles.Find(owner.GetGuardianForm(caster.Id)!.GuardianForceId)!.ModelId : owner.GetGuardianFormVisual(caster.Id);
        NativeBattleGraphicsResource sourceResource = GetResource(owner, caster, casterForm);
        byte sourceIndex = originalGraphic ? PhysicalIndex(caster) : (byte)0;
        (NativeBattleGraphicsModel source, NativeBattleEffectTextureState textures) = NativeEffectActorTextures.Allocate(CreateModel(owner, caster, sourceResource, casterForm, frame.Positions[caster.Id]) with { Index = sourceIndex }, sourceResource);
        ushort occupiedSlots = source.TextureAllocationMask.GetValueOrDefault();
        ImmutableArray<NativeBattleEffectModelBinding>.Builder nativeTargets = ImmutableArray.CreateBuilder<NativeBattleEffectModelBinding>();
        ImmutableArray<NativeBattleForeignActor>.Builder foreignTargets = ImmutableArray.CreateBuilder<NativeBattleForeignActor>();
        if (originalGraphic)
        {
            foreach (BattleCombatant participant in owner.Combatants.Where(actor => actor != caster && actor.NativeActorIndex.HasValue))
            {
                NativeBattleGraphicsResource resource = GetResource(owner, participant, null);
                NativeBattleGraphicsModel model = CreateModel(owner, participant, resource, null, frame.Positions[participant.Id]) with { Index = PhysicalIndex(participant) };
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
            int? recipientForm = owner.GetGuardianFormVisual(contact.RecipientId);
            BattleCombatant recipient = owner.Combatants.Single(actor => actor.Id == contact.RecipientId);
            NativeBattleGraphicsResource resource = GetResource(owner, recipient, recipientForm);
            NativeBattleGraphicsModel target = CreateModel(owner, recipient, resource, recipientForm, frame.Positions[recipient.Id]) with { Index = contact.Contact.ActorIndex };
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
        return new(new(resources, source, sourceResource, packets, new(sourceIndex, parameterFlags, mask, 0, 0, 0) { Parameter80 = 0, Parameter82 = 0 }, action.MagicId, textures, NativeCrtRandomState.FromColdProcess(), frame.Projection, foreignTargets.ToImmutable())
        {
            CameraMode = cameraMode,
            HostCameraPose = frame.Camera,
            GuardianFormMode = formMode,
            GuardianSummon = guardianSummon,
            SourceIsParty = caster.IsPlayer,
            NativeTargets = nativeTargets.ToImmutable()
        });
    }

    private static byte PhysicalIndex(BattleCombatant actor)
    {
        return checked((byte)(actor.NativeActorIndex ?? throw new InvalidDataException("The original graphic requires its allocated native actor row.")));
    }

    private NativeBattleGraphicsResource GetResource(BattleSession owner, BattleCombatant actor, int? formModel)
    {
        (bool IsPlayer, int Model, int Costume, int? Weapon) key = formModel is int model
            ? (false, model, 0, null)
            : (actor.IsPlayer, actor.ModelId, actor.Costume, actor.WeaponId);
        if (!actorResources.TryGetValue(key, out NativeBattleGraphicsResource? resource))
        {
            resource = key.IsPlayer ? assets.ReadNativeGraphicsResource(owner.GetCombatantDefinition(actor.Id)) : assets.ReadEnemyNativeGraphicsResource(key.Model);
            actorResources.Add(key, resource);
        }
        return resource;
    }

    private NativeBattleGraphicsModel CreateModel(BattleSession owner, BattleCombatant actor, NativeBattleGraphicsResource resource, int? formModel, NativeBattleVector position)
    {
        NativeGlobalRandomState random = new(0);
        NativeBattlePoseSource poseSource = resource.PoseSource!;
        short scale = formModel is null ? (short)4096 : unchecked((short)(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(poseSource.Skeleton.AsSpan().Slice(2)) * 4));
        int modelId = formModel ?? actor.ModelId;
        bool characterModel = actor.IsPlayer && formModel is null;
        BattleStatusPresentation status = owner.ReadStatusPresentation(actor.Id);
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
}
