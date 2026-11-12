using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7GuestAbilityUiTests
{
    [Fact]
    public void UnsupportedAbilitiesAndTheirStockAppearInRed()
    {
        BattleUiTests.TestCanvas canvas = new();
        UiTheme theme = new();
        BattleHudLayout layout = new(new(0, 0, 960, 672));
        Ff7Scene.DrawBattleChoices(canvas, theme, layout, 1, "BLUE MAGIC", [new("Available"), new("Unsupported", "6", true)], 1, true);

        Assert.Equal(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Available").Color);
        Assert.Equal(new UiColor(255, 80, 80), Assert.Single(canvas.TextDraws, draw => draw.Text == "Unsupported").Color);
        Assert.Equal(new UiColor(255, 80, 80), Assert.Single(canvas.TextDraws, draw => draw.Text == "6").Color);
    }

    [Fact]
    public void SelectingAnUnsupportedAbilityShowsItsReasonWithoutSelectingATarget()
    {
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        Ff7BattleCommandOption option = new(Ff7BattleCommand.Magic, 0xEC01, 0, true, true)
        {
            DisplayName = "Blue Magic",
            Category = "Blue Magic",
            UnsupportedReason = "This action requires a target status contract that is unavailable in the current battle bridge."
        };
        typeof(Ff7Scene).GetMethod("SelectBattleAction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, [option]);

        Assert.Same(option, typeof(Ff7Scene).GetField("unavailableBattleCommand", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene));
        Assert.Null(typeof(Ff7Scene).GetField("selectedBattleCommand", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene));
        BattleUiTests.TestCanvas canvas = new();
        Ff7Scene.DrawBattleUnsupportedAbility(canvas, new UiTheme(), new(new(0, 0, 960, 672)), 1, option);
        Assert.Contains(canvas.TextDraws, draw => draw.Text == option.UnsupportedReason && draw.Color == new UiColor(255, 80, 80));
        Assert.Contains(canvas.Texts, text => text == "Confirm or Cancel to return");
    }

    [Theory]
    [InlineData((ushort)57, (ushort)0x10)]
    [InlineData((ushort)0xEC01, (ushort)1)]
    public void GenericAbilityKeysRetainOrdinarySingleTargetQueueSemantics(ushort ability, ushort target)
    {
        Ff7BattleCommandQueueState state = Ff7BattleCommandQueue.StageReady(Ff7BattleCommandQueueState.Create(), 0, (byte)Ff7BattleCommand.Magic, ability, target, guestAttack: true, guestAbility: ability);
        Ff7BattleQueuedCommand command = Assert.Single(state.Ready.OfType<Ff7BattleQueuedCommand>());
        Assert.Equal(ability, command.GuestAbility);
        Assert.Equal(ability, command.Argument);
        Assert.Equal(target, command.TargetMask);
        Assert.True(command.GuestAttack);
    }
}
