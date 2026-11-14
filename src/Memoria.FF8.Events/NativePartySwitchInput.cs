using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public static class NativePartySwitchInput
{
    public const int EmptySlot = 255;

    public static void Validate(PlayerAction action)
    {
        if (action.Kind != PlayerActionKind.SwitchPartyMembers || !IsSwitchSelection(action.Value) || action.Quantity is not int target || !IsSwitchSelection(target) || action.Text is not null || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0 || action.WorldButtons != 0 || action.WaitWhilePlayable)
        {
            throw new ArgumentException("Switch input requires two explicit character or vacant slot selections and one ordinary menu action.", nameof(action));
        }
    }

    private static bool IsSwitchSelection(int row) => (uint)row < NativeCharacterVitals.ExtendedRecordCount || row == EmptySlot;

    public static bool CanExchange(NativeCharacterVitals vitals, int source, int target, byte entrySelection, int capacity = 3)
    {
        if ((entrySelection & 2) == 0 || vitals.DreamEnabled)
        {
            return false;
        }
        if (capacity > 3)
        {
            return SelectExpandedParty(vitals, source, target, capacity) is not null;
        }
        if (source == EmptySlot || target == EmptySlot)
        {
            int member = source == EmptySlot ? target : source;
            return vitals.ExtendedRosterEnabled && NativeCharacterVitals.IsExtendedRosterCharacter(member) && IsSelectableMember(vitals, member) && (vitals.Records[member].MemberFlags & 6) == 0 && !vitals.CombatParty.Contains((byte)member) && vitals.CombatParty.Contains((byte)EmptySlot) && SelectedPartyIsUsable(vitals, SelectParty(vitals, source, target), allowEmptySlots: true);
        }
        if (!IsSelectableMember(vitals, source) || !IsSelectableMember(vitals, target))
        {
            return false;
        }
        bool sourceParty = vitals.CombatParty.Contains((byte)source);
        bool targetParty = vitals.CombatParty.Contains((byte)target);
        //4CBE0A checks pane identity before applying either member's lock.
        if (sourceParty != targetParty && ((vitals.Records[source].MemberFlags | vitals.Records[target].MemberFlags) & 6) != 0)
        {
            return false;
        }
        //4CBFFE..4CC047 refuses closing an incomplete or entirely incapacitated party.
        bool extendedExchange = vitals.ExtendedRosterEnabled && (NativeCharacterVitals.IsExtendedRosterCharacter(source) || NativeCharacterVitals.IsExtendedRosterCharacter(target));
        return SelectedPartyIsUsable(vitals, SelectParty(vitals, source, target), allowEmptySlots: extendedExchange);
    }

    private static bool IsSelectableMember(NativeCharacterVitals vitals, int row) => vitals.IsRosterMember(row) && vitals.Records[row].CharacterId == row;

    private static bool SelectedPartyIsUsable(NativeCharacterVitals vitals, byte[] selected, bool allowEmptySlots)
    {
        return (allowEmptySlots || !selected.Contains((byte)EmptySlot)) && selected.Any(row => row != EmptySlot && (vitals.Records[row].Status & 1) == 0);
    }

    private static byte[] SelectParty(NativeCharacterVitals vitals, int source, int target)
    {
        if (source == EmptySlot || target == EmptySlot)
        {
            byte[] party = vitals.CombatParty.ToArray();
            party[Array.IndexOf(party, (byte)EmptySlot)] = (byte)(source == EmptySlot ? target : source);
            return party;
        }
        return vitals.CombatParty.Select(row => row == source ? (byte)target : row == target ? (byte)source : row).ToArray();
    }

    private static byte[]? SelectExpandedParty(NativeCharacterVitals vitals, int source, int target, int capacity)
    {
        if (source == target || source != EmptySlot && !IsSelectableMember(vitals, source) || target != EmptySlot && !IsSelectableMember(vitals, target))
        {
            return null;
        }
        byte[] party = vitals.GetBattlePartyRows(capacity);
        if (source == EmptySlot || target == EmptySlot)
        {
            byte member = (byte)(source == EmptySlot ? target : source);
            if ((vitals.Records[member].MemberFlags & 6) != 0)
            {
                return null;
            }
            if (party.Contains(member))
            {
                party = party.Where(row => row != member).ToArray();
            }
            else if (party.Length < capacity)
            {
                party = [.. party, member];
            }
            else
            {
                return null;
            }
        }
        else
        {
            bool sourceParty = party.Contains((byte)source);
            bool targetParty = party.Contains((byte)target);
            if (!sourceParty && !targetParty || sourceParty != targetParty && ((vitals.Records[source].MemberFlags | vitals.Records[target].MemberFlags) & 6) != 0)
            {
                return null;
            }
            party = party.Select(row => row == source ? (byte)target : row == target ? (byte)source : row).ToArray();
        }
        return party.Any(row => (vitals.Records[row].Status & 1) == 0) ? party : null;
    }

    public static NativeCharacterVitals Apply(NativeCharacterVitals vitals, PlayerAction action, byte entrySelection, int capacity = 3)
    {
        Validate(action);
        vitals.Validate();
        int target = action.Quantity!.Value;
        if (!CanExchange(vitals, action.Value, target, entrySelection, capacity))
        {
            throw new InvalidOperationException("The current menu does not permit this party-member exchange.");
        }
        if (capacity > 3)
        {
            return vitals.WithBattleParty(SelectExpandedParty(vitals, action.Value, target, capacity)!, capacity);
        }
        byte[] party = SelectParty(vitals, action.Value, target);
        //4CC46D publishes the selected combat rows;495EF0 rebuilds their stats.
        return (vitals with { CombatParty = party }).RebuildParty();
    }
}

public sealed partial class EventEngine
{
    public Func<int>? ReadBattlePartyCapacity { get; set; }

    private static bool HasExtendedCombatParty(NativeCharacterVitals vitals) => vitals.ExtendedRosterEnabled && !vitals.DreamEnabled && vitals.CombatParty.Any(row => NativeCharacterVitals.IsExtendedRosterCharacter(row));

    public void ConfigureExtendedRoster(bool enabled)
    {
        if (services.CharacterVitals is not { } current || current.ExtendedRosterEnabled == enabled || partyMenu is { Kind: NativeFieldMenuKind.PartySelection or NativeFieldMenuKind.GuestSelection } || Scene.DrawPointInteraction is not null)
        {
            return;
        }
        NativeCharacterVitals next = current.WithExtendedRoster(enabled);
        services.SetCharacterVitals(next);
        if (partyMenu is { Kind: NativeFieldMenuKind.Normal, TemporaryCombatParty: not null } menu)
        {
            partyMenu = menu with { TemporaryCombatParty = next.CombatParty.ToArray(), CombatPartyIndependent = menu.CombatPartyIndependent || menu.NativeReturnResult == 1 && !current.CombatParty.SequenceEqual(next.CombatParty) };
        }
    }

    private bool ApplyPartySwitchMenuAction(PlayerAction action)
    {
        NativePartySwitchInput.Validate(action);
        bool activeMenu = partyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };
        if (!activeMenu && (partyMenu is not null || !CanAcceptFieldMovement || CurrentDialogue is not null || !Scene.MenuEnabled))
        {
            return false;
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || !HasCanonicalPartySlots(Scene) || !services.CanStoreNativeEventOwners)
        {
            throw new NotSupportedException("Switch requires verified native field slots and their publication service.");
        }
        NativeCharacterVitals source = services.CharacterVitals ?? throw new NotSupportedException("Switch requires the actual saved character owner.");
        byte entry = activeMenu ? partyMenu!.NativeEntrySelection : services.WorldControls?.Menu209 ?? 0;
        int capacity = ReadBattlePartyCapacity?.Invoke() ?? 3;
        if (!NativePartySwitchInput.CanExchange(source, action.Value, action.Quantity!.Value, entry, capacity))
        {
            return false;
        }
        NativeCharacterVitals next = NativePartySwitchInput.Apply(source, action, entry, capacity);
        // Extended combat exchanges preserve the field's authored actors and story party.
        bool unchangedNativeParty = capacity > 3 && source.CombatParty.SequenceEqual(next.CombatParty);
        bool combatOnly = HasExtendedCombatParty(source) || HasExtendedCombatParty(next);
        byte[]? field = combatOnly || unchangedNativeParty ? null : source.PreparePartyMenuFieldSlots(Scene.Party, next.CombatParty);
        if (field is not null && !activeMenu && (services.WorldControls!.EventFlags104 & 0x800) != 0)
        {
            if (UnsupportedJunctionBackupAliases() is string aliases)
            {
                throw new NotSupportedException(aliases);
            }
            next = PrepareNativeReserveParty(next, field);
        }
        if (activeMenu)
        {
            PublishPartyMenuCallback(next, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
            partyMenu = partyMenu! with { TemporaryCombatParty = next.CombatParty.ToArray(), NativeReturnResult = field is null ? partyMenu.NativeReturnResult : (ushort)1, CombatPartyIndependent = unchangedNativeParty ? partyMenu.CombatPartyIndependent : combatOnly && partyMenu.NativeReturnResult == 1, Callbacks = checked(partyMenu.Callbacks + 1) };
        }
        else
        {
            services.SetCharacterVitals(next);
        }
        if (field is not null)
        {
            Scene.Party.Clear();
            Scene.Party.AddRange(field.Select(row => (int)row));
            if (!activeMenu)
            {
                RefreshParty();
            }
        }
        return true;
    }
}
