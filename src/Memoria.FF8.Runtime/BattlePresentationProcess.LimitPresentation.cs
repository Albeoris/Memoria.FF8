namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private sealed record LimitPresentationScope(BattleLimitFamily Family, bool WeaponWasHidden, bool SuppressEquipment);
    private readonly Dictionary<BattleModelOwner, LimitPresentationScope> limitPresentationScopes = [];

    private NativeBattleLimitPresentationBinding? ReadLimitPresentationBinding(BattleModelOwner owner)
    {
        BattleLimitFamily? family = services?.ReadActiveLimitFamily?.Invoke(owner);
        if (family is null || owner is not BattlePartyActorModel)
        {
            return null;
        }
        string actor = RequireCanonicalGraphicsActorId(owner);
        return resources.NativeGraphics?.LimitPresentations.SingleOrDefault(binding => binding.ActorId == actor && binding.Family == family);
    }

    internal void InitializeRestoredLimitPresentationScopes()
    {
        if (NativeGraphics is not { } graphics)
        {
            return;
        }
        foreach (BattleModelOwner owner in graphics.ActorModels.Owners)
        {
            if (ReadLimitPresentationBinding(owner) is { } binding)
            {
                limitPresentationScopes[owner] = new(binding.Family, false, binding.Resource.EquipmentPresentationRole == NativeBattleEquipmentPresentationRole.FreeHands || binding.Family == BattleLimitFamily.Duel);
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(owner, graphics.ActorModels.Get(owner) with { EquipmentPresentationRole = binding.Resource.EquipmentPresentationRole }) };
            }
        }
        NativeGraphics = graphics;
    }

    private NativeBattleGraphicsState SynchronizeLimitPresentationScopes(NativeBattleGraphicsState graphics)
    {
        foreach (BattleModelOwner owner in graphics.ActorModels.Owners)
        {
            graphics = SynchronizeLimitPresentationScope(graphics, owner);
        }
        return graphics;
    }

    private NativeBattleGraphicsState SynchronizeLimitPresentationScope(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        NativeBattleLimitPresentationBinding? binding = ReadLimitPresentationBinding(owner);
        limitPresentationScopes.TryGetValue(owner, out LimitPresentationScope? previous);
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
        if (binding?.Family == previous?.Family)
        {
            model = model with { EquipmentPresentationRole = binding?.Resource.EquipmentPresentationRole ?? NativeBattleEquipmentPresentationRole.Equipped };
            if (previous?.SuppressEquipment == true)
            {
                model = model with { Flags0 = (ushort)(model.Flags0 | 0x200) };
            }
            return graphics with { ActorModels = graphics.ActorModels.Set(owner, model) };
        }
        if (previous?.SuppressEquipment == true)
        {
            model = model with { Flags0 = previous.WeaponWasHidden ? (ushort)(model.Flags0 | 0x200) : (ushort)(model.Flags0 & ~0x200) };
        }
        if (binding is null)
        {
            limitPresentationScopes.Remove(owner);
        }
        else
        {
            limitPresentationScopes[owner] = new(binding.Family, (model.Flags0 & 0x200) != 0, binding.Resource.EquipmentPresentationRole == NativeBattleEquipmentPresentationRole.FreeHands || binding.Family == BattleLimitFamily.Duel);
        }
        if ((model.Flags0 & 2) == 0)
        {
            return graphics with { ActorModels = graphics.ActorModels.Set(owner, model) };
        }
        NativeBattleGraphicsResource resource = binding?.Resource ?? ReadOriginalPresentationModelResource(owner) ?? throw new InvalidDataException("A Limit presentation scope lost its standing actor resource.");
        NativeBattlePoseSource body = resource.PoseSource ?? throw new InvalidDataException("A Limit presentation scope requires its recipient skeleton and motion.");
        byte standing = NativeStandingSequence(model.Flags8 ?? throw new InvalidDataException("A Limit presentation scope requires its current actor statuses."));
        NativeBattleSequenceState state = (model.SequenceState ?? new()).InitializeControl5027D0(standing, 0) with
        {
            Pose = 0,
            PoseFrame114 = 1,
            PoseLimit115 = resource.AnimationFrames[0],
            DecodedPose = body.Sample(0, 1, 0),
            LinkedPose = resource.LinkedPoseSource is null ? null : new(0, 1, resource.LinkedAnimationFrames[0], 0),
            LinkedDecodedPose = resource.LinkedPoseSource?.Sample(0, 1, 0)
        };
        model = model with
        {
            Flags0 = binding is not null && (binding.Family == BattleLimitFamily.Duel || binding.Resource.EquipmentPresentationRole == NativeBattleEquipmentPresentationRole.FreeHands) ? (ushort)(model.Flags0 | 0x200) : model.Flags0,
            EquipmentPresentationRole = resource.EquipmentPresentationRole,
            Flags2 = (ushort)((model.Flags2 ?? 0) & ~8),
            Sequence = standing,
            SequenceFrame = 0,
            ResourceFlags44 = 1,
            SequenceState = state,
            ActiveResourceRow100 = model.OriginalResourceRow104 ?? model.ActiveResourceRow100,
            ActiveStagedResourceRow100 = null,
            LinkedStagedResourceRow = null,
            ResourceMagicId = null,
            ResourceRenzokukenTargetBank = null,
            ActiveAnimationBank = null,
            ActiveAnimationDirectory = null,
            LinkedContactOffset = null
        };
        model = NativeBattleBodyBoneMatrices.Publish508F60(model, body, resource.LinkedPoseSource);
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, body, ReadNativeModelScaleVector502AB0(graphics, model)) };
        return graphics with { ActorModels = graphics.ActorModels.Set(owner, model) };
    }
}
