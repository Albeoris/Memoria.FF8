using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudGuardianFormTests
{
    [Fact]
    public void FinisherSkipHasItsOwnRemappableInputAndDoesNotToggleSpeedOrBoost()
    {
        KeyboardInput keyboard = new();
        Assert.Equal(Keys.F8, Assert.Single(Assert.Single(keyboard.Profile.GetBindings(ControlAction.GuardianFormSkip, ControlDevice.Keyboard)).Keys));
        Assert.Equal(Buttons.RightStick, Assert.Single(Assert.Single(keyboard.Profile.GetBindings(ControlAction.GuardianFormSkip, ControlDevice.Controller)).Buttons));
        keyboard.Profile = keyboard.Profile.WithBinding(ControlAction.GuardianFormSkip, ControlDevice.Keyboard, new([Keys.P], []));
        keyboard.Read(default, true);
        keyboard.Read(new KeyboardState(Keys.P), true);
        Assert.True(keyboard.GuardianFormSkipRequested);
        Assert.False(keyboard.SpeedToggleRequested);
        Assert.False(keyboard.GuardianBoostRequested);
        keyboard.Read(new KeyboardState(Keys.P), true);
        Assert.False(keyboard.GuardianFormSkipRequested);
    }
    [Theory]
    [InlineData(MenuInput.Left)]
    [InlineData(MenuInput.Cancel)]
    public void RightOpensSummonAndBackRetainsActorAndGuardianForce(MenuInput back)
    {
        BattleSession battle = Create();
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(battle, inputs);
        hud.Choose(1);
        hud.Navigate(MenuInput.Right);
        Assert.True(hud.SelectingGuardianFormAction);
        Assert.Equal("Summon", Assert.Single(hud.ChoiceLabels));
        Assert.False(hud.SelectingTarget);
        hud.Navigate(MenuInput.Right);
        Assert.Equal("actor", hud.SelectedActor!.Id);
        hud.Navigate(back);
        Assert.False(hud.SelectingGuardianFormAction);
        Assert.True(hud.SelectingAction);
        Assert.Equal(2, hud.SelectedAction!.Id);
        Assert.Equal("actor", hud.SelectedActor!.Id);
        Assert.Empty(inputs);
        Assert.Equal(BattleSession.AtbMaximum, hud.SelectedActor.Atb);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryConfirmationRemainsClassic(bool enabled)
    {
        BattleSession battle = Create(enabled);
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(battle, inputs);
        hud.Choose(1);
        Assert.Equal(enabled, Assert.Single(hud.ChoiceLabels).Contains('>'));
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        hud.Navigate(MenuInput.Confirm);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal(BattleInputKind.Action, input.Kind);
        Assert.Equal(BattleAbility.GuardianForce, input.Ability);
        Assert.Equal(2, input.ActionId);
    }

    [Theory]
    [InlineData(0, BattleGuardianFormAction.Attack)]
    [InlineData(1, BattleGuardianFormAction.Fire)]
    [InlineData(2, BattleGuardianFormAction.HellFire)]
    public void ActiveFormUsesDedicatedCommandsWithoutStockOrHeroAttack(int choice, BattleGuardianFormAction action)
    {
        BattleSession original = Create();
        BattleSession battle = BattleSession.Restore(original.Capture() with
        {
            GuardianForms = new(true, 1, [new("actor", 2, "ifrit-v1", BattleGuardianFormPhase.Active, 1, 100) { EntryCompleted = true }])
        });
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(battle, inputs);
        Assert.Equal(new[] { "Attack", "Fire", "Hell Fire" }, hud.ChoiceLabels);
        Assert.False(hud.NativeLimitAvailable);
        hud.Choose(choice);
        Assert.True(hud.SelectingTarget);
        Assert.Equal(action.ToString().Replace("HellFire", "Hell Fire"), hud.TargetActionCaption);
        Assert.Null(hud.TargetActionPreview);
        hud.Navigate(MenuInput.Confirm);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal(BattleInputKind.GuardianFormAction, input.Kind);
        Assert.Equal((int)action, input.ActionId);
        Assert.Equal("enemy", input.TargetId);
        Assert.Empty(battle.Combatants.Single(actor => actor.Id == "actor").MagicStocks);
    }

    [Fact]
    public void PointerAffordanceAndConfirmSubmitSelfWithoutTargetPicker()
    {
        BattleSession battle = Create();
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(battle, inputs);
        hud.Choose(1);
        hud.OpenGuardianFormActions(0);
        hud.Choose(0);
        BattleInput input = Assert.Single(inputs);
        Assert.Equal(BattleInputKind.GuardianFormActivate, input.Kind);
        Assert.Equal("actor", input.ActorId);
        Assert.Equal("actor", input.TargetId);
        Assert.Equal(2, input.ActionId);
        Assert.False(hud.SelectingTarget);
    }

    [Fact]
    public void InvalidConfirmationKeepsSubmenuWithoutConsumingTurn()
    {
        BattleSession battle = Create();
        List<BattleInput> inputs = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: _ => throw new InvalidOperationException("Unavailable"));
        hud.Choose(1);
        hud.Navigate(MenuInput.Right);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingGuardianFormAction);
        Assert.False(hud.SelectingTarget);
        Assert.Equal(BattleSession.AtbMaximum, hud.SelectedActor!.Atb);
        Assert.Empty(inputs);
    }

    private static BattleHudController Hud(BattleSession battle, List<BattleInput> inputs)
    {
        return new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add);
    }

    private static BattleSession Create(bool enabled = true)
    {
        BattleNativeAction physical = new(8, 79, 3, 79)
        {
            MagicId = 0, AttackType = 1, Power = 14, HitCount = 1, TargetFlags = null, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        return new(new BattleDefinition
        {
            GuardianFormsEnabled = enabled,
            GuardianFormResources = enabled ? [new(2, physical, 12, new([0, 0]) { AnimationFrames = [] }) { ElementDefenses = [1000, 700, 800, 800, 800, 800, 800, 800], StatusResistances = [.. Enumerable.Repeat((byte)100, 40)] }] : [],
            GuardianForces = [new(2, 1000, 1000, true)],
            ActionDefinitions = [new(BattleAbility.GuardianForce, 2, "Renamed GF", BattleActionEffect.Damage, 10, false), new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 10, false)],
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, EquipmentIds = [2], Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce] },
                new() { Id = "other", Name = "Other", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
