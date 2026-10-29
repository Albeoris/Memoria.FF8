using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSelectHudTests
{
    [Fact]
    public void Select_hides_the_command_panel_and_party_vitals_and_release_restores_them()
    {
        BattleSession battle = CreateBattle();
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { });
        RecordingCanvas visible = Draw(hud);
        Assert.Contains("COMMAND", visible.Texts);
        Assert.Contains("Attack", visible.Texts);
        Assert.Contains("Squall", visible.Texts);
        Assert.Contains("Zell", visible.Texts);
        Assert.Contains(visible.Texts, text => text.Contains('/'));
        Assert.True(visible.Fills > 0);
        Assert.True(visible.Pointers > 0);

        hud.SetNativeCommandButtons(0x100);
        RecordingCanvas hidden = Draw(hud);

        Assert.True(hud.CommandMenuHidden);
        Assert.Empty(hidden.Texts);
        Assert.Equal(0, hidden.Fills);
        Assert.Equal(0, hidden.Pointers);
        hud.SetNativeCommandButtons(0);
        RecordingCanvas released = Draw(hud);
        Assert.False(hud.CommandMenuHidden);
        Assert.Equal(visible.Texts, released.Texts);
        Assert.Equal(visible.Fills, released.Fills);
        Assert.Equal(visible.Pointers, released.Pointers);
    }

    [Fact]
    public void Hidden_command_menu_ignores_keyboard_and_pointer_and_preserves_actor_selection()
    {
        BattleSession battle = CreateBattle();
        List<BattleAttackCommand> commands = [];
        BattleHudController hud = new(battle, (actor, target) => commands.Add(new(actor, target)), () => { }, () => { });
        hud.SelectParty("party-1");
        int choice = hud.ChoiceIndex;
        UiView view = hud.BuildView();
        hud.SetNativeCommandButtons(0x100);

        view.Key(UiKey.Down);
        view.Key(UiKey.Confirm);
        view.Pointer(100, 540, true);
        view.Pointer(100, 540, false);
        view.Pointer(550, 540, true);
        view.Pointer(550, 540, false);

        Assert.False(hud.SelectingTarget);
        Assert.False(hud.SelectingAction);
        Assert.Equal(choice, hud.ChoiceIndex);
        Assert.Equal("party-1", hud.SelectedActor!.Id);
        Assert.Empty(commands);
        hud.SetNativeCommandButtons(0);
        view.Key(UiKey.Confirm);
        Assert.True(hud.SelectingTarget);
        view.Key(UiKey.Confirm);
        Assert.Equal(new BattleAttackCommand("party-1", "enemy-0"), Assert.Single(commands));
    }

    [Fact]
    public void Hidden_target_menu_ignores_confirm_cancel_hover_and_click_and_restores_the_selected_target()
    {
        BattleSession battle = CreateBattle();
        List<BattleAttackCommand> commands = [];
        BattleHudController hud = new(battle, (actor, target) => commands.Add(new(actor, target)), () => { }, () => { });
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Down);
        Assert.Equal("enemy-1", hud.SelectedTarget!.Id);
        UiView view = hud.BuildView();
        hud.SetNativeCommandButtons(0x100);

        view.Key(UiKey.Up);
        view.Key(UiKey.Confirm);
        view.Key(UiKey.Cancel);
        view.Pointer(100, 540, false);
        view.Pointer(100, 540, true);
        view.Pointer(100, 540, false);
        view.Pointer(550, 580, true);
        view.Pointer(550, 580, false);

        Assert.True(hud.SelectingTarget);
        Assert.Equal("party-0", hud.SelectedActor!.Id);
        Assert.Equal("enemy-1", hud.SelectedTarget!.Id);
        Assert.Empty(commands);
        Assert.Empty(Draw(hud).Texts);
        hud.SetNativeCommandButtons(0);
        RecordingCanvas released = Draw(hud);
        Assert.Contains("TARGET  < ENEMIES >", released.Texts);
        Assert.Contains("Bite Bug B", released.Texts);
        view.Key(UiKey.Confirm);
        Assert.Equal(new BattleAttackCommand("party-0", "enemy-1"), Assert.Single(commands));
    }

    [Fact]
    public void Select_preserves_the_open_magic_submenu_and_selected_command_on_release()
    {
        BattleDefinition definition = CreateBattle().Capture().Definition;
        definition.Combatants[0] = definition.Combatants[0] with { Abilities = [BattleAbility.Attack, BattleAbility.Magic], MagicStocks = [new(1, 5)] };
        definition = definition with { ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 10, false)] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { });
        hud.Navigate(MenuInput.Down);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingAction);
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        Assert.Equal(1, hud.SelectedAction!.Id);
        RecordingCanvas visible = Draw(hud);
        Assert.Contains("Magic", visible.Texts);

        hud.SetNativeCommandButtons(0x100);
        Assert.Empty(Draw(hud).Texts);
        Assert.True(hud.CommandMenuHidden);
        Assert.True(hud.SelectingAction);
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        Assert.Equal(1, hud.SelectedAction!.Id);

        hud.SetNativeCommandButtons(0);
        Assert.Equal(visible.Texts, Draw(hud).Texts);
        Assert.False(hud.CommandMenuHidden);
        Assert.True(hud.SelectingAction);
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        Assert.Equal(1, hud.SelectedAction!.Id);
    }

    private static BattleSession CreateBattle()
    {
        return new BattleSession(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party-0", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "party-1", Name = "Zell", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "enemy-0", Name = "Bite Bug A" },
                new() { Id = "enemy-1", Name = "Bite Bug B" }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static RecordingCanvas Draw(BattleHudController hud)
    {
        RecordingCanvas canvas = new();
        hud.BuildView().Draw(canvas, new UiTheme());
        return canvas;
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<string> Texts { get; } = [];
        public int Fills { get; private set; }
        public int Pointers { get; private set; }

        public void Fill(UiRect bounds, UiColor color)
        {
            Fills++;
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add(text);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize * 0.5f), fontSize);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }

        public void MenuPointer(UiRect bounds)
        {
            Pointers++;
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
