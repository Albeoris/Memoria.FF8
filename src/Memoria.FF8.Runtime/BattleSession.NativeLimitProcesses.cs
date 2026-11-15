namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private IEnumerable<BattleNativeAction> GetNativeRinoaLimitActions(BattleCombatant actor, byte crisis)
    {
        if ((definition.NativeSavedBattleFlagsKnownMask & 0x30) != 0x30)
        {
            throw new NotSupportedException("Native Rinoa Limit menu requires the saved1CFE97A bits0x10 and0x20.");
        }
        byte flags = CurrentNativeSavedBattleFlags();
        BattleNativeLimitDefinition limit = actor.NativeLimit!;
        if (limit.InvocationChoices.IsDefault || limit.InvocationChoices.Length != 2)
        {
            throw new InvalidDataException("Native Rinoa Limit menu requires both original kernel24 records.");
        }
        if ((flags & 0x10) == 0 && (!UsesExpandedRoster || HasExpandedCombine(actor)))
        {
            NativeLimitProgress progress = definition.LimitProgress ?? throw new NotSupportedException("Combine requires the actual saved Angelo learned flags.");
            progress.Validate();
            ushort id = NativeBattleLimitRules.AngeloActionId(crisis, (byte)(progress.AngeloFlags >> 4));
            yield return FindInstalledNativeAction(19, id) with { Name = limit.InvocationChoices[0].Name };
        }
        if ((flags & 0x20) != 0 && (!UsesExpandedRoster || HasExpandedAngelWing(actor)))
        {
            yield return FindInstalledNativeAction(19, 4) with { Name = limit.InvocationChoices[1].Name };
        }
    }

    private static BattleCombatant NativePlayerSelectedTarget(BattleCombatant actor, BattleCombatant selected, byte targetFlags)
    {
        //4AB190 starts the ally cursor at its owner;4AA1D0 disables navigation for target mode2/3.
        return NativeBattleLimitRules.IsFixedAllyTarget(targetFlags) ? actor : selected;
    }

    private BattleTargetRequest CreatePlayerTargetRequest(BattleCombatant actor, BattleCombatant target, byte targetFlags)
    {
        BattleCombatant selected = NativePlayerSelectedTarget(actor, target, targetFlags);
        if (!UsesExpandedRoster)
        {
            return NativeBattleTargetRequestAdapter.CreatePlayerRequest(targetFlags, selected.EncodeNativeSlot());
        }

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
        return new(selection, BattleTargetEligibility.Living, (targetFlags & 1) != 0, BattleTargetReplacement.UseCurrentBattlePolicy);
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
        if (!UsesExpandedRoster)
        {
            NativeBattleTargetRequestAdapter.OriginalWordProjection original = selection is BattleTargetSelection.SideGroup group
                ? NativeBattleTargetRequestAdapter.OriginalGroup(group.Side)
                : new(originalPreflight ?? throw new InvalidDataException("The automatic action requires its reached original target operand."));
            return NativeBattleTargetRequestAdapter.CreateOriginalRequest(original with { Random = (targetFlags & 2) != 0 }, BattleTargetEligibility.SavedStatus, allowsKnockedOutAllies);
        }

        if ((targetFlags & 2) != 0)
        {
            selection = NativeBattleTargetRequestAdapter.RandomizeOriginalSelection(selection, samplesOriginalSlots: false);
        }
        return new(selection, BattleTargetEligibility.SavedStatus, allowsKnockedOutAllies, BattleTargetReplacement.UseCurrentBattlePolicy);
    }
}
