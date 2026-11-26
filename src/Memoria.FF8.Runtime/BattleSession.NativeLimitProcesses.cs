namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private IEnumerable<BattleNativeAction> GetNativeRinoaLimitActions(BattleCombatant actor, byte crisis)
    {
        byte requiredFlags = actor.ActiveNativeLimitFamily switch { BattleLimitFamily.Combine => 0x10, BattleLimitFamily.AngelWing => 0x20, _ => 0 };
        if ((definition.NativeSavedBattleFlagsKnownMask & requiredFlags) != requiredFlags)
        {
            throw new NotSupportedException("The selected Rinoa Limit family requires its actual saved story flag.");
        }
        byte flags = CurrentNativeSavedBattleFlags();
        BattleNativeLimitDefinition limit = actor.NativeLimit!;
        if (limit.InvocationChoices.IsDefault || limit.InvocationChoices.Length != 2)
        {
            throw new InvalidDataException("Native Rinoa Limit menu requires both original kernel24 records.");
        }
        if ((actor.ActiveNativeLimitFamily is null or BattleLimitFamily.Combine) && (flags & 0x10) == 0 && HasExpandedCombine(actor))
        {
            NativeLimitProgress progress = definition.LimitProgress ?? throw new NotSupportedException("Combine requires the actual saved Angelo learned flags.");
            progress.Validate();
            byte learned = (byte)(progress.AngeloFlags >> 4);
            ushort id = NativeBattleLimitRules.AngeloActionId(crisis, learned);
            BattleNativeAction? finisher = FindInstalledNativeAction(19, id);
            if (IsUnavailableInvincibilityFinisher(finisher))
            {
                learned = (byte)(learned & ~(1 << id));
                finisher = (learned & ((1 << crisis) - 1)) != 0 ? FindInstalledNativeAction(19, NativeBattleLimitRules.AngeloActionId(crisis, learned)) : null;
            }
            if (finisher is not null)
            {
                yield return finisher with { Name = limit.InvocationChoices[0].Name };
            }
        }
        if ((actor.ActiveNativeLimitFamily is null or BattleLimitFamily.AngelWing) && (flags & 0x20) != 0 && HasExpandedAngelWing(actor))
        {
            yield return FindInstalledNativeAction(19, 4) with { Name = limit.InvocationChoices[1].Name };
        }
    }

    private static BattleCombatant NativePlayerSelectedTarget(BattleCombatant actor, BattleCombatant selected, byte targetFlags)
    {
        //4AB190 starts the ally cursor at its owner;4AA1D0 disables navigation for target mode2/3.
        return NativeBattleLimitRules.IsFixedAllyTarget(targetFlags) ? actor : selected;
    }

    private BattleTargetRequest CreatePlayerTargetRequest(BattleCombatant actor, BattleCombatant target, byte targetFlags, BattleTargetReplacement replacement = BattleTargetReplacement.UseCurrentBattlePolicy)
    {
        BattleCombatant selected = NativePlayerSelectedTarget(actor, target, targetFlags);

        BattleTargetSide side = selected.IsPlayer ? BattleTargetSide.Allies : BattleTargetSide.Enemies;
        BattleTargetSelection selection = (targetFlags & 0x30) switch
        {
            0 => new BattleTargetSelection.SideGroup(side),
            0x10 => new BattleTargetSelection.Explicit([TargetActorReference(selected)]),
            0x20 => new BattleTargetSelection.SideGroup(BattleTargetSide.Both),
            _ => throw new NotSupportedException("Original player target flags request an unresolved target mode.")
        };
        if ((targetFlags & 2) != 0)
        {
            selection = new BattleTargetSelection.RandomOnSide(selected.Side);
        }
        return new(selection, BattleTargetEligibility.Living, (targetFlags & 1) != 0, replacement);
    }

    private BattleTargetRequest CreateAutomaticTargetRequest(byte targetFlags) => CreateAutomaticTargetRequest(targetFlags, out _);

    private BattleTargetRequest CreateAutomaticTargetRequest(byte targetFlags, out NativeBattleTargetRequestAdapter.DeferredOperands? originalPreflight)
    {
        BattleSide side = (targetFlags & 0x40) == 0 ? BattleSide.Allies : BattleSide.Enemies;
        BattleTargetSide targetSide = side == BattleSide.Allies ? BattleTargetSide.Allies : BattleTargetSide.Enemies;
        BattleTargetSelection selection = (targetFlags & 0x30) switch
        {
            0 => new BattleTargetSelection.SideGroup(targetSide),
            0x10 => ChooseOriginalRandomActionTarget(side),
            0x20 => new BattleTargetSelection.SideGroup(BattleTargetSide.Both),
            _ => throw new NotSupportedException("Native automatic Limit target mode has no original483880 branch.")
        };
        originalPreflight = selection as NativeBattleTargetRequestAdapter.DeferredOperands;
        bool allowsKnockedOutAllies = (targetFlags & 1) != 0;

        if ((targetFlags & 2) != 0)
        {
            selection = NativeBattleTargetRequestAdapter.RandomizeOriginalSelection(selection, samplesOriginalSlots: false);
        }
        return new(selection, BattleTargetEligibility.SavedStatus, allowsKnockedOutAllies, BattleTargetReplacement.UseCurrentBattlePolicy);
    }
}
