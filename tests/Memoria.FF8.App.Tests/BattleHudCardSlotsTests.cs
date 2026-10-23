using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudCardSlotsTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    public void OpeningIsOneModalDealAndHeldInputCannotSelectOrBypassIt(int crisis, bool pointer)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession session = new(new BattleDefinition
        {
            SelphieCardSlots = true,
            NativeGlobalRandom = new(17),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = assets.NativeActions.ToArray(),
            NativeLimitsDisabled = false,
            LimitProgress = game.ReadNewGameLimitProgress(),
            Combatants = [new() { Id = "selphie", Name = "Selphie", IsPlayer = true, ModelId = 5, Level = 20, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(5) }, new() { Id = "enemy", Name = "Enemy", Speed = 1 }]
        }, BattleAtbMode.TurnBased);
        session = BattleSession.Restore(session.Capture() with { NativeLimits = [new("selphie", (byte)crisis)] });
        NativeBattleRandomState expected = session.Capture().Random!;
        for (int draw = 0; draw < 4 * (2 * crisis + 1); draw++)
        {
            expected = expected.Draw(out _);
        }
        int debug = 0;
        int turns = 0;
        BattleHudController hud = new(session, session.QueueAttack, () => debug++, () => turns++);
        List<TripleTriadSound> sounds = [];
        hud.SlotsSoundRequested += sounds.Add;
        hud.Navigate(MenuInput.Right);
        hud.Choose(0);
        Assert.True(hud.CardSlotsOwnsInput);
        Assert.Equal(2 * crisis + 1, session.PendingSlotsDeal!.Candidates.Length);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.Capture().Random));
        Assert.Null(session.GetNativeSlotsRoll("selphie"));
        hud.AdvanceSlotsPresentation(TimeSpan.FromSeconds(1), true, true);
        hud.Navigate(MenuInput.Confirm);
        Assert.Null(session.PendingSlotsDeal.SelectedIndex);
        string before = JsonSerializer.Serialize(session.Capture());
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        view.Draw(canvas, new UiTheme());
        hud.Navigate(MenuInput.Cancel);
        hud.Navigate(MenuInput.PageRight);
        hud.Choose(1);
        hud.RerollNativeSlots();
        hud.SelectParty("selphie");
        hud.CycleCommandActor();
        hud.AdvanceTurn();
        hud.OpenDebug();
        hud.SelectTarget("enemy", true);
        Assert.Equal(0, debug);
        Assert.Equal(0, turns);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        Assert.DoesNotContain("Do over", canvas.Texts);
        Assert.DoesNotContain("Cast", canvas.Texts);
        Assert.Equal(1, sounds.Count(sound => sound == TripleTriadSound.Place));
        BattleNativeSlotsRoll accepted = session.PendingSlotsDeal.Candidates[0];
        hud.AdvanceSlotsPresentation(TimeSpan.Zero, false, false);
        if (pointer)
        {
            UiRect card = SlotsCardView.CardBounds(new(0, 0, 960, 672), 0, 1);
            view.Pointer(card.X + 10, card.Y + 10, true);
        }
        else
        {
            hud.Navigate(MenuInput.Confirm);
        }
        Assert.Equal(0, session.PendingSlotsDeal!.SelectedIndex);
        hud.Navigate(MenuInput.Right);
        hud.Navigate(MenuInput.Confirm);
        hud.AdvanceSlotsPresentation(TimeSpan.FromMilliseconds(240), true, true);
        Assert.Null(session.PendingSlotsDeal);
        Assert.Equal(accepted, Assert.Single(session.QueuedActions).NativeSlots);
        Assert.True(hud.CardSlotsOwnsInput);
        hud.AdvanceSlotsPresentation(TimeSpan.Zero, false, false);
        Assert.False(hud.CardSlotsOwnsInput);
        Assert.Equal(1, sounds.Count(sound => sound == TripleTriadSound.Confirm));
    }
}
