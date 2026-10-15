using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePartySlotUiTests
{
    [Fact]
    public void FullPartyOffersExplicitCellsAndSeparateDoneInsteadOfAutomaticConfirmation()
    {
        //UI-only declared pane values; source metadata is unconsumed and no VM/checkpoint authority is claimed.
        NativePartyMenuState menu = new(0, string.Empty, string.Empty, 0, 0, 0, 0, 0, NativePartyMenuPhase.Active, 2, [0, 1, 5], 0x3B, 1, 1, false, NativeFieldMenuKind.PartySelection, 2, 0, null, null, null, [3, 4, 255, 255, 255, 255, 255, 255], null);
        string before = JsonSerializer.Serialize(menu);
        IReadOnlyList<PlayerAction> inputs = NativePartyMenuProjection.Inputs(menu);
        Assert.Equal(new[] { 0, 1, 2 }, inputs.Where(input => input.Kind == PlayerActionKind.SelectPartySlot).Select(input => input.Value));
        Assert.Equal(PlayerActionKind.SelectPartySlot, inputs[0].Kind);
        Assert.Equal(PlayerActionKind.Confirm, inputs[^1].Kind);
        NativePartyMenuState pending = menu with { SelectedPartySlot = 2 };
        IReadOnlyList<PlayerAction> replacements = NativePartyMenuProjection.Inputs(pending);
        Assert.DoesNotContain(replacements, input => input.Kind == PlayerActionKind.Confirm);
        Assert.Equal(new[] { 3, 4, 255 }, replacements.Where(input => input.Kind == PlayerActionKind.SelectPartyMember).Select(input => input.Value));
        Assert.Contains(new PlayerAction(PlayerActionKind.SelectPartyMember, 4), replacements);
        Assert.Equal(before, JsonSerializer.Serialize(menu));
        Assert.DoesNotContain(NativePartyMenuProjection.Inputs(menu with { TemporaryCombatParty = [0, 1, 255] }), input => input.Kind == PlayerActionKind.Confirm);
    }

    [Theory]
    [InlineData(PlayerActionKind.SelectPartySlot, -1)]
    [InlineData(PlayerActionKind.SelectPartySlot, 3)]
    [InlineData(PlayerActionKind.SelectPartySlot, 256)]
    [InlineData(PlayerActionKind.SelectPartyMember, -1)]
    [InlineData(PlayerActionKind.SelectPartyMember, 8)]
    [InlineData(PlayerActionKind.SelectPartyMember, 254)]
    [InlineData(PlayerActionKind.SelectPartyMember, 256)]
    public void InvalidFullWidthPartyInputRefusesBeforeChangingTheCompleteJournal(PlayerActionKind kind, int value)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine events = new(session);
        GameState before = session.State;
        ReplayJournal journal = session.ReplayJournal!;
        Assert.Throws<ArgumentException>(() => events.Apply(new PlayerAction(kind, value)));
        Assert.Same(before, session.State);
        Assert.Same(journal, session.ReplayJournal);
        Assert.True(session.ReplayJournal!.IsComplete);
        Assert.Equal(0, session.ReplayJournal.EntryCount);
    }
}
