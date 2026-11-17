using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly Dictionary<string, BattleNativeSlotsRoll> nativeSlotsRolls = new(StringComparer.Ordinal);

    public BattleNativeSlotsRoll? GetNativeSlotsRoll(string actorId)
    {
        _ = GetActor(actorId);
        return nativeSlotsRolls.GetValueOrDefault(actorId);
    }

    public BattleNativeSlotsRoll RollNativeSlots(string actorId)
    {
        RequireNoPendingSlotsDeal();
        BattleCombatant actor = GetActor(actorId);
        byte crisis = GetNativeLimitLevel(actorId);
        if (Outcome != BattleOutcome.Active || !actor.IsPlayer || !actor.IsReady || actor.NativeLimit is not { Command: 16 } rules || crisis == 0)
        {
            throw new InvalidOperationException("Slots roll requires Selphie's ready owned Limit selection.");
        }
        if (rules.SlotsSelectionBytes.IsDefaultOrEmpty || rules.SlotsSpellBytes.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native Slots roll requires its actual contiguous kernel26/27 tables.");
        }
        BattleNativeSlotsRoll roll = GenerateNativeSlotsRoll(rules, actor.Level, crisis);
        nativeSlotsRolls[actorId] = roll;
        return roll;
    }

    private BattleNativeSlotsRoll GenerateNativeSlotsRoll(BattleNativeLimitDefinition rules, int level, byte crisis)
    {
        BattleNativeSlotsRoll roll = NativeBattleSlotsRules.Roll(rules, level, crisis, (byte)NextRandomByte(), (byte)NextRandomByte(), (byte)NextRandomByte(), (byte)NextRandomByte());
        _ = FindInstalledNativeAction(16, roll.MagicId);
        return roll;
    }

    private static bool SupportsSlotsAction(BattleNativeAction action)
    {
        return action is { Command: 16, KernelSection: 1, HitCount: > 0, TargetFlags: byte flags, PresentationOnly: false, CrisisLevel: null } &&
            (flags & 0x30) != 0x30;
    }

    private bool CanCastSlots(BattleNativeAction action, BattleNativeSlotsRoll roll)
    {
        return SupportsSlotsAction(action) && roll.Repetitions is >= 1 and <= NativeBattleSlotsRules.MaxQueuedRepetitions && roll.MagicId == action.ActionId &&
            (NativeBattleLimitLearningRules.SlotsMask(action) == 0 || definition.LimitProgress?.SpecialMagicFlags is not null);
    }

    private void QueueSlots(BattleCombatant actor, BattleNativeAction action, byte sequence, BattleNativeSlotsRoll roll)
    {
        if (!CanCastSlots(action, roll) || roll.PreflightSelection is not null)
        {
            throw new NotSupportedException("Slots requires its installed spell, supported repetition count and any applicable saved learning flags.");
        }
        ValidateNativeActionContract(action);
        byte nativeSequence = NativeBattleActionGraphics.ResolvePlayerSequence(16, sequence, action.ActionId, definition.NativeCommandSequences);
        byte flags = action.TargetFlags!.Value;
        BattleSide side = (flags & 0x40) == 0 ? BattleSide.Allies : BattleSide.Enemies;
        BattleTargetSide sideGroup = (flags & 0x30) == 0x20 ? BattleTargetSide.Both :
            side == BattleSide.Allies ? BattleTargetSide.Allies : BattleTargetSide.Enemies;
        BattleTargetRequest group = new(new BattleTargetSelection.SideGroup(sideGroup), BattleTargetEligibility.SavedStatus, (flags & 1) != 0, BattleTargetReplacement.UseCurrentBattlePolicy);
        BattleCombatant anchor = ResolveBattleTargets(group, sampleRandom: false).FirstOrDefault() ??
            throw new InvalidOperationException("Slots has no eligible occupied target on its installed side.");
        bool single = (flags & 0x30) == 0x10;
        ImmutableArray<BattleMagicCastInvocation>.Builder casts = ImmutableArray.CreateBuilder<BattleMagicCastInvocation>(roll.Repetitions);
        NativeBattleRandomState before = random;
        try
        {
            for (int index = 0; index < roll.Repetitions; index++)
            {
                BattleCombatant selected = single ? ReadNativeRandomActionTarget(side) ?? anchor : anchor;
                BattleActorReference reference = TargetActorReference(selected);
                BattleTargetSelection preflight = new BattleTargetSelection.Explicit([reference]);
                BattleTargetRequest targets = single ? new(preflight, group.Eligibility, group.AllowsKnockedOutAllies, group.Replacement) : group;
                if ((flags & 2) != 0)
                {
                    BattleSide randomSide = sideGroup == BattleTargetSide.Both ? BattleSide.Enemies : side;
                    targets = new(new BattleTargetSelection.RandomOnSide(randomSide, samplesOriginalSlots: true), group.Eligibility, group.AllowsKnockedOutAllies, group.Replacement);
                }
                casts.Add(new(targets, preflight, reference));
            }
        }
        catch
        {
            random = before;
            throw;
        }

        ImmutableArray<BattleMagicCastInvocation> invocations = casts.MoveToImmutable();
        BattleMagicCastInvocation first = invocations[0];
        OnNativePlayerCommandSubmitted(actor, 16);
        actions.Enqueue(new(actor.Id, first.FallbackAnchor.ActorId, first.Targets, BattleAbility.LimitBreak, 16 << 16 | action.ActionId)
        {
            NativeQueuedCommand = 16,
            NativeAction = action,
            NativeSlots = roll with { PreflightSelection = single ? first.PreflightSelection : null },
            NativeSequence = nativeSequence,
            MagicCasts = invocations
        });
        actor.Atb = 0;
        actor.IsBusy = true;
        nativeLimitLevels.Remove(actor.Id);
        nativeSlotsRolls.Remove(actor.Id);
        Log("Queued", actor.Id, first.FallbackAnchor.ActorId, 0, $"{actor.Name}: {action.Name} queued.");
    }

    private void ValidateSlotsGroup(BattleCombatant actor, string targetId, BattleAbility ability, int actionId, ImmutableArray<BattleMagicCastInvocation> casts, int mode, BattleNativeAction? action, BattleNativeSlotsRoll roll)
    {
        if (ability != BattleAbility.LimitBreak || actor.NativeLimit?.Command != 16 || action is null || !CanCastSlots(action, roll) ||
            actionId != (16 << 16 | action.ActionId) || casts.IsDefault || casts.Length != roll.Repetitions || mode != 0 || casts[0]?.FallbackAnchor.ActorId != targetId)
        {
            throw new InvalidDataException("Saved Slots lacks its accepted installed spell and repeated target requests.");
        }
        byte flags = action.TargetFlags!.Value;
        bool single = (flags & 0x30) == 0x10;
        BattleSide side = (flags & 0x40) == 0 ? BattleSide.Allies : BattleSide.Enemies;
        BattleTargetSide sideGroup = (flags & 0x30) == 0x20 ? BattleTargetSide.Both :
            side == BattleSide.Allies ? BattleTargetSide.Allies : BattleTargetSide.Enemies;
        BattleSide randomSide = sideGroup == BattleTargetSide.Both ? BattleSide.Enemies : side;
        if (single ? roll.PreflightSelection is not BattleTargetSelection.Explicit { Actors: [var first] } || first != casts[0].FallbackAnchor : roll.PreflightSelection is not null)
        {
            throw new InvalidDataException("Saved Slots contradicts its first admitted target selection.");
        }
        foreach (BattleMagicCastInvocation cast in casts)
        {
            if (cast is null || cast.PreflightSelection is not BattleTargetSelection.Explicit { Actors: [var selected] } || selected != cast.FallbackAnchor ||
                FindTargetActor(selected) is not { } target || sideGroup != BattleTargetSide.Both && target.Side != side ||
                cast.Targets.Eligibility != BattleTargetEligibility.SavedStatus || cast.Targets.AllowsKnockedOutAllies != ((flags & 1) != 0) ||
                cast.Targets.Replacement != BattleTargetReplacement.UseCurrentBattlePolicy || cast.Targets.DeathCallbackSelf is not null)
            {
                throw new InvalidDataException("Saved Slots cast contradicts its installed targeting contract.");
            }
            bool valid = (flags & 2) != 0 ? cast.Targets.Selection is BattleTargetSelection.RandomOnSide { SamplesOriginalSlots: true } randomTarget && randomTarget.Side == randomSide :
                single ? cast.Targets.Selection is BattleTargetSelection.Explicit { Actors: [var exact] } && exact == selected :
                cast.Targets.Selection is BattleTargetSelection.SideGroup group && group.Side == sideGroup;
            if (!valid)
            {
                throw new InvalidDataException("Saved Slots cast changed its installed target mode.");
            }
        }
    }

    private BattleTargetRequest SelectSlotsTargets(BattleNativeAction action, BattleMagicCastInvocation invocation)
    {
        BattleTargetRequest original = invocation.Targets;
        if (original.Selection is not BattleTargetSelection.Explicit || FindTargetActor(invocation.FallbackAnchor) is not { } preferred)
        {
            return original;
        }
        bool players = preferred.IsPlayer;
        bool healing = players && action.AttackType is 3 or 4 or 21 or 25 or 32;
        bool statuses = action.AttackType is not (3 or 4 or 5 or 6 or 13 or 16 or 21 or 25 or 32) && (action.Statuses0 != 0 || action.Statuses1 != 0);
        bool damaging = !players && action.AttackType is 2 or 8 or 11 or 15 or 20 or 22 or 26 or 33;
        if (!healing && !statuses && !damaging || nativeScriptState.DisableAutomaticTargetReplacement && !IsBattleTargetEligible(preferred, original))
        {
            return original;
        }
        BattleCombatant? target = Roster(preferred.Side).Where(candidate => candidate.Slot is not null && IsBattleTargetEligible(candidate, original))
            .OrderBy(candidate => damaging && ModifyNativeElement(candidate, action, 100, false) < 0 ? 1 : 0)
            .ThenBy(candidate => statuses ? NativeSlotsStatusPriority(candidate, action, !players) : 0)
            .ThenBy(candidate => healing ? candidate.CurrentHp : 0)
            .ThenBy(candidate => candidate == preferred ? 0 : 1)
            .ThenBy(candidate => candidate.Slot!.Value.Position)
            .FirstOrDefault();
        return target is null ? original : new(new BattleTargetSelection.Explicit([TargetActorReference(target)]), original.Eligibility, original.AllowsKnockedOutAllies, original.Replacement);
    }

    private static int NativeSlotsStatusPriority(BattleCombatant candidate, BattleNativeAction action, bool considerImmunity)
    {
        if (considerImmunity && action.StatusAttack != 255 && !NativeSlotsHasSusceptibleStatus(candidate, action.Statuses0, action.Statuses1))
        {
            return 2;
        }
        ushort missingSaved = (ushort)(action.Statuses0 & ~NativeSavedStatusBits(candidate) & 0x7F);
        uint missingBattle = action.Statuses1 & ~NativeBattleStatusBits(candidate);
        if (missingSaved == 0 && missingBattle == 0)
        {
            return 1;
        }
        if (!considerImmunity || action.StatusAttack == 255)
        {
            return 0;
        }
        return NativeSlotsHasSusceptibleStatus(candidate, missingSaved, missingBattle) ? 0 : 2;
    }

    private static bool NativeSlotsHasSusceptibleStatus(BattleCombatant candidate, ushort saved, uint battle)
    {
        for (int bit = 0; bit < 40; bit++)
        {
            bool included = bit < 7 ? (saved & (1 << bit)) != 0 : bit >= 8 && (battle & (1u << (bit - 8))) != 0;
            if (included && GetNativeStatusResistance(candidate, bit) < 200)
            {
                return true;
            }
        }
        return false;
    }
}
