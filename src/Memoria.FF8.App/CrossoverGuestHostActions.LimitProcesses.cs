using System.Collections.Immutable;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions : IFf7BattleGuestProcess, IFf7BattleGuestPresentationOnly
{
    private BattleNativeDuelState? mixedDuel;
    private BattleNativeShotState? mixedShot;
    private BattleNativeSlotsRoll? mixedSlots;
    private BattleNativeAction? processAction;
    private BattleNativeAction? nextProcessAction;
    private byte? pendingDuelFinisher;
    private ushort processTarget;
    private bool processActionQueued;
    private bool mixedSlotsAccepted;
    private int mixedSlotsRemaining;
    private bool mixedProcessActive;
    private bool angelWing;
    private bool angelWingCast;
    private bool angelWingFallback;
    public string? ProcessFailure { get; private set; }
    public void ClearProcessFailure() => ProcessFailure = null;
    private byte? sourceShotFlags;
    private byte sourceBattleFlags;
    private ushort processButtons;
    private ushort previousProcessButtons;
    private long processClock;
    private long processRemainder;

    public bool ProcessActive => mixedProcessActive;
    public bool ProcessOwnsReadyTurn => angelWing;
    public BattleNativeDuelState? DuelProcess => mixedDuel;
    public BattleNativeShotState? ShotProcess => mixedShot;
    public BattleNativeSlotsRoll? SlotsProcess => mixedSlots;
    public string ProcessSlotsName => mixedSlots is { } slots ? assets.NativeActions.Single(action => action.Command == 16 && action.ActionId == slots.MagicId).Name ?? "Slots" : "Slots";
    public string? SlotsCastUnsupportedReason
    {
        get
        {
            if (mixedSlots is not { } slots)
            {
                return null;
            }
            BattleNativeAction action = assets.NativeActions.Single(action => action.Command == 16 && action.ActionId == slots.MagicId);
            return ProcessActionUnsupportedReason(action);
        }
    }
    public bool SlotsChoosing => mixedProcessActive && mixedSlots is not null && !mixedSlotsAccepted;
    public IReadOnlyList<BattleNativeDuelChoice> DuelProcessChoices => mixedDuel is { Phase: BattleNativeDuelPhase.Ready } duel ? NativeBattleDuelRules.Choices(kit.Limit, duel.MoveSetId, originLimitProgress!.DuelFlags, assets.NativeActions) : [];
    private bool IsProcessInvocation(ushort ability) => ProcessAbilities(ReadNativeAbilities()).Any(choice => choice.Key == ability && choice.NativeAction is null && choice.Command is 11 or 14 or 16 or 19);

    public void BindLimitProcessOwners(byte? shotFlags, byte battleFlags)
    {
        sourceShotFlags = shotFlags;
        sourceBattleFlags = battleFlags;
        capabilityVersion++;
    }

    public void SetProcessButtons(ushort buttons)
    {
        processButtons = buttons;
    }

    private IReadOnlyList<CrossoverGuestAbility> ProcessAbilities(IReadOnlyList<CrossoverGuestAbility> original)
    {
        List<CrossoverGuestAbility> result = original.ToList();
        if (limitSelection is { Crisis: > 0 } selection && kit.Limit.Command == 14)
        {
            result.RemoveAll(choice => choice.Command == 14);
            foreach (BattleNativeAction shot in assets.NativeActions.Where(action => action.Command == 14 && action.ActionId < 8))
            {
                bool ammo = encounter.Items?.Slots.Any(stock => stock.ItemId == shot.ActionId + 101 && stock.Count != 0) == true;
                if (ammo || sourceShotFlags is byte learned && (learned & (1 << shot.ActionId)) != 0)
                {
                    result.Add(new(CrossoverGuestAbilityCatalog.Key(14, shot.ActionId), 14, "Shot", shot.Name ?? "Shot", null, 1, null));
                }
            }
        }
        if (limitSelection is { Crisis: > 0 } rinoa && kit.Limit.Command == 19)
        {
            result.RemoveAll(choice => choice.Command == 19);
            if ((sourceBattleFlags & 0x10) == 0 && originLimitProgress is { } learned)
            {
                ushort id = NativeBattleLimitRules.AngeloActionId(rinoa.Crisis, (byte)(learned.AngeloFlags >> 4));
                result.Add(new(CrossoverGuestAbilityCatalog.Key(19, id), 19, "Limit", kit.Limit.InvocationChoices[0].Name, null, 1, null));
            }
            if ((sourceBattleFlags & 0x20) != 0)
            {
                result.Add(new(CrossoverGuestAbilityCatalog.Key(19, 4), 19, "Limit", kit.Limit.InvocationChoices[1].Name, null, 1, null));
            }
        }
        if (processAction is { } action)
        {
            ushort key = ProcessKey(action);
            result.RemoveAll(choice => choice.Key == key);
            result.Add(new(key, action.Command, "Limit", action.Name ?? "Limit", action, 1, null));
        }
        return result;
    }

    private string? ProcessInvocationUnsupportedReason(CrossoverGuestAbility choice)
    {
        if (mixedProcessActive)
        {
            return "Another native Limit process already owns this actor.";
        }
        if (originLimitProgress is null || limitSelection is not { Crisis: > 0 })
        {
            return "The native Limit selection is no longer available.";
        }
        if (choice.Command == 11 && (limitOptions is null || (limitOptions & 2) != 0 && limitUiRandom is null))
        {
            return "Duel requires its source input options and automatic input random owner.";
        }
        if (choice.Command == 14 && (encounter.Items?.Slots.Any(stock => stock.ItemId == 101 + (choice.Key & 255) && stock.Count != 0) != true))
        {
            return "No ammunition remains for this Shot selection.";
        }
        return null;
    }

    private string StartProcess(ushort ability, int target)
    {
        CrossoverGuestAbility choice = ProcessAbilities(ReadNativeAbilities()).Single(choice => choice.Key == ability);
        if (ProcessInvocationUnsupportedReason(choice) is { } reason)
        {
            return reason;
        }
        byte crisis = limitSelection!.Crisis;
        retainedProcessCrisis = crisis;
        processTarget = checked((ushort)(1 << target));
        mixedProcessActive = true;
        processClock = host.Tick;
        processRemainder = 0;
        processActionQueued = false;
        previousProcessButtons = processButtons;
        if (choice.Command == 11)
        {
            byte moveSet = NativeBattleDuelRules.InitialMoveSet(kit.Limit, crisis);
            short timer = NativeBattleDuelRules.Timer(kit.Limit, crisis);
            mixedDuel = new(moveSet, processTarget, timer, timer, BattleNativeDuelPhase.Opening) { Ui = new(BattleTimedInputUiPhase.Opening) };
            SelectProcessAction(241, NativeBattleDuelRules.ActionId(kit.Limit, moveSet));
        }
        else if (choice.Command == 14)
        {
            byte id = checked((byte)(ability & 255));
            short timer = checked((short)(4 * kit.Limit.ShotTimers[crisis - 1]));
            byte ammo = encounter.Items!.Slots.First(stock => stock.ItemId == 101 + id && stock.Count != 0).Count;
            mixedShot = new(id, processTarget, timer, timer, ammo, BattleNativeShotPhase.Opening) { Ui = new(BattleTimedInputUiPhase.Opening) };
            SelectProcessAction(14, id);
        }
        else if (choice.Command == 16)
        {
            RerollProcessSlots();
        }
        else
        {
            SelectProcessAction(19, checked((byte)(ability & 255)));
        }
        limitSelection = limitSelection with { Crisis = 0, ReadyTick = null };
        capabilityVersion++;
        return choice.Name;
    }

    private void SelectProcessAction(byte command, ushort id)
    {
        BattleNativeAction action = assets.NativeActions.Single(action => action.Command == command && action.ActionId == id && (action.CrisisLevel is null || action.CrisisLevel == retainedProcessCrisis));
        QueueProcessAction(action, command is 14 or 238);
    }

    private static ushort ProcessKey(BattleNativeAction action) => action.ActionId <= byte.MaxValue ? CrossoverGuestAbilityCatalog.Key(action.Command, action.ActionId) : action.ActionId == 65530 ? (ushort)0xBFFE : action.ActionId == 65532 ? (ushort)0xBFFF : throw new NotSupportedException("The source Limit control has no invocation identifier.");

    private void QueueProcessAction(BattleNativeAction action, bool presentationOnly = false)
    {
        if (presentationOnly || action.AttackType == 0 && (action.Statuses1 & ~0x2000000u) == 0 && action.Statuses0 == 0)
        {
            action = action with { PresentationOnly = true };
        }
        if (processActionQueued)
        {
            nextProcessAction = action;
            return;
        }
        processAction = action;
        processActionQueued = false;
        capabilityVersion++;
    }

    public void RerollProcessSlots()
    {
        if (!mixedProcessActive || kit.Limit.Command != 16 || mixedSlotsAccepted)
        {
            throw new InvalidOperationException("Do over requires the active native Slots selection.");
        }
        byte Next()
        {
            limitSelection = limitSelection! with { Random = limitSelection!.Random.Draw(out byte value) };
            return value;
        }
        byte crisis = limitSelection!.Crisis;
        if (crisis == 0)
        {
            crisis = retainedProcessCrisis;
        }
        mixedSlots = NativeBattleSlotsRules.Roll(kit.Limit, kit.Level, crisis, Next(), Next(), Next(), Next());
        mixedSlotsRemaining = mixedSlots.Repetitions;
        capabilityVersion++;
    }

    private byte retainedProcessCrisis;

    public void AcceptProcessSlots()
    {
        if (!SlotsChoosing)
        {
            return;
        }
        if (SlotsCastUnsupportedReason is { } unsupported)
        {
            ProcessFailure = unsupported;
            return;
        }
        mixedSlotsAccepted = true;
        SelectProcessAction(16, mixedSlots!.MagicId);
    }

    public void FinishProcessShot()
    {
        if (mixedShot is not { Phase: BattleNativeShotPhase.Ready or BattleNativeShotPhase.AwaitingEffect } shot)
        {
            return;
        }
        mixedShot = shot with { RemainingTimer = -1, Phase = BattleNativeShotPhase.Ending, Ui = new(BattleTimedInputUiPhase.Closing) };
        if (!processActionQueued)
        {
            SelectProcessAction(238, shot.ActionId);
        }
    }

    public void AdvanceProcess(long hostTick)
    {
        if (!mixedProcessActive)
        {
            return;
        }
        long elapsed = hostTick - processClock;
        if (elapsed < 0)
        {
            throw new InvalidDataException("The native Limit input clock moved backwards.");
        }
        processClock = hostTick;
        int rate = mixedShot is null ? 60 : 30;
        processRemainder += elapsed * rate;
        int frames = checked((int)(processRemainder / Ff7BattleSession.LogicUpdatesPerSecond));
        processRemainder %= Ff7BattleSession.LogicUpdatesPerSecond;
        ushort pulse = (ushort)(processButtons & ~previousProcessButtons);
        if (frames != 0)
        {
            previousProcessButtons = processButtons;
        }
        for (int frame = 0; frame < frames; frame++)
        {
            if (mixedDuel is { } duel)
            {
                mixedDuel = duel with { Ui = (duel.Ui ?? new(BattleTimedInputUiPhase.Active)).Advance(60) };
                if (duel.Phase != BattleNativeDuelPhase.Ready)
                {
                    continue;
                }
                duel = mixedDuel with { RemainingTimer = (short)(duel.RemainingTimer - 1), AutomaticCountdown = (short)(duel.AutomaticCountdown - 1) };
                mixedDuel = duel;
                if (duel.RemainingTimer <= 0)
                {
                    mixedDuel = duel with { Phase = BattleNativeDuelPhase.Finisher, Ui = new(BattleTimedInputUiPhase.Closing) };
                    QueueProcessAction(NativeBattleDuelRules.Control(65532, 0), true);
                    continue;
                }
                IReadOnlyList<BattleNativeDuelChoice> choices = DuelProcessChoices;
                BattleNativeDuelChoice? selected = null;
                if ((limitOptions!.Value & 2) != 0)
                {
                    if (duel.AutomaticCountdown == 30)
                    {
                        BattleNativeDuelChoice[] learned = choices.Where(choice => choice.Learned).ToArray();
                        selected = choices[0];
                        if (learned.Length != 0)
                        {
                            limitUiRandom = limitUiRandom!.Draw(out ushort roll);
                            int index = roll / (0x8000 / learned.Length);
                            if (index >= learned.Length)
                            {
                                throw new NotSupportedException("Native automatic Duel selected an uninitialized fourth stack entry.");
                            }
                            selected = learned[index];
                        }
                    }
                }
                else if (frame == 0 && pulse != 0)
                {
                    ushort[] ring = duel.InputRing.ToArray();
                    byte cursor = (byte)((duel.InputCursor + 1) & 7);
                    ring[cursor] = pulse;
                    mixedDuel = duel with { InputRing = ring.ToImmutableArray(), InputCursor = cursor };
                    selected = choices.FirstOrDefault(choice => NativeBattleDuelRules.Matches(choice, mixedDuel.InputRing, cursor));
                }
                if (selected is not null)
                {
                    BattleNativeAction selectedAction = assets.NativeActions.Single(action => action.Command == (selected.Finisher ? 239 : 241) && action.ActionId == selected.ActionId);
                    if (ProcessActionUnsupportedReason(selectedAction) is { } unsupported)
                    {
                        ProcessFailure = unsupported;
                        continue;
                    }
                    mixedDuel = mixedDuel! with { MoveSetId = selected.MoveSetId, Phase = selected.Finisher ? BattleNativeDuelPhase.Finisher : BattleNativeDuelPhase.AwaitingEffect };
                    if (selected.Finisher)
                    {
                        pendingDuelFinisher = selected.ActionId;
                        QueueProcessAction(NativeBattleDuelRules.Control(65530, selected.ActionId), true);
                    }
                    else
                    {
                        SelectProcessAction(241, selected.ActionId);
                    }
                }
            }
            if (mixedShot is { Phase: BattleNativeShotPhase.Ready or BattleNativeShotPhase.AwaitingEffect } shot)
            {
                mixedShot = shot with { RemainingTimer = (short)(shot.RemainingTimer - 1), Ui = (shot.Ui ?? new(BattleTimedInputUiPhase.Active)).Advance(60) };
                if (mixedShot.RemainingTimer <= 0 || frame == 0 && (pulse & 0x10) != 0)
                {
                    FinishProcessShot();
                }
                else if (shot.Phase == BattleNativeShotPhase.Ready && frame == 0 && (pulse & 8) != 0)
                {
                    TriggerProcessShot();
                }
            }
        }
    }

    public void TriggerProcessShot()
    {
        if (mixedShot is not { Phase: BattleNativeShotPhase.Ready, RemainingAmmo: > 0, RemainingTimer: > 0 } shot)
        {
            return;
        }
        mixedShot = shot with { Phase = BattleNativeShotPhase.AwaitingEffect };
        SelectProcessAction(237, shot.ActionId);
    }

    public Ff7BattleGuestProcessAction? PollProcess(long hostTick)
    {
        if (!mixedProcessActive && angelWing)
        {
            BeginAngelWingTurn();
        }
        if (!mixedProcessActive)
        {
            return null;
        }
        if (angelWingFallback)
        {
            if (processActionQueued)
            {
                return null;
            }
            processActionQueued = true;
            return new(0, processTarget) { Command = Ff7BattleCommand.Attack };
        }
        if (processActionQueued)
        {
            CancelProcess();
            return null;
        }
        if (processAction is not { } action)
        {
            return null;
        }
        ushort targets = SelectProcessTargets(action);
        if (targets == 0)
        {
            CancelProcess();
            return null;
        }
        processActionQueued = true;
        return new(ProcessKey(action), targets, System.Numerics.BitOperations.PopCount((uint)targets) > 1);
    }

    public string? DuelProcessChoiceUnsupportedReason(BattleNativeDuelChoice choice)
    {
        BattleNativeAction action = assets.NativeActions.Single(action => action.Command == (choice.Finisher ? 239 : 241) && action.ActionId == choice.ActionId);
        return ProcessActionUnsupportedReason(action);
    }

    private string? ProcessActionUnsupportedReason(BattleNativeAction action)
    {
        BattleNativeAction mechanics = action with { Statuses1 = action.Statuses1 & ~0x2000000u };
        if (CrossoverBattleAbilityRules.UnsupportedReason(mechanics) is { } unsupported)
        {
            return unsupported;
        }
        byte flags = action.TargetFlags.GetValueOrDefault();
        int target = NativeBattleLimitRules.IsFixedAllyTarget(flags) ? ActorSlot : host.Actors.FirstOrDefault(actor => AbilityTargetIsLegal(flags, actor.Actor, host.GetOriginSavedStatuses(actor.Actor.Slot)) && ((flags & 0x30) == 0x20 || (actor.Actor.Slot >= 4) == ((flags & 0x40) != 0)))?.Actor.Slot ?? -1;
        return target < 0 ? "No valid target is available for this native Limit phase." : PresentationUnsupportedReason(action, ProcessKey(action), target);
    }

    private ushort SelectProcessTargets(BattleNativeAction action)
    {
        byte flags = action.TargetFlags.GetValueOrDefault();
        if (NativeBattleLimitRules.IsFixedAllyTarget(flags))
        {
            return checked((ushort)(1 << ActorSlot));
        }
        if (action.PresentationOnly)
        {
            return processTarget;
        }
        int target = System.Numerics.BitOperations.TrailingZeroCount((uint)processTarget);
        bool automatic = mixedSlots is not null || angelWingCast || mixedDuel is { Phase: BattleNativeDuelPhase.Finisher } && (flags & 0x30) != 0x10;
        if (!automatic)
        {
            return AbilityTargetMask(action, target);
        }
        bool enemy = (flags & 0x40) != 0;
        var candidates = host.Actors.Where(actor => (flags & 0x30) == 0x20 || (actor.Actor.Slot >= 4) == enemy).Where(actor => AbilityTargetIsLegal(flags, actor.Actor, host.GetOriginSavedStatuses(actor.Actor.Slot))).ToArray();
        if (candidates.Length == 0)
        {
            return 0;
        }
        if ((flags & 0x30) != 0x10)
        {
            return checked((ushort)candidates.Aggregate(0, (mask, actor) => mask | (1 << actor.Actor.Slot)));
        }
        int selected = ProcessRandomByte() % candidates.Length;
        if (mixedSlots is not null && !enemy && action.AttackType is 3 or 4 or 21 or 25 or 32)
        {
            return checked((ushort)(1 << candidates.OrderBy(actor => actor.Actor.HitPoints).ThenBy(actor => actor.Actor.Slot == candidates[selected].Actor.Slot ? 0 : 1).ThenBy(actor => actor.Actor.Slot).First().Actor.Slot));
        }
        return checked((ushort)(1 << candidates[selected].Actor.Slot));
    }

    public void ProcessActionCompleted(ushort ability)
    {
        if (angelWingFallback && ability == 0)
        {
            CancelProcess();
            return;
        }
        BattleNativeAction action = processAction ?? throw new InvalidDataException("The native Limit process lost its source phase.");
        if (ability != ProcessKey(action))
        {
            throw new InvalidDataException("The completed action belongs to a different native Limit phase.");
        }
        processAction = nextProcessAction;
        nextProcessAction = null;
        processActionQueued = false;
        if (mixedDuel is { } duel)
        {
            if (action.ActionId == 65530 && pendingDuelFinisher is byte finisher)
            {
                pendingDuelFinisher = null;
                SelectProcessAction(239, finisher);
            }
            else if (action.ActionId == 65532 || action.Command == 239)
            {
                CancelProcess();
            }
            else if (processAction is null && duel.Phase != BattleNativeDuelPhase.Ready)
            {
                throw new InvalidDataException("The native Duel phase completed before its source invocation acknowledgement.");
            }
        }
        else if (mixedShot is { } shot)
        {
            if (action.Command == 238)
            {
                CancelProcess();
            }
            else if (shot.Phase == BattleNativeShotPhase.Ending || shot.RemainingAmmo == 0)
            {
                mixedShot = shot with { Phase = BattleNativeShotPhase.Ending };
                SelectProcessAction(238, shot.ActionId);
            }
            else
            {
                mixedShot = shot with { Phase = BattleNativeShotPhase.Ready, Ui = new(BattleTimedInputUiPhase.Active) };
            }
        }
        else if (mixedSlots is { } slots && --mixedSlotsRemaining > 0)
        {
            SelectProcessAction(16, slots.MagicId);
        }
        else
        {
            CancelProcess();
        }
        capabilityVersion++;
    }

    public void ApplyProcessOperation(ushort ability, int targetSlot, Ff7BattleGuestProcessOperation operation)
    {
        if (!ProcessActive || processAction is not { } action || ProcessKey(action) != ability || targetSlot != ActorSlot || operation.SourceBattleStatuses != (action.Statuses1 & 0x2000000) || operation.StatusAttack != action.StatusAttack || !operation.Supported)
        {
            throw new InvalidDataException("The source execution status contradicts its active native Limit receipt.");
        }
        angelWing = true;
    }

    public void CancelProcess()
    {
        mixedProcessActive = false;
        mixedDuel = null;
        mixedShot = null;
        mixedSlots = null;
        mixedSlotsAccepted = false;
        angelWingCast = false;
        angelWingFallback = false;
        processAction = null;
        nextProcessAction = null;
        pendingDuelFinisher = null;
        processActionQueued = false;
        ProcessFailure = null;
        capabilityVersion++;
    }

    private void AcknowledgeProcessInvocation()
    {
        if (mixedDuel is { Phase: BattleNativeDuelPhase.Opening or BattleNativeDuelPhase.AwaitingEffect } duel && processAction?.Command == 241 && processAction.ActionId <= byte.MaxValue)
        {
            mixedDuel = duel with { Phase = BattleNativeDuelPhase.Ready, InputRing = ImmutableArray.Create<ushort>(0, 0, 0, 0, 0, 0, 0, 0), InputCursor = 0, AutomaticCountdown = 120, PendingPulse = 0, Ui = new(BattleTimedInputUiPhase.Active) };
        }
        if (mixedShot is { Phase: BattleNativeShotPhase.Opening or BattleNativeShotPhase.AwaitingEffect } shot && processAction?.Command is 14 or 237)
        {
            mixedShot = shot with { Phase = BattleNativeShotPhase.Ready, Ui = new(BattleTimedInputUiPhase.Active) };
        }
    }

    private byte ProcessRandomByte()
    {
        limitSelection = limitSelection! with { Random = limitSelection!.Random.Draw(out byte result) };
        return result;
    }

    private void BeginAngelWingTurn()
    {
        NativeCharacterVitals owner = encounter.Working;
        bool logical = owner.MagicSystem.SharedMagicStock || owner.MagicSystem.RecordZeroExtension.Length != 0 && kit.RecordRow == 0;
        NativeMagicStock[] inventory = logical ? owner.GetMagicInventory(kit.RecordRow).ToArray() : owner.Records[kit.RecordRow].MagicInventory;
        bool Eligible(NativeMagicStock stock) => stock.SpellId != 0 && assets.NativeActions.Any(action => action.Command == 2 && action.ActionId == stock.SpellId && (action.TargetFlags.GetValueOrDefault() & 0x40) != 0) && MagicJunctionPolicy.CanAccess(owner, kit.RecordRow, stock.SpellId);
        int[] enemies = host.Actors.Where(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints > 0 && (actor.Actor.Conditions & 8) != 0).Select(actor => actor.Actor.Slot).ToArray();
        if (enemies.Length == 0)
        {
            return;
        }
        mixedProcessActive = true;
        processClock = host.Tick;
        processRemainder = 0;
        if (!inventory.Any(Eligible))
        {
            angelWingFallback = true;
            processTarget = checked((ushort)(1 << enemies[ProcessRandomByte() % enemies.Length]));
            return;
        }
        int slot = logical ? ProcessRandomByte() % inventory.Length : ProcessRandomByte() & 31;
        while (!Eligible(inventory[slot]))
        {
            slot = (slot + 1) % inventory.Length;
        }
        SelectProcessAction(2, inventory[slot].SpellId);
        angelWingCast = true;
        processTarget = checked((ushort)(1 << enemies[ProcessRandomByte() % enemies.Length]));
    }

    private void CommitProcessCost(BattleNativeAction action, int target)
    {
        if (mixedShot is not { } shot || action.Command != 237)
        {
            return;
        }
        ushort ammo = checked((ushort)(101 + shot.ActionId));
        Ff7BattleGuestItemSelection selection = new(host.RequestId, host.Tick, ActorSlot, target, ammo);
        encounter.Items = encounter.Items!.Reserve(selection).Complete(ActorSlot, target, ammo, false);
        mixedShot = shot with { RemainingAmmo = checked((byte)(shot.RemainingAmmo - 1)) };
    }

    public bool IsPresentationOnly(ushort ability) => processAction is { PresentationOnly: true } action && ProcessKey(action) == ability;

    public void BeginPresentationOnly(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
    {
        BattleNativeAction action = NativeAbility(invocation.GuestAbility);
        if (!action.PresentationOnly || Invocation is not null || attack.Visual is not { } visual)
        {
            throw new InvalidOperationException("The native presentation-only phase has no ready source owner.");
        }
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        NativeBattleContactHalf contact = NativeBattleContactHalf.FromScratch(target.Slot == ActorSlot ? (byte)0 : (byte)3, 0, 0, 0, 0, 0, 0);
        CrossoverNativeMagicPresentationState presentation = fireProgram.Begin(visual.Model with { Index = 0 }, target.Slot, encounter.CrtRandom, contact, action, encounter.GlobalRandom, CreateArena(placement));
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        fire = new(invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, placement, target.CollisionRadius, new(CrossoverGunbladeHostAttack.ProjectPhysicalSource(kit), new(0, 0, 0), 0), null, presentation, false);
    }
}
