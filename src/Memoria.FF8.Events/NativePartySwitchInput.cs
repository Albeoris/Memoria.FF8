using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public static class NativePartySwitchInput
{
    public static void Validate(PlayerAction action)
    {
        if (action.Kind != PlayerActionKind.SwitchPartyMembers || (uint)action.Value >= 8 || action.Quantity is not (>= 0 and < 8) || action.Text is not null || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0 || action.WorldButtons != 0 || action.WaitWhilePlayable)
        {
            throw new ArgumentException("Switch input requires two explicit physical character rows and one ordinary menu action.", nameof(action));
        }
    }

    public static bool CanExchange(NativeCharacterVitals vitals, int source, int target, byte entrySelection)
    {
        if ((entrySelection & 2) == 0 || vitals.DreamEnabled || (uint)source >= 8 || (uint)target >= 8 || vitals.Records[source].CharacterId != source || vitals.Records[target].CharacterId != target || (vitals.Records[source].MemberFlags & 1) == 0 || (vitals.Records[target].MemberFlags & 1) == 0)
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
        byte[] selected = vitals.CombatParty.Select(row => row == source ? (byte)target : row == target ? (byte)source : row).ToArray();
        //4CBFFE..4CC047 refuses closing an incomplete or entirely incapacitated party.
        return !selected.Contains((byte)255) && selected.Any(row => (vitals.Records[row].Status & 1) == 0);
    }

    public static NativeCharacterVitals Apply(NativeCharacterVitals vitals, PlayerAction action, byte entrySelection)
    {
        Validate(action);
        vitals.Validate();
        int target = action.Quantity!.Value;
        if (!CanExchange(vitals, action.Value, target, entrySelection))
        {
            throw new InvalidOperationException("The current menu does not permit this party-member exchange.");
        }
        byte[] party = vitals.CombatParty.Select(row => row == action.Value ? (byte)target : row == target ? (byte)action.Value : row).ToArray();
        //4CC46D publishes the selected combat rows;495EF0 rebuilds their stats.
        return (vitals with { CombatParty = party }).RebuildParty();
    }
}

public sealed partial class EventEngine
{
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
        if (!NativePartySwitchInput.CanExchange(source, action.Value, action.Quantity!.Value, entry))
        {
            return false;
        }
        NativeCharacterVitals next = NativePartySwitchInput.Apply(source, action, entry);
        byte[] field = source.PreparePartyMenuFieldSlots(Scene.Party, next.CombatParty);
        if (!activeMenu && (services.WorldControls!.EventFlags104 & 0x800) != 0)
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
            partyMenu = partyMenu! with { TemporaryCombatParty = next.CombatParty.ToArray(), NativeReturnResult = 1, Callbacks = checked(partyMenu.Callbacks + 1) };
        }
        else
        {
            services.SetCharacterVitals(next);
        }
        Scene.Party.Clear();
        Scene.Party.AddRange(field.Select(row => (int)row));
        if (!activeMenu)
        {
            RefreshParty();
        }
        return true;
    }
}
