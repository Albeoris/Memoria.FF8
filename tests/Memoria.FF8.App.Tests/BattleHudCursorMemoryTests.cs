using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudCursorMemoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrawRemembersSubmittedBlizzardAndItsStockOrCastMode(bool cast)
    {
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(Create(), inputs.Add);
        OpenDraw(hud);
        hud.Choose(1);
        hud.Choose(cast ? 1 : 0);
        if (cast)
        {
            hud.SelectTarget("enemy", true);
        }
        BattleInput input = Assert.Single(inputs);
        Assert.Equal(2, input.ActionId);
        Assert.Equal(cast, input.DrawCast);
        Assert.Equal(BattleAbility.Draw, hud.SelectedCommand);
        OpenDraw(hud);
        Assert.Equal(2, hud.SelectedAction!.Id);
        Assert.Equal(1, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingDrawMode);
        Assert.Equal(cast ? 1 : 0, hud.ChoiceIndex);
    }

    [Fact]
    public void InitialCursorResetsSubmittedCommandSpellAndDrawMode()
    {
        BattleHudController hud = new(Create(), (_, _) => { }, () => { }, () => { }, execute: _ => { });
        Assert.False(hud.CursorMemory);
        OpenDraw(hud);
        hud.Choose(1);
        hud.Choose(1);
        hud.SelectTarget("enemy", true);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        OpenDraw(hud);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(0, hud.ChoiceIndex);
    }

    [Fact]
    public void CharacterSwitchingRestoresEachAcceptedCommandAndAction()
    {
        BattleHudController hud = Hud(Create(), _ => { });
        Submit(hud, BattleAbility.Magic, 1);
        hud.SelectParty("second");
        Submit(hud, BattleAbility.Item, 0);
        hud.SelectParty("first");
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(2, hud.SelectedAction!.Id);
        hud.CycleCommandActor();
        Assert.Equal("second", hud.SelectedActor!.Id);
        Assert.Equal(BattleAbility.Item, hud.SelectedCommand);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.SelectParty("first");
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
    }

    [Fact]
    public void EachCommandRestoresItsOwnAcceptedAction()
    {
        BattleHudController hud = Hud(Create(), _ => { });
        Submit(hud, BattleAbility.Magic, 1);
        Submit(hud, BattleAbility.Item, 0);
        Submit(hud, BattleAbility.GuardianForce, 1);
        OpenCommand(hud, BattleAbility.Magic);
        Assert.Equal(2, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Cancel);
        OpenCommand(hud, BattleAbility.Item);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Cancel);
        OpenCommand(hud, BattleAbility.GuardianForce);
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelledOrRejectedActionDoesNotReplaceAcceptedMemory(bool reject)
    {
        bool rejecting = false;
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(Create(), input =>
        {
            if (rejecting)
            {
                throw new InvalidOperationException("Unavailable");
            }
            inputs.Add(input);
        });
        Submit(hud, BattleAbility.Magic, 1);
        OpenCommand(hud, BattleAbility.Magic);
        hud.Choose(0);
        if (reject)
        {
            rejecting = true;
            hud.SelectTarget("enemy", true);
            Assert.True(hud.SelectingTarget);
        }
        hud.Navigate(MenuInput.Cancel);
        hud.Navigate(MenuInput.Cancel);
        Assert.Single(inputs);
        OpenCommand(hud, BattleAbility.Magic);
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    [Fact]
    public void DrawModeMemoryBelongsToItsAcceptedSpell()
    {
        BattleHudController hud = Hud(Create(), _ => { });
        OpenDraw(hud);
        hud.Choose(1);
        hud.Choose(1);
        hud.SelectTarget("enemy", true);
        OpenDraw(hud);
        hud.Choose(0);
        Assert.Equal(0, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Confirm);
        OpenDraw(hud);
        hud.Choose(1);
        Assert.Equal(1, hud.ChoiceIndex);
    }

    [Fact]
    public void NewCharacterAndSameCharacterReselectionUseTheirOwnMemory()
    {
        BattleHudController hud = Hud(Create(), _ => { });
        Submit(hud, BattleAbility.Magic, 1);
        hud.SelectParty("first");
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        hud.SelectParty("second");
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        OpenCommand(hud, BattleAbility.Magic);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.SelectParty("first");
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    [Fact]
    public void RealQueuedActionKeepsMemoryWithItsSubmittingActorThroughHandoff()
    {
        BattleSession battle = Create();
        BattleHudController hud = Hud(battle, input => battle.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId));
        Submit(hud, BattleAbility.Magic, 1);
        hud.BuildView();
        Assert.Equal("second", hud.SelectedActor!.Id);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        for (int frame = 0; frame < 25; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(9, battle.Combatants.Single(actor => actor.Id == "first").MagicStocks.Single(stock => stock.Id == 2).Count);
        battle.AdvanceTurn();
        Assert.Contains(battle.ReadyPlayerCommandOrder, actor => actor.Id == "first");
        hud.SelectParty("first");
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(2, hud.SelectedAction!.Id);
    }

    [Fact]
    public void DrawRestoresSpellIdentityAcrossSourceOrderAndFallsBackWhenAbsent()
    {
        BattleSession battle = Create(definition => definition with
        {
            Combatants =
            [
                .. definition.Combatants,
                definition.Combatants[2] with { Id = "reordered", NativeDrawSlots = [new(2, 0), new(1, 0), new(0, 0), new(0, 0)] },
                definition.Combatants[2] with { Id = "missing", NativeDrawSlots = [new(1, 0), new(0, 0), new(0, 0), new(0, 0)] }
            ]
        });
        BattleHudController hud = Hud(battle, _ => { });
        OpenDraw(hud);
        hud.Choose(1);
        hud.Choose(0);
        OpenDraw(hud, "reordered");
        Assert.Equal(new[] { "Blizzard", "Fire" }, hud.ChoiceLabels);
        Assert.Equal(2, hud.SelectedAction!.Id);
        Assert.Equal(0, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Down);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Up);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        OpenDraw(hud, "missing");
        Assert.Equal(1, hud.SelectedAction!.Id);
        Assert.Equal(0, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Down);
        Assert.Equal(1, hud.SelectedAction!.Id);
        Assert.Equal(0, hud.ChoiceIndex);
    }

    [Fact]
    public void DepletedAcceptedItemFallsBackToFirstRemainingChoiceAndNavigatesFromIt()
    {
        BattleSession battle = Create(definition => definition with
        {
            Items = [new(1, 10), new(2, 1), new(3, 10)],
            ActionDefinitions = [.. definition.ActionDefinitions, new(BattleAbility.Item, 3, "Third item", BattleActionEffect.Damage, 10, false)]
        });
        BattleHudController hud = Hud(battle, input => battle.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId));
        Submit(hud, BattleAbility.Item, 1);
        Assert.DoesNotContain(battle.Items, stock => stock.Id == 2);
        for (int frame = 0; frame < 25; frame++)
        {
            battle.Tick();
        }
        battle.AdvanceTurn();
        Assert.Contains(battle.ReadyPlayerCommandOrder, actor => actor.Id == "first");
        hud.SelectParty("first");
        OpenCommand(hud, BattleAbility.Item);
        Assert.Equal(new[] { "First item", "Third item" }, hud.ChoiceLabels);
        Assert.Equal(1, hud.SelectedAction!.Id);
        Assert.Equal(0, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Down);
        Assert.Equal(3, hud.SelectedAction!.Id);
        Assert.Equal(1, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Up);
        Assert.Equal(1, hud.SelectedAction!.Id);
        Assert.Equal(0, hud.ChoiceIndex);
    }

    [Fact]
    public void CursorPreferenceChangesApplyToNewSelectionsBetweenAcceptedTurns()
    {
        BattleHudController hud = Hud(Create(), _ => { });
        Submit(hud, BattleAbility.Magic, 1);
        hud.CursorMemory = false;
        Submit(hud, BattleAbility.Item, 1);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        OpenCommand(hud, BattleAbility.Magic);
        Assert.Equal(1, hud.SelectedAction!.Id);
        hud.Navigate(MenuInput.Cancel);
        hud.CursorMemory = true;
        OpenCommand(hud, BattleAbility.Magic);
        Assert.Equal(2, hud.SelectedAction!.Id);
        hud.Choose(0);
        hud.SelectTarget("enemy", true);
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(1, hud.SelectedAction!.Id);
    }

    private static BattleHudController Hud(BattleSession battle, Action<BattleInput> execute)
    {
        return new(battle, (_, _) => { }, () => { }, () => { }, execute: execute) { CursorMemory = true };
    }

    private static void OpenCommand(BattleHudController hud, BattleAbility command)
    {
        hud.Choose(hud.Commands.ToList().IndexOf(command));
        Assert.True(hud.SelectingAction);
    }

    private static void OpenDraw(BattleHudController hud, string source = "enemy")
    {
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Draw));
        Assert.True(hud.SelectingDrawSource);
        hud.SelectTarget(source, true);
        Assert.True(hud.SelectingAction);
    }

    private static void Submit(BattleHudController hud, BattleAbility command, int index)
    {
        OpenCommand(hud, command);
        hud.Choose(index);
        Assert.True(hud.SelectingTarget);
        hud.SelectTarget("enemy", true);
        Assert.False(hud.SelectingTarget);
    }

    private static BattleSession Create(Func<BattleDefinition, BattleDefinition>? transform = null)
    {
        BattleCombatantDefinition Player(string id) => new()
        {
            Id = id,
            Name = id,
            IsPlayer = true,
            MaximumHp = 1000,
            CurrentHp = 1000,
            InitialAtb = BattleSession.AtbMaximum,
            Speed = 100,
            Abilities = [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.Item, BattleAbility.GuardianForce, BattleAbility.Draw],
            MagicStocks = [new(1, 10), new(2, 10)],
            EquipmentIds = [1, 2]
        };
        BattleDefinition definition = new()
        {
            Combatants =
            [
                Player("first"),
                Player("second"),
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeDrawSlots = [new(1, 0), new(2, 0), new(0, 0), new(0, 0)] }
            ],
            Items = [new(1, 10), new(2, 10)],
            GuardianForces = [new(1, 1000, 1000, true), new(2, 1000, 1000, true)],
            ActionDefinitions =
            [
                new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 10, false),
                new(BattleAbility.Magic, 2, "Blizzard", BattleActionEffect.Damage, 10, false),
                new(BattleAbility.Item, 1, "First item", BattleActionEffect.Damage, 10, false),
                new(BattleAbility.Item, 2, "Second item", BattleActionEffect.Damage, 10, false),
                new(BattleAbility.GuardianForce, 1, "First GF", BattleActionEffect.Damage, 10, false),
                new(BattleAbility.GuardianForce, 2, "Second GF", BattleActionEffect.Damage, 10, false)
            ]
        };
        return new(transform is null ? definition : transform(definition), BattleAtbMode.TurnBased);
    }
}
