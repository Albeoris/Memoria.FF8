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

    internal ExpandedBattlePresentationProcess CreateProcess(BattleSession owner)
    {
        BattleDefinition definition = owner.ReadDefinition();
        BattleModelRoster roster = owner.ReadBattlePresentationRoster();
        ImmutableDictionary<string, NativeBattleVector> positions = sceneFrames.ReadPositions(owner);
        ImmutableArray<BattleModelOwner> actors = roster.Owners.ToImmutableArray();
        var combatants = owner.Combatants.ToDictionary(actor => actor.Id, StringComparer.Ordinal);
        ImmutableArray<NativeBattleGraphicsBinding> bindings = actors.Select(modelOwner =>
        {
            BattleCombatant actor = combatants[ActorId(modelOwner)];
            BattleRosterSlot slot = actor.Slot ?? throw new InvalidDataException("An entry model owner requires its allocated slot.");
            return PrepareModelBinding(owner, modelOwner, actor, slot, positions[actor.Id]);
        }).ToImmutableArray();
        ImmutableArray<string> loadOrder = actors.OrderBy(actor => actor.Side == BattleSide.Allies).Select(ActorId).ToImmutableArray();
        NativeBattleGraphicsDefinition graphics = new(rules, bindings, loadOrder)
        {
            PrimitiveTables = tables,
            SoftwarePrimitiveTable = softwareTable,
            PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8(),
            PcUiIcons = assets.ReadNativePcUiIcons().Binding
        };
        return new(owner, definition with { NativeGraphics = graphics });
    }

    public NativeBattleGraphicsBinding PrepareModelBinding(BattleSession owner, BattleModelOwner modelOwner, BattleRosterSlot slot)
    {
        string actorId = ActorId(modelOwner);
        BattleCombatant actor = owner.Combatants.SingleOrDefault(candidate => candidate.Id == actorId)
            ?? throw new InvalidDataException("The requested model owner does not belong to the supplied battle.");
        NativeBattleVector position = sceneFrames.ReadPosition(owner, actor, slot);
        return PrepareModelBinding(owner, modelOwner, actor, slot, position);
    }

    private NativeBattleGraphicsBinding PrepareModelBinding(BattleSession owner, BattleModelOwner modelOwner, BattleCombatant actor, BattleRosterSlot slot, NativeBattleVector position)
    {
        bool matches = actor.IsPlayer
            ? modelOwner == new BattlePartyActorModel(new(actor.Id))
            : modelOwner == new BattleEnemyActorModel(new(actor.Id));
        if (!matches || modelOwner.Side != slot.Side || actor.Slot is { } current && current != slot ||
            actor.Slot is null && (actor.IsPlayer || actor.SceneSlot is null || !owner.ReadDefinition().UsesOriginalEnemySlots))
        {
            throw new InvalidDataException("An installed model binding requires its original allocation, slot and actor identity.");
        }
        byte modelId = checked((byte)(actor.IsPlayer ? actor.ModelId : actor.ModelId + 16));
        return new(actor.Id, modelId, GetResource(owner, actor, null)) { InitialPosition = position };
    }

    public NativeBattleEffectPresentation PrepareEffect(BattleSession owner, BattleCombatant caster, BattleNativeAction action, byte sequence, ImmutableArray<ExpandedBattleContact> contacts, GuardianFormInvocationMode? formMode)
    {
        BattleDefinition definition = owner.ReadDefinition();
        (BattleModelRoster roster, NativeBattleModelAliases aliases) = owner.ReadBattlePresentationPacketBindings();
        BattleModelOwner sourceOwner = owner.ReadPresentationModelOwner(caster.Id);
        foreach (ExpandedBattleContact contact in contacts)
        {
            if (owner.ReadPresentationModelOwner(contact.RecipientId) != contact.Contact.Recipient || !roster.Contains(contact.Contact.Recipient))
            {
                throw new InvalidDataException("Expanded effect contact does not match its presentation roster binding.");
            }
        }

        NativeBattleEffectInvocation invocation;
        if (formMode == GuardianFormInvocationMode.EntryOnly)
        {
            if (!contacts.IsEmpty)
            {
                throw new InvalidDataException("Guardian form positioning cannot consume combat contacts.");
            }
            invocation = new NativeBattleEffectInvocation.Positioning(sourceOwner, roster, aliases);
        }
        else
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases);
            (packets, _) = packets.Prepare(new(sourceOwner, action.Command, sequence, -1, action.ActionId, action.MagicId, null));
            packets = packets.AppendContacts(contacts.Select(contact => contact.Packet));
            (packets, BattleActionHeaderReference header) = packets.Publish(contacts.Length);
            BattleModelTargets targets = BattleModelTargets.From(roster.Owners.Where(model => contacts.Any(contact => contact.Contact.Recipient == model)));
            NativeBattleEffectParameters parameters = new(sourceOwner, action.Command == 247 ? (byte)1 : (byte)0, targets, header, 0, 0)
            {
                Parameter80 = 0,
                Parameter82 = 0
            };
            invocation = new NativeBattleEffectInvocation.Combat(packets, parameters);
        }

        ExpandedBattleSceneFrame frame = ReadSceneFrame(owner);
        bool guardianSummon = action is { Command: 254, KernelSection: 2 } && formMode is null;
        bool activatesActor = definition.UsesOriginalEnemySlots && action is { Command: 8, KernelSection: 3, ActionId: 287, MagicId: 20 };
        bool originalGraphic = activatesActor || action.Command == 252 && definition.UsesOriginalEnemySlots;
        int? casterForm = formMode == GuardianFormInvocationMode.EntryOnly ? BattleGuardianFormProfiles.Find(owner.GetGuardianForm(caster.Id)!.GuardianForceId)!.ModelId : owner.GetGuardianFormVisual(caster.Id);
        NativeBattleGraphicsResource sourceResource = GetResource(owner, caster, casterForm);
        NativeBattleGraphicsModel source = CreateModel(owner, caster, sourceResource, casterForm, frame.Positions[caster.Id]);
        if (!caster.IsActive)
        {
            source = source with { Flags0 = 0 };
        }

        var nativeTargets = ImmutableArray.CreateBuilder<NativeBattleEffectModelBinding>();
        var modelBindings = ImmutableArray.CreateBuilder<NativeBattleGraphicsBinding>();
        modelBindings.Add(new(caster.Id, source.ModelId4!.Value, sourceResource));
        var combatants = owner.Combatants.ToDictionary(actor => actor.Id, StringComparer.Ordinal);
        foreach (BattleModelOwner binding in roster.Owners)
        {
            if (binding == sourceOwner)
            {
                continue;
            }
            BattleCombatant participant = combatants[ActorId(binding)];
            int? form = owner.GetGuardianFormVisual(participant.Id);
            NativeBattleGraphicsResource resource = GetResource(owner, participant, form);
            NativeBattleGraphicsModel model = CreateModel(owner, participant, resource, form, frame.Positions[participant.Id]);
            if (!participant.IsActive)
            {
                model = model with { Flags0 = 0 };
            }
            nativeTargets.Add(new(binding, model, resource)
            {
                CanActivate = activatesActor && participant.IsActive && participant is { Hidden: true, Suspended: true, Untargetable: true }
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
        return new(new(resources, source, sourceResource, invocation, action.MagicId, textures, NativeCrtRandomState.FromColdProcess(), frame.Projection, [])
        {
            CameraMode = cameraMode,
            HostCameraPose = frame.Camera,
            GuardianFormMode = formMode,
            GuardianSummon = guardianSummon,
            PartyActorCount = roster.Party.Count(),
            NativeTargets = nativeTargets.ToImmutable()
        });
    }

    private static string ActorId(BattleModelOwner owner) => owner switch
    {
        BattlePartyActorModel party => party.Actor.ActorId,
        BattleEnemyActorModel enemy => enemy.Actor.ActorId,
        _ => throw new InvalidDataException("Expanded battle presentation requires a canonical actor owner.")
    };

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
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(new(0)
        {
            Transform = new(position, position, 0, actor.IsPlayer ? (short)0 : (short)2048, scale),
            LinkedTransform = new(default, default),
            Color40 = rules.SummonColors!.Transition,
            Color44 = (rules.SummonColors!.Normal & 0xFFFFFFu) | 0x32000000u,
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }
        }, checked((byte)(characterModel ? modelId : modelId + 16)), 0, actor.IsPlayer, ref random, characterModel ? null : assets.ReadEnemyNativeInformation246(modelId));
        // TODO(TD-009): Use the first installed idle pose until the host retains native entry and idle progression.
        model = NativeBattleGraphicsModels.SelectSequence(model with { Rotation14 = model.Transform!.Angle36, Flags8 = rules.Project(status.Saved, status.Battle) }, characterModel ? (byte)1 : (byte)0);
        model = model with { SequenceState = model.SequenceState! with { Pose = 0, PoseFrame114 = 1, PoseLimit115 = resource.AnimationFrames[0], DecodedPose = poseSource.Sample(0, 1, 0) } };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, poseSource);
        model = NativeBattlePoseMatrices.PublishShadowBounds5086F0(model, resource.PoseSource!);
        return model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
    }
}
