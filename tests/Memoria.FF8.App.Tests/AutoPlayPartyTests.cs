using System.Text.Json;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayPartyTests
{
    [Theory]
    [InlineData(NativePartyMenuPhase.Requested)]
    [InlineData(NativePartyMenuPhase.Entering)]
    [InlineData(NativePartyMenuPhase.Closing)]
    [InlineData(NativePartyMenuPhase.Teardown)]
    [InlineData(NativePartyMenuPhase.Restoring)]
    [InlineData(NativePartyMenuPhase.FieldReentry)]
    [InlineData(NativePartyMenuPhase.FieldResumeInitialization)]
    public void OpeningAndClosingMenusAdvanceTheirNativeCallbacksBeforeInput(NativePartyMenuPhase phase)
    {
        foreach (NativeFieldMenuKind kind in Enum.GetValues<NativeFieldMenuKind>())
        {
            Assert.Equal(new PlayerAction(PlayerActionKind.Wait), GameWorld.PlanAutoPlayPartyInput(Menu() with { Phase = phase, Kind = kind, TemporaryCombatParty = null, TemporaryReserveParty = null }));
        }
    }

    [Theory]
    [InlineData(NativeFieldMenuKind.Normal)]
    [InlineData(NativeFieldMenuKind.Save)]
    [InlineData(NativeFieldMenuKind.Shop)]
    [InlineData(NativeFieldMenuKind.WeaponShop)]
    public void OptionalActiveMenusCloseWithTheOrdinaryCancelInput(NativeFieldMenuKind kind)
    {
        Assert.Equal(new PlayerAction(PlayerActionKind.Cancel), GameWorld.PlanAutoPlayPartyInput(Menu() with { Kind = kind }));
    }

    [Fact]
    public void PartySelectionFillsEmptySlotsWithTheLowestEligibleUnlockedReserveMemberThenConfirms()
    {
        NativePartyMenuState menu = Menu() with { TemporaryCombatParty = [0, 255, 255], TemporaryReserveParty = [5, 3, 1, 2, 255, 255, 255, 255], EligibleMask = 0x3F, LockedMask = 1 << 1 };
        string before = JsonSerializer.Serialize(menu);
        Assert.Equal(new PlayerAction(PlayerActionKind.SelectPartySlot, 1), GameWorld.PlanAutoPlayPartyInput(menu));
        Assert.Equal(new PlayerAction(PlayerActionKind.SelectPartyMember, 2), GameWorld.PlanAutoPlayPartyInput(menu with { SelectedPartySlot = 1 }));
        Assert.Equal(new PlayerAction(PlayerActionKind.SelectPartySlot, 2), GameWorld.PlanAutoPlayPartyInput(menu with { TemporaryCombatParty = [0, 2, 255] }));
        Assert.Equal(new PlayerAction(PlayerActionKind.Confirm), GameWorld.PlanAutoPlayPartyInput(menu with { TemporaryCombatParty = [0, 2, 3] }));
        Assert.Equal(before, JsonSerializer.Serialize(menu));
    }

    [Fact]
    public void GuestSelectionFillsOnlyEnabledSlotsAndPreservesTheFixedLeader()
    {
        NativePartyMenuState menu = Menu() with { Kind = NativeFieldMenuKind.GuestSelection, NativeEntrySelection = 4, TemporaryCombatParty = [0, 255, 255], TemporaryReserveParty = [4, 1, 2, 255, 255, 255, 255, 255], EligibleMask = 0x17, LockedMask = 1 };
        Assert.Equal(new PlayerAction(PlayerActionKind.SelectPartySlot, 2), GameWorld.PlanAutoPlayPartyInput(menu));
        Assert.Equal(new PlayerAction(PlayerActionKind.SelectPartyMember, 1), GameWorld.PlanAutoPlayPartyInput(menu with { SelectedPartySlot = 2 }));
        Assert.Equal(new PlayerAction(PlayerActionKind.Confirm), GameWorld.PlanAutoPlayPartyInput(menu with { TemporaryCombatParty = [0, 255, 1] }));
        Assert.Equal(new PlayerAction(PlayerActionKind.Confirm), GameWorld.PlanAutoPlayPartyInput(menu with { NativeEntrySelection = 0 }));
    }

    [Fact]
    public void ExistingOccupiedSelectionIsClearedBeforeConfirmingWithoutReplacingMembers()
    {
        Assert.Equal(new PlayerAction(PlayerActionKind.Cancel), GameWorld.PlanAutoPlayPartyInput(Menu() with { SelectedPartySlot = 0 }));
    }

    [Fact]
    public void EmptySelectionWithoutAnEligibleUnlockedReserveFailsExplicitly()
    {
        NativePartyMenuState menu = Menu() with { TemporaryCombatParty = [0, 255, 2], SelectedPartySlot = 1, TemporaryReserveParty = [1, 255, 255, 255, 255, 255, 255, 255], EligibleMask = 7, LockedMask = 2 };
        Assert.Throws<NotSupportedException>(() => GameWorld.PlanAutoPlayPartyInput(menu));
        Assert.Throws<NotSupportedException>(() => GameWorld.PlanAutoPlayPartyInput(menu with { LockedMask = 0, EligibleMask = 5 }));
    }

    private static NativePartyMenuState Menu()
    {
        // Declared menu panes exercise automatic input selection without claiming VM checkpoint authority.
        return new NativePartyMenuState(0, string.Empty, string.Empty, 0, 0, 0, 0, 0, NativePartyMenuPhase.Active, 2, [0, 1, 2], 7, 1, 1, false, NativeFieldMenuKind.PartySelection, 2, 0, null, null, null, [255, 255, 255, 255, 255, 255, 255, 255], null);
    }
}
