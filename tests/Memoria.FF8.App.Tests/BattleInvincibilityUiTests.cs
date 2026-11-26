using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleInvincibilityUiTests
{
    [Theory]
    [InlineData("config-combat")]
    [InlineData("new-game-options")]
    public void Hardcore_settings_force_invincibility_rule_on_and_lock_it(string screen)
    {
        const string key = "Invincibility once per battle";
        UiPreferences normal = new();
        Assert.False(normal.InvincibilityOncePerBattle);
        Assert.True(normal.WithSetting(key, "On").InvincibilityOncePerBattle);
        UiPreferences hardcore = normal.WithSetting("Difficulty", "Hardcore", allowDifficultyChange: true);
        Assert.True(hardcore.InvincibilityOncePerBattle);
        Assert.Throws<InvalidOperationException>(() => hardcore.WithSetting(key, "Off"));

        MenuData data = new();
        data.SupportedActions.Add("config");
        data.Settings["Difficulty"] = "Hardcore";
        data.Settings[key] = "On";
        MenuController menu = new(() => data, _ => { });
        menu.Open(screen);
        Assert.False(Assert.Single(menu.Entries, entry => entry.Id == key).Enabled);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void Unavailable_targets_are_gray_and_confirmation_requires_an_eligible_recipient(bool group, bool allyUsedInvincibility, bool allowed)
    {
        ushort itemId = group ? (ushort)21 : (ushort)19;
        BattleNativeAction native = new(4, itemId, 7, itemId)
        {
            MagicId = 0, AttackType = 2, Power = 0, HitCount = 1, TargetFlags = group ? (byte)0 : (byte)0x10,
            AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 255,
            Statuses0 = 0, Statuses1 = 0x800, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null,
            RawRecord = [], SupplementalRecord = []
        };
        BattleActionDefinition item = new(BattleAbility.Item, itemId, "Invincibility item", BattleActionEffect.Status, 0, true, group) { NativeAction = native };
        BattleSession battle = new(new BattleDefinition
        {
            InvincibilityOncePerBattle = true,
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Item], NativeStatus1 = 0x800 },
                new() { Id = "ally", Name = "Ally", IsPlayer = true, NativeStatus1 = allyUsedInvincibility ? 0x800u : 0u },
                new() { Id = "enemy", Name = "Enemy" }
            ],
            ActionDefinitions = [item],
            Items = [new(itemId, 2)]
        }, BattleAtbMode.TurnBased);
        List<MenuSound> sounds = [];
        List<BattleInput> dispatched = [];
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn(), execute: dispatched.Add, readPresentationUi: BattleHudTestPresentation.Visible);
        hud.SoundRequested += sounds.Add;
        hud.Choose(Array.IndexOf(hud.Commands.ToArray(), BattleAbility.Item));
        hud.Choose(0);
        Assert.True(hud.SelectingTarget);
        Assert.True(hud.IsTargetUnavailable(battle.Combatants[0]));
        Assert.Equal(allyUsedInvincibility, hud.IsTargetUnavailable(battle.Combatants[1]));

        UiTheme theme = new();
        BattleUiTests.TestCanvas canvas = new();
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        view.Draw(canvas, theme);
        Assert.Contains(canvas.TextDraws, draw => draw.Text == "Caster" && draw.Color == theme.Muted);

        sounds.Clear();
        hud.SelectTarget("caster", true);
        Assert.Equal(allowed ? MenuSound.Confirm : MenuSound.Error, sounds[^1]);
        Assert.Equal(allowed ? 1 : 0, dispatched.Count);
        Assert.Equal(!allowed, hud.SelectingTarget);
        Assert.Equal(2, Assert.Single(battle.Items).Count);
    }
}
