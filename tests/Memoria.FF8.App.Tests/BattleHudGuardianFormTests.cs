using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudGuardianFormTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Guardian_form_submenu_uses_the_same_native_blinking_arrow(bool enabled)
    {
        BattleSession battle = Create(enabled);
        BattleHudController hud = Hud(battle, []);
        hud.Choose(1);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas first = new();
        view.Draw(first, new UiTheme());
        Assert.DoesNotContain(first.Texts, text => text.Contains('>'));
        if (!enabled)
        {
            Assert.Empty(first.Images);
            return;
        }
        var arrow = Assert.Single(first.Images);
        Assert.Equal(new UiImageSource(UiImageKind.Icon, 6, 5), arrow.Source);
        hud.AdvancePresentation(TimeSpan.FromSeconds(2.125 / BattlePresentationTiming.CallbacksPerSecond));
        BattleUiTests.TestCanvas next = new();
        view.Draw(next, new UiTheme());
        Assert.Equal(new UiImageSource(UiImageKind.Icon, 6, 0), Assert.Single(next.Images).Source);
        Assert.Equal(arrow.Bounds, Assert.Single(next.Images).Bounds);
        hud.Navigate(MenuInput.Right);
        BattleUiTests.TestCanvas submenu = new();
        view.Draw(submenu, new UiTheme());
        Assert.Empty(submenu.Images);
        Assert.Equal("Summon", Assert.Single(hud.ChoiceLabels));
    }

    [Fact]
    public void FinisherSkipUsesRemappableEscapeLeftWithoutDedicatedShortcut()
    {
        KeyboardInput keyboard = new();
        Assert.DoesNotContain(ControlsProfile.Definitions, definition => definition.Action == ControlAction.GuardianFormSkip);
        Assert.Empty(keyboard.Profile.GetBindings(ControlAction.GuardianFormSkip, ControlDevice.Keyboard));
        Assert.Empty(keyboard.Profile.GetBindings(ControlAction.GuardianFormSkip, ControlDevice.Controller));
        keyboard.Read(default, true);
        keyboard.Read(new KeyboardState(Keys.D), true);
        Assert.True(keyboard.GuardianFormSkipRequested);
        Assert.False(keyboard.SpeedToggleRequested);
        Assert.False(keyboard.GuardianBoostRequested);
        keyboard.Read(new KeyboardState(Keys.D), true);
        Assert.False(keyboard.GuardianFormSkipRequested);
        keyboard.Profile = keyboard.Profile.WithBinding(ControlAction.CameraLeft, ControlDevice.Keyboard, new([Keys.P], []));
        keyboard.Read(default, true);
        keyboard.Read(new KeyboardState(Keys.P), true);
        Assert.True(keyboard.GuardianFormSkipRequested);
        keyboard.Read(default, true);
        keyboard.Read(new KeyboardState(Keys.F8), true);
        Assert.False(keyboard.GuardianFormSkipRequested);
    }

    [Fact]
    public void FinisherSkipUsesControllerEscapeLeftAndIgnoresOldProfileShortcut()
    {
        string path = TestResources.ArtifactFile("old-finisher-controls.json");
        ControlsProfile.CreateDefault().Save(path);
        string json = File.ReadAllText(path);
        json = json.Replace("\"Bindings\": [", "\"Bindings\": [{\"Action\":\"GuardianFormSkip\",\"Device\":\"Keyboard\",\"Chord\":{\"Keys\":[\"F8\"],\"Buttons\":[]}},");
        File.WriteAllText(path, json);
        KeyboardInput keyboard = new() { Profile = ControlsProfile.Load(path) };
        Assert.Empty(keyboard.Profile.GetBindings(ControlAction.GuardianFormSkip, ControlDevice.Keyboard));
        keyboard.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, []), true);
        keyboard.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 1, 0, Buttons.LeftTrigger), true);
        Assert.True(keyboard.GuardianFormSkipRequested);
        Assert.Equal((ushort)1, (ushort)(keyboard.HeldFieldButtons & 1));
        keyboard.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 1, 0, Buttons.LeftTrigger), true);
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
        Assert.Equal("Renamed GF", Assert.Single(hud.ChoiceLabels));
        Assert.Equal("1000", hud.ChoiceValue(hud.SelectedAction!));
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
        BattleSession original = Create(level: 1);
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
        Assert.Equal("enemy", Assert.Single(hud.Targets).Id);
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
    public void IfritCanSelectHeavyStrikeFromLevel15AndOtherCharactersCannotSummonWhileHeIsOut()
    {
        BattleSession original = Create(level: 15);
        BattleSession battle = BattleSession.Restore(original.Capture() with
        {
            GuardianForms = new(true, 1, [new("actor", 2, "ifrit-v1", BattleGuardianFormPhase.Active, 1, 100) { EntryCompleted = true }])
        });
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(battle, inputs);
        Assert.Equal(new[] { "Heavy Strike", "Fire", "Hell Fire" }, hud.ChoiceLabels);
        hud.Choose(0);
        Assert.Equal("Heavy Strike", hud.TargetActionCaption);
        Assert.Equal("enemy", Assert.Single(hud.Targets).Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal((int)BattleGuardianFormAction.HeavyStrike, Assert.Single(inputs).ActionId);

        hud.SelectParty("other");
        hud.Choose(1);
        Assert.Empty(hud.ActionChoices);
        Assert.False(hud.SelectingGuardianFormAction);
        Assert.DoesNotContain(inputs, input => input.Kind == BattleInputKind.GuardianFormActivate || input.Ability == BattleAbility.GuardianForce);
        Assert.True(battle.CanChoosePlayerCommand("other"));
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
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: _ => throw new InvalidOperationException("Unavailable"), readPresentationUi: BattleHudTestPresentation.Visible);
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
        return new(battle, (_, _) => { }, () => { }, () => { }, execute: inputs.Add, readPresentationUi: BattleHudTestPresentation.Visible);
    }

    private static BattleSession Create(bool enabled = true, int level = 1)
    {
        BattleNativeAction physical = new(8, 79, 3, 79)
        {
            MagicId = 0, AttackType = 1, Power = 14, HitCount = 1, TargetFlags = null, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleNativeAction heavy = physical with { ActionId = 59, KernelIndex = 59, Power = 20 };
        BattleNativeAction fire = physical with { Command = 2, ActionId = 1, KernelSection = 1, KernelIndex = 1, MagicId = 2, AttackType = 2, TargetFlags = 0x50, Name = "Fire" };
        BattleNativeAction ifrit = physical with { Command = 254, ActionId = 66, KernelSection = 2, KernelIndex = 2, AttackType = 11, TargetFlags = 0x40, Name = "Hell Fire", RawRecord = [.. new byte[128]] };
        BattleNativeAction quezacotl = ifrit with { ActionId = 64, KernelIndex = 0, Name = "Thunder Storm" };
        BattleGuardianFormActionBinding attackBinding = new(BattleGuardianFormAction.Attack, "Attack", BattleGuardianFormActionSource.Physical, 79, 12);
        BattleGuardianFormActionBinding fireBinding = new(BattleGuardianFormAction.Fire, "Fire", BattleGuardianFormActionSource.Magic, 1, 11, BattleGuardianFormActionCost.Innate);
        BattleGuardianFormActionBinding heavyBinding = new(BattleGuardianFormAction.HeavyStrike, "Heavy Strike", BattleGuardianFormActionSource.Physical, 59, 12);
        return new(new BattleDefinition
        {
            GuardianFormsEnabled = enabled,
            NativeBattleSpeed = 0,
            NativeActions = [physical, heavy, fire, ifrit, quezacotl],
            GuardianFormResources = enabled ? [new(2, physical, 12, new([0, 0]) { AnimationFrames = [] }) { InnateBindings = BattleGuardianFormProfiles.BindIfritInnateActions(attackBinding, fireBinding, heavyBinding), ElementDefenses = [1000, 700, 800, 800, 800, 800, 800, 800], StatusResistances = [.. Enumerable.Repeat((byte)100, 40)] }] : [],
            GuardianForces = [new(2, 1000, 1000), new(0, 1000, 1000)],
            ActionDefinitions = [new(BattleAbility.GuardianForce, 2, "Renamed GF", BattleActionEffect.Damage, 10, false) { NativeAction = ifrit, Level = level }, new(BattleAbility.GuardianForce, 0, "Quezacotl", BattleActionEffect.Damage, 10, false) { NativeAction = quezacotl }, new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Damage, 10, false) { NativeAction = fire }],
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, EquipmentIds = [2], Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce], GuardianForceCompatibility = new ushort[16] },
                new() { Id = "other", Name = "Other", IsPlayer = true, MaximumHp = 100, CurrentHp = 100, InitialAtb = BattleSession.AtbMaximum, EquipmentIds = [0], Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce], GuardianForceCompatibility = new ushort[16] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 },
                new() { Id = "reserve", Name = "Reserve", InitialHidden = true, InitialUntargetable = true }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
