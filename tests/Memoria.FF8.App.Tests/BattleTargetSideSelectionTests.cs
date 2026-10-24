using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTargetSideSelectionTests
{
    [Fact]
    public void AttackStartsOnEnemiesAndCanSelectAlliesThroughKeyboardAndPointer()
    {
        BattleSession session = Create();
        List<(string Actor, string Target)> attacks = [];
        BattleHudController hud = new(session, (actor, target) => attacks.Add((actor, target)), () => { }, () => { });
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("enemy", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Left);
        Assert.Equal("caster", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Down);
        Assert.Equal("ally", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(("caster", "ally"), Assert.Single(attacks));
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("enemy", hud.SelectedTarget!.Id);
        hud.SelectTarget("caster", true);
        Assert.Equal(("caster", "caster"), attacks[1]);
    }

    [Fact]
    public void ExplicitSideSwitchChangesSidesImmediatelyWithMultipleEnemyColumns()
    {
        BattleDefinition definition = Create().Capture().Definition;
        BattleSession session = new(definition with
        {
            Combatants = [.. definition.Combatants, .. Enumerable.Range(1, 7).Select(index => new BattleCombatantDefinition { Id = $"extra-{index}", Name = $"Enemy {index}" })]
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { });
        Assert.False(hud.CanChangeTargetSide);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(8, hud.Targets.Count);
        Assert.True(hud.CanChangeTargetSide);
        hud.ChangeTargetSide();
        Assert.Equal("caster", hud.SelectedTarget!.Id);
        Assert.Equal(2, hud.Targets.Count);
        Assert.True(hud.CanChangeTargetSide);
        hud.ChangeTargetSide();
        Assert.Equal("enemy", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Cancel);
        Assert.False(hud.CanChangeTargetSide);
    }

    [Fact]
    public void AllyAttackSurvivesJsonRestoreAndContinues()
    {
        BattleSession session = Create();
        session.QueueAttack("caster", "ally");
        BattleSession restored = Restore(session);
        session.Tick();
        restored.Tick();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.Capture()), JsonSerializer.SerializeToElement(restored.Capture())));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MagicDefaultSideIsAPreferenceAndOppositeSideSubmissionRestores(bool healing)
    {
        BattleActionDefinition action = new(BattleAbility.Magic, 1, "Spell", healing ? BattleActionEffect.Heal : BattleActionEffect.Damage, 10, healing);
        BattleSession session = Create(action);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId));
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(healing ? "caster" : "enemy", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Right);
        Assert.Equal(healing ? "enemy" : "caster", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.False(hud.SelectingTarget);
        Restore(session);
    }

    [Fact]
    public void HealingItemCanBeQueuedAgainstAnEnemyAndRestored()
    {
        BattleActionDefinition action = new(BattleAbility.Item, 1, "Potion", BattleActionEffect.Heal, 10, true);
        BattleSession session = Create(action);
        session.QueueAction("caster", "enemy", BattleAbility.Item, action.Id);
        Restore(session);
    }

    [Fact]
    public void DrawCastKeepsItsEnemySourceAndAllowsHealingAnEnemyRecipient()
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Draw] },
                new() { Id = "source", Name = "Source", NativeDrawSlots = [new(1, 0), new(0, 0), new(0, 0), new(0, 0)] }
            ],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Cure", BattleActionEffect.Heal, 10, true)]
        }, BattleAtbMode.TurnBased);
        BattleInput? submitted = null;
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input =>
        {
            session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId, input.DrawCast, drawCastTargetId: input.DrawCastTargetId);
            submitted = input;
        });
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingDrawSource);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingDrawMode);
        hud.Navigate(MenuInput.Right);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingDrawCastRecipient);
        Assert.Equal("caster", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Right);
        Assert.Equal("source", hud.SelectedTarget!.Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.NotNull(submitted);
        Assert.Equal("source", submitted.TargetId);
        Assert.Equal("source", submitted.DrawCastTargetId);
        Restore(session);
    }

    [Theory]
    [InlineData(0x14, true)]
    [InlineData(0x54, true)]
    [InlineData(0x44, true)]
    [InlineData(0x18, false)]
    [InlineData(0x48, false)]
    [InlineData(0x50, false)]
    public void NativeSelectionModeControlsSideSwitching(byte flags, bool expected)
    {
        Assert.Equal(expected, NativeBattleLimitRules.CanSelectEitherSide(flags));
    }

    [Fact]
    public void InstalledLifeCanSelectLivingZombieEnemyButCannotSelectLivingOrdinaryEnemy()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition life = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.NativeAction?.AttackType == 5);
        BattleSession session = Create(life, zombie: true);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { });
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("enemy", Assert.Single(hud.SelectableTargets).Id);
        Assert.Equal("enemy", hud.SelectedTarget!.Id);
        BattleSession ordinary = Create(life);
        Assert.False(BattleSession.IsValidSelectedActionTarget(ordinary.Combatants[0], life, ordinary.Combatants[2]));
    }

    [Fact]
    public void InstalledCureAllowsEnemySelectionAndNativeSingleCastTargetGroup()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition cure = assets.ActionDefinitions.First(action => action.Ability == BattleAbility.Magic && action.NativeAction?.AttackType == 3 && BattleSession.CanSelectEitherTargetSide(action));
        BattleSession session = Create(cure, zombie: true);
        session.QueueAction("caster", "enemy", BattleAbility.Magic, cure.Id, magicTargets: ["enemy"]);
        Restore(session);
    }

    [Fact]
    public void FixedSelfAndFixedEnemyActionsKeepTheirNativeRestrictions()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition source = assets.ActionDefinitions.First(action => action.Ability == BattleAbility.Magic);
        foreach (byte flags in new byte[] { 0x18, 0x48 })
        {
            BattleActionDefinition action = source with { Allies = flags == 0x18, NativeAction = source.NativeAction! with { TargetFlags = flags } };
            BattleSession session = Create(action);
            BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { });
            hud.Navigate(MenuInput.Confirm);
            hud.Navigate(MenuInput.Confirm);
            string selected = hud.SelectedTarget!.Id;
            hud.Navigate(MenuInput.Left);
            Assert.Equal(selected, hud.SelectedTarget!.Id);
            Assert.All(hud.SelectableTargets, target => Assert.Equal(flags == 0x18 ? "caster" : "enemy", target.Id));
        }
    }

    private static BattleSession Create(BattleActionDefinition? action = null, bool zombie = false)
    {
        return new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [action?.Ability ?? BattleAbility.Attack], MagicStocks = action is null ? [] : [new(action.Id, 10)] },
                new() { Id = "ally", Name = "Ally", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", NativeStatus0 = zombie ? (ushort)0x40 : (ushort)0 }
            ],
            ActionDefinitions = action is null ? [] : [action],
            NativeActions = action?.NativeAction is { } native ? [native] : [],
            Items = action?.Ability == BattleAbility.Item ? [new(action.Id, 1)] : []
        }, BattleAtbMode.TurnBased);
    }

    private static BattleSession Restore(BattleSession session)
    {
        string json = JsonSerializer.Serialize(session.Capture());
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
    }
}
