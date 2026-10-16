namespace Memoria.FF8.Runtime;

public sealed record BattleNativeRenzokukenInvocation(byte CrisisLevel);
public sealed record BattleNativeRenzokukenState(ushort MagicId, ushort TargetMask, byte? FinisherActionId, BattleNativeRenzokukenInputState Input)
{
    public int PoseFrames { get; init; }
    public byte UiPhase { get; init; }
    public long UiSubframeTicks { get; init; }
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

    public bool CanTriggerNativeRenzokuken => nativeRenzokukenProcesses.Count != 0 && (RequireNativeRenzokukenOptions() & 1) == 0 && nativeRenzokukenProcesses.Single().Value.Input.Phase != 8;
    public bool NativeRenzokukenBarVisible => nativeRenzokukenProcesses.Count != 0 && (RequireNativeRenzokukenOptions() & 0x8001) == 0 && RenzokukenResource(nativeRenzokukenProcesses.Single().Value.MagicId).ShowBar != 0;

    public bool NativeRenzokukenTriggerWindowOpen
    {
        get
        {
            if (!CanTriggerNativeRenzokuken)
            {
                return false;
            }
            BattleNativeRenzokukenState state = nativeRenzokukenProcesses.Single().Value;
            BattleNativeRenzokukenResource resource = RenzokukenResource(state.MagicId);
            int distance = state.Input.PoseClock + state.Input.SubframeClock - resource.Timeline.InputThresholds[state.Input.ContactIndex];
            return state.Input.TriggerTiming == 0 && NativeGunbladeAttack.TriggerSucceeded(checked((short)(distance + 5)));
        }
    }

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
        byte targetFlags = actor.NativeLimit!.InvocationChoices.Single().TargetFlags;
        ushort mask = NativeBattleLimitRules.PlayerTargetMask(targetFlags, target.NativeIndex);
        OnNativePlayerCommandSubmitted(actor, 5);
        BattleAttackCommand command = new(actor.Id, target.Id, BattleAbility.LimitBreak, 5 << 16) { NativeQueuedCommand = 5, NativeTargetMask = mask, NativeRenzokuken = new(crisis) };
        EnqueueNativePacket(actor, command, actor.NativeLimit.InvocationChoices.Single().Name, playerInput: true);
    }

    private void StartNativeRenzokuken(BattleAttackCommand queued)
    {
        BattleCombatant actor = GetActor(queued.ActorId);
        byte crisis = queued.NativeRenzokuken!.CrisisLevel;
        BattleCombatant? target = ResolveNativeActionMask(queued.NativeTargetMask).FirstOrDefault();
        if (target is null || !CanNativeBattleActorAct(actor))
        {
            actor.IsBusy = false;
            nativeLimitLevels.Remove(actor.Id);
            Log("Cancelled", actor.Id, queued.TargetId, 0, "Renzokuken has no available original target.");
            return;
        }
        if (target.NativeRenzokukenMagicIds.Length != 7)
        {
            throw new NotSupportedException("Retargeted Renzokuken requires its actual target resource selectors.");
        }
        ushort magicId = target.NativeRenzokukenMagicIds[crisis + NextRandomByte() % 2 + 1];
        BattleNativeRenzokukenResource resource = RenzokukenResource(magicId);
        byte? finisher = null;
        if (NativeBattleRenzokukenRules.HasFinisher(crisis, checked((byte)NextRandomByte())))
        {
            finisher = NativeBattleRenzokukenRules.Finisher(actor.NativePlayerStatRules!.WeaponRecord[2], checked((byte)(NextRandomByte() % 4)));
            _ = FindInstalledNativeAction(249, finisher.Value);
        }
        ushort mask = checked((ushort)(1 << target.NativeIndex));
        ushort finisherTargets = mask;
        if (finisher is byte selected)
        {
            BattleNativeAction action = FindInstalledNativeAction(249, selected);
            ushort computed = NativeAutomaticActionTargetMask(action.TargetFlags!.Value);
            finisherTargets = (computed & 0x8000) != 0 ? computed : mask;
            nativeActionScratch = nativeActionScratch with { FinisherActionIndexE2E = selected };
        }
        BattleActionPresentation original = new(actor.Id, target.Id, BattleActionPhase.Windup, 1, BattleAbility.LimitBreak, finisher is null ? 0xFFFD : 0xFFFE) { NativeQueuedCommand = 5, NativeTargetMask = mask, NativeSequence = 21, NativeRenzokuken = queued.NativeRenzokuken };
        nativeActionScratch = nativeActionScratch with { FinisherPendingE03 = finisher is not null };
        BeginNativeTimedAction(original, magicId, finisher, finisherTargets);
        nativeRenzokukenProcesses.Add(actor.Id, new(magicId, mask, finisher, new()));
        Log("Renzokuken", actor.Id, target.Id, resource.Timeline.InputThresholds.Length, $"{actor.Name}: Renzokuken.");
    }

    public bool TriggerNativeRenzokuken()
    {
        if (!CanTriggerNativeRenzokuken)
        {
            return false;
        }
        (string actorId, BattleNativeRenzokukenState state) = nativeRenzokukenProcesses.Single();
        nativeRenzokukenProcesses[actorId] = state with { PendingTrigger = true };
        return NativeRenzokukenTriggerWindowOpen;
    }

    public void AdvanceNativeRenzokukenUi(bool poseAdvanced, bool triggerPressed = false)
    {
        if (nativeRenzokukenProcesses.Count == 0)
        {
            return;
        }
        (string actorId, BattleNativeRenzokukenState state) = nativeRenzokukenProcesses.Single();
        BattleNativeRenzokukenResource resource = RenzokukenResource(state.MagicId);
        BattleNativeRenzokukenInputStep step = NativeBattleRenzokukenRules.AdvanceInput(resource.Timeline, state.Input, poseAdvanced, triggerPressed || state.PendingTrigger, (RequireNativeRenzokukenOptions() & 1) != 0, nativeBattleUiRandom);
        nativeBattleUiRandom = step.Random;
        state = state with { Input = step.State, PendingTrigger = false, PoseFrames = state.PoseFrames + (poseAdvanced ? 1 : 0) };
        nativeRenzokukenProcesses[actorId] = state;
        if (step.Contact is { } contact)
        {
            BattleCombatant actor = GetActor(actorId);
            BattleNativeAction physical = actor.NativeAttack! with { Command = 251, DispatchCommand = 5, ActionId = 0, AttackType = 10, Power = actor.NativePlayerStatRules!.CharacterRecord[33], HitCount = 1, AttackFlags = 0, DamagePresentation = 4, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0 };
            ResolveNativeTimedContact(state.TargetMask, physical, contact.TriggerTiming, contact.Index == contact.Total && state.FinisherActionId is null);
        }
        if (state.Input.Phase == 8 && state.PoseFrames >= resource.Timeline.TotalPoseFrames)
        {
            nativeRenzokukenProcesses.Remove(actorId);
            nativeLimitLevels.Remove(actorId);
            CompleteNativeTimedAction();
            GetActor(actorId).IsBusy = actions.Any(command => command.ActorId == actorId);
            CompleteBattleIfNeeded();
        }
    }

    public void AdvanceNativeRenzokukenPresentation(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(1000))
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        if (nativeRenzokukenProcesses.Count == 0)
        {
            return;
        }
        (string actorId, BattleNativeRenzokukenState state) = nativeRenzokukenProcesses.Single();
        //TODO(TD-009): The original widgetBYTE33 phase currently projects onto four120Hz callbacks per30Hz pose. Use the shared native graphics scheduler when it owns these service callbacks
        long ticks = state.UiSubframeTicks + elapsed.Ticks * 120;
        int callbacks = checked((int)(ticks / TimeSpan.TicksPerSecond));
        nativeRenzokukenProcesses[actorId] = state with { UiSubframeTicks = ticks % TimeSpan.TicksPerSecond };
        for (int callback = 0; callback < callbacks; callback++)
        {
            if (!nativeRenzokukenProcesses.TryGetValue(actorId, out BattleNativeRenzokukenState? current))
            {
                break;
            }
            byte phase = (byte)((current.UiPhase + 1) & 3);
            nativeRenzokukenProcesses[actorId] = current with { UiPhase = phase };
            AdvanceNativeRenzokukenUi(phase == 0);
        }
    }

    private void RestoreNativeRenzokuken(string actorId, byte crisis, BattleNativeRenzokukenState state)
    {
        BattleCombatant actor = GetActor(actorId);
        BattleNativeRenzokukenResource resource = RenzokukenResource(state.MagicId);
        if (actor.NativeLimit?.Command != 5 || crisis == 0 || state.Input is null || state.Input.ContactIndex > resource.Timeline.InputThresholds.Length || state.Input.Phase is not (0 or 1 or 2 or 3 or 4 or 6 or 8) || state.Input.SubframeClock is < 0 or > 3 || state.Input.PoseClock < 0 || state.Input.PoseClock % 4 != 0 || state.Input.TriggerTiming != 0 && state.Input.TriggerTiming != 1 && state.Input.TriggerTiming < 20 || (state.Input.Phase == 8) != (state.Input.ContactIndex == resource.Timeline.InputThresholds.Length) || state.PoseFrames < 0 || state.PoseFrames > resource.Timeline.TotalPoseFrames || state.UiPhase > 3 || state.UiSubframeTicks is < 0 or >= TimeSpan.TicksPerSecond || actor.NativeAttack is null || actor.NativePlayerStatRules is null)
        {
            throw new InvalidDataException("Saved Renzokuken input contradicts its original actor or resource.");
        }
        _ = RequireNativeRenzokukenOptions();
        if (state.FinisherActionId is byte finisher)
        {
            _ = FindInstalledNativeAction(249, finisher);
        }
        nativeRenzokukenProcesses.Add(actorId, state);
        actor.IsBusy = true;
    }

    private void ValidateNativeRenzokukenOwners()
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
        if (timed.Command.ActorId != actorId || timed.Command.NativeRenzokuken?.CrisisLevel != nativeLimitLevels.GetValueOrDefault(actorId) || timed.Contacts != process.Input.ContactIndex || timed.Command.NativeTargetMask != process.TargetMask)
        {
            throw new InvalidDataException("Saved Renzokuken contact count or target differs from its durable transaction.");
        }
    }
}
