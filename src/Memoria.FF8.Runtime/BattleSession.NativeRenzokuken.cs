namespace Memoria.FF8.Runtime;

public sealed record BattleNativeRenzokukenInvocation(byte CrisisLevel);
public sealed record BattleNativeRenzokukenState(ushort MagicId, BattleTargetRequest TargetRequest, byte? FinisherActionId, BattleNativeRenzokukenInputState Input)
{
    public int PoseFrames { get; init; }
    public bool PendingTrigger { get; init; }
}

public sealed partial class BattleSession
{
    private readonly Dictionary<string, BattleNativeRenzokukenState> nativeRenzokukenProcesses = new(StringComparer.Ordinal);

    public BattleNativeRenzokukenState? GetNativeRenzokuken(string actorId)
    {
        _ = GetActor(actorId);
        return nativeRenzokukenProcesses.GetValueOrDefault(actorId);
    }

    public bool CanTriggerNativeRenzokuken => Presentation is { } process && CanTriggerExpandedHostedRenzokuken(process);
    public bool NativeRenzokukenBarVisible => Presentation is { } process && CaptureExpandedHostedRenzokukenFrame() is { } frame &&
        process.TriggerWidget is { Visible: true } widget && widget.Actor == frame.Owner.Actor && widget.SourceMagicId == frame.Owner.MagicId;
    public bool NativeRenzokukenTriggerWindowOpen => CanTriggerNativeRenzokuken && IsNativeTriggerWindowOpen(Presentation!.TriggerWidget!);

    private ushort RequireNativeRenzokukenOptions()
    {
        return definition.LimitProgress?.Options978 ?? throw new NotSupportedException("Renzokuken requires the actual saved native options WORD1CFE978.");
    }

    private BattleNativeRenzokukenResource RenzokukenResource(ushort magicId)
    {
        return definition.NativeRenzokukenResources.SingleOrDefault(resource => resource.MagicId == magicId) ?? throw new NotSupportedException($"Native Renzokuken resource {magicId} has no original effect-service projection.");
    }

    private void QueueNativeRenzokuken(BattleCombatant actor, BattleCombatant target, byte crisis)
    {
        ushort options = RequireNativeRenzokukenOptions();
        if ((options & 1) != 0 && nativeBattleUiRandom is null)
        {
            throw new NotSupportedException("Automatic Renzokuken requires its actual battle UI random state.");
        }
        if (actor.NativePlayerStatRules is not { } rules || actor.NativeAttack is null || rules.CharacterRecord.Length != 36 || rules.WeaponRecord.Length != 12)
        {
            throw new NotSupportedException("Renzokuken requires its character power and equipped weapon finisher flags.");
        }
        if (nativeShotProcesses.Count != 0 || nativeDuelProcesses.Count != 0 || nativeRenzokukenProcesses.Count != 0)
        {
            throw new InvalidOperationException("A native timed Limit already owns the battle menu.");
        }
        if (target.NativeRenzokukenMagicIds.Length != 7)
        {
            throw new NotSupportedException("Renzokuken requires the selected target's original information+336 resource selectors.");
        }
        _ = RenzokukenResource(target.NativeRenzokukenMagicIds[crisis + 1]);
        _ = RenzokukenResource(target.NativeRenzokukenMagicIds[crisis + 2]);
            if (!IsExpandedRenzokukenTarget(target) || HasQueuedExpandedTimedLimit)
            {
                throw new InvalidOperationException("Renzokuken requires one available enemy and no queued timed Limit.");
            }
            for (byte index = 0; index < 4; index++)
            {
                if ((rules.WeaponRecord[2] & (1 << index)) != 0)
                {
                    ValidateExpandedRenzokukenFinisher(FindInstalledNativeAction(249, index));
                }
            }
            OnNativePlayerCommandSubmitted(actor, 5);
            actions.Enqueue(new(actor.Id, target.Id, SelectBattleTarget(target, eligibility: BattleTargetEligibility.Living), BattleAbility.LimitBreak, 5 << 16) { NativeQueuedCommand = 5, NativeRenzokuken = new(crisis) });
            actor.Atb = 0;
            actor.IsBusy = true;
            Log("Queued", actor.Id, target.Id, 0, $"{actor.Name}: Renzokuken queued.");
            return;
    }

    private (ushort Magic, byte? Finisher) SelectNativeRenzokukenResource(BattleCombatant actor, BattleCombatant target, byte crisis)
    {
        if (target.NativeRenzokukenMagicIds.Length != 7)
        {
            throw new NotSupportedException("Retargeted Renzokuken requires its actual target resource selectors.");
        }
        ushort magicId = target.NativeRenzokukenMagicIds[crisis + NextRandomByte() % 2 + 1];
        _ = RenzokukenResource(magicId);
        byte? finisher = null;
        if (NativeBattleRenzokukenRules.HasFinisher(crisis, checked((byte)NextRandomByte())))
        {
            finisher = NativeBattleRenzokukenRules.Finisher(actor.NativePlayerStatRules!.WeaponRecord[2], checked((byte)(NextRandomByte() % 4)));
            _ = FindInstalledNativeAction(249, finisher.Value);
        }
        return (magicId, finisher);
    }

    public bool TriggerNativeRenzokuken()
    {
        return CanTriggerNativeRenzokuken && TriggerGunblade();
    }

    private void RestoreNativeRenzokuken(string actorId, byte crisis, BattleNativeRenzokukenState state, ExpandedHostedEvent? hostedEvent)
    {
        BattleCombatant actor = GetActor(actorId);
        BattleNativeRenzokukenResource resource = RenzokukenResource(state.MagicId);
        if (actor.NativeLimit?.Command != 5 || crisis == 0 || state.Input is null || state.Input.ContactIndex > resource.Timeline.InputThresholds.Length || state.Input.Phase is not (0 or 1 or 2 or 3 or 4 or 6 or 8) || state.Input.SubframeClock is < 0 or > 3 || state.Input.PoseClock < 0 || state.Input.PoseClock % 4 != 0 || state.Input.TriggerTiming != 0 && state.Input.TriggerTiming != 1 && state.Input.TriggerTiming < 20 || (state.Input.Phase == 8) != (state.Input.ContactIndex == resource.Timeline.InputThresholds.Length) || state.PoseFrames < 0 || !OwnsHostedRenzokukenRestore(hostedEvent, actorId) && state.PoseFrames > resource.Timeline.TotalPoseFrames || actor.NativeAttack is null || actor.NativePlayerStatRules is null)
        {
            throw new InvalidDataException("Saved Renzokuken input contradicts its original actor or resource.");
        }
        _ = RequireNativeRenzokukenOptions();
        if (state.TargetRequest is null || state.TargetRequest.Selection is not BattleTargetSelection.Explicit { Actors.Length: 1 })
        {
            throw new InvalidDataException("Saved Renzokuken requires its invocation target selection.");
        }
        if (state.FinisherActionId is byte finisher)
        {
            _ = FindInstalledNativeAction(249, finisher);
        }
        nativeRenzokukenProcesses.Add(actorId, state);
        actor.IsBusy = true;
    }

    private void ValidateNativeRenzokukenOwners(ExpandedHostedEvent? hostedEvent, NativeBattleTriggerReceipt? hostedTrigger)
    {
        foreach (BattleAttackCommand queued in actions.Where(command => command.NativeRenzokuken is not null))
        {
            if (queued.NativeQueuedCommand != 5 || queued.Ability != BattleAbility.LimitBreak || queued.ActionId != 5 << 16 || queued.NativeAction is not null || queued.NativeGraphic is not null || queued.NativeRenzokuken!.CrisisLevel != nativeLimitLevels.GetValueOrDefault(queued.ActorId))
            {
                throw new InvalidDataException("Saved Renzokuken control packet disagrees with its selected crisis or metadata ownership.");
            }
        }
        if (nativeRenzokukenProcesses.Count == 0)
        {
            if (nativeTimedAction is not null)
            {
                throw new InvalidDataException("Saved native timed transaction lacks its Renzokuken process.");
            }
            return;
        }
        (string actorId, BattleNativeRenzokukenState process) = nativeRenzokukenProcesses.Single();
        NativeBattleTimedAction timed = nativeTimedAction ?? throw new InvalidDataException("Saved Renzokuken process lacks its durable command transaction.");
        bool hosted = OwnsHostedRenzokukenRestore(hostedEvent, actorId);
        if ((ActiveAction is not null && !hosted || timed.Command.NativeRenzokuken is not { } invocation || GetActor(timed.Command.TargetId) is not { IsPlayer: false } target || !target.IsActive && !hosted ||
            target.NativeRenzokukenMagicIds.Length != 7 || process.MagicId != target.NativeRenzokukenMagicIds[invocation.CrisisLevel + 1] && process.MagicId != target.NativeRenzokukenMagicIds[invocation.CrisisLevel + 2] ||
            process.FinisherActionId is byte finisher && (GetActor(actorId).NativePlayerStatRules!.WeaponRecord[2] & (1 << finisher)) == 0))
        {
            throw new InvalidDataException("Saved expanded Renzokuken disagrees with its installed target selectors or equipped finishers.");
        }
        NativeBattleTriggerReceipt? candidateTrigger = hostedTrigger;
        int pending = candidateTrigger is { } receipt && receipt.Actor == CreatePresentationModelOwner(GetActor(actorId)) && receipt.ContactIndex == process.Input.ContactIndex && receipt.ContactIndex == timed.Contacts + 1 ? 1 : 0;
        if (timed.Command.ActorId != actorId || timed.Command.NativeRenzokuken?.CrisisLevel != nativeLimitLevels.GetValueOrDefault(actorId) || timed.Contacts + pending != process.Input.ContactIndex || timed.Command.TargetRequest != process.TargetRequest)
        {
            throw new InvalidDataException("Saved Renzokuken contact count or target differs from its durable transaction.");
        }
    }

    private static bool IsExpandedRenzokukenTarget(BattleCombatant target)
    {
        return !target.IsPlayer && target.IsActive && target.IsAlive && !target.Untargetable;
    }

}
