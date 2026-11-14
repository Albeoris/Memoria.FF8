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
        NativeBattleGraphicsRoster roster = owner.ReadExpandedPresentationRoster();
        byte sourceIndex = owner.ReadExpandedPresentationRow(caster.Id);
        foreach (ExpandedBattleContact contact in contacts)
        {
            if (roster[contact.Contact.ActorIndex].ActorId != contact.RecipientId)
            {
                throw new InvalidDataException("Expanded effect contact does not match its presentation roster binding.");
            }
        }
        NativeBattleEffectContactBridge<string> bridge = new(contacts.Select(contact => (contact.RecipientId, contact.Packet)), roster);
        (NativeBattleActionPacketsState packets, NativeBattleGraphicsActorSet targets) = bridge.PrepareHosted(action.Command, sequence, action.ActionId, action.MagicId, sourceIndex);

        ExpandedBattleSceneFrame frame = ReadSceneFrame(owner);
        bool guardianSummon = action is { Command: 254, KernelSection: 2 } && formMode is null;
        bool activatesActor = definition.UsesOriginalEnemySlots && action is { Command: 8, KernelSection: 3, ActionId: 287, MagicId: 20 };
        bool originalGraphic = activatesActor || action.Command == 252 && definition.UsesOriginalEnemySlots;
        int? casterForm = formMode == GuardianFormInvocationMode.EntryOnly ? BattleGuardianFormProfiles.Find(owner.GetGuardianForm(caster.Id)!.GuardianForceId)!.ModelId : owner.GetGuardianFormVisual(caster.Id);
        NativeBattleGraphicsResource sourceResource = GetResource(owner, caster, casterForm);
        NativeBattleGraphicsModel source = CreateModel(owner, caster, sourceResource, casterForm, frame.Positions[caster.Id]) with { Index = sourceIndex };
        if (!caster.IsActive)
        {
            source = source with { Flags0 = 0 };
        }

        var nativeTargets = ImmutableArray.CreateBuilder<NativeBattleEffectModelBinding>();
        var modelBindings = ImmutableArray.CreateBuilder<NativeBattleGraphicsBinding>();
        modelBindings.Add(new(caster.Id, source.ModelId4!.Value, sourceResource));
        var combatants = owner.Combatants.ToDictionary(actor => actor.Id, StringComparer.Ordinal);
        for (int row = 0; row < roster.ActorCount; row++)
        {
            if (row == sourceIndex || roster.Rows[row] is not { } binding)
            {
                continue;
            }
            BattleCombatant participant = combatants[binding.ActorId];
            int? form = owner.GetGuardianFormVisual(participant.Id);
            NativeBattleGraphicsResource resource = GetResource(owner, participant, form);
            NativeBattleGraphicsModel model = CreateModel(owner, participant, resource, form, frame.Positions[participant.Id]) with { Index = row };
            if (!participant.IsActive)
            {
                model = model with { Flags0 = 0 };
            }
            nativeTargets.Add(new(participant.Id, model, resource)
            {
                CanActivate = activatesActor && participant.IsActive && participant is { Hidden: true, Suspended: true, Untargetable: true },
                IsParty = binding.IsParty
            });
            modelBindings.Add(new(participant.Id, model.ModelId4!.Value, resource));
        }

        NativeBattleEffectInitializerAsset common = definition.NativeCommonEffectTexture ?? throw new InvalidDataException("Expanded battle effects require the installed common texture.");
        (NativeBattleEffectTextureState textures, _) = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(new(common.Name, common.Data));
        textures = textures.Drain505D20(NativeBattleEffectMemory.FromColdProcess().Arena).State;
        BattleDefinition resources = definition with
        {
            NativeGraphics = new(rules, modelBindings.ToImmutable(), [])
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
        NativeBattleEffectParameters parameters = new(sourceIndex, parameterFlags, targets.NativePhysicalBits, 0, 0, 0)
        {
            Parameter80 = 0,
            Parameter82 = 0,
            PresentationTargets = targets
        };
        return new(new(resources, source, sourceResource, packets, parameters, action.MagicId, textures, NativeCrtRandomState.FromColdProcess(), frame.Projection, [])
        {
            CameraMode = cameraMode,
            HostCameraPose = frame.Camera,
            GuardianFormMode = formMode,
            GuardianSummon = guardianSummon,
            PartyActorCount = roster.PartyRows.Count(),
            SourceIsParty = roster[sourceIndex].IsParty,
            NativeTargets = nativeTargets.ToImmutable()
        });
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
        // TODO(TD-009): Use the first installed idle pose until the host retains native entry and idle progression.
        model = NativeBattleGraphicsModels.SelectSequence(model with { Rotation14 = model.Transform!.Angle36, Flags8 = rules.Project(status.Saved, status.Battle) }, characterModel ? (byte)1 : (byte)0);
        model = model with { SequenceState = model.SequenceState! with { Pose = 0, PoseFrame114 = 1, PoseLimit115 = resource.AnimationFrames[0], DecodedPose = poseSource.Sample(0, 1, 0) } };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, poseSource);
        model = NativeBattlePoseMatrices.PublishShadowBounds5086F0(model, resource.PoseSource!);
        return model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
    }
}
