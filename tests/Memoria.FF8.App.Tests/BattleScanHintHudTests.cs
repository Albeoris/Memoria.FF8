using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleScanHintHudTests
{
    [Theory]
    [InlineData(BattleScanHintMode.Off)]
    [InlineData(BattleScanHintMode.Targets)]
    [InlineData(BattleScanHintMode.Full)]
    public void Fire_hover_and_target_selection_only_use_disclosed_enemy_resistances(BattleScanHintMode mode)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition fire = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 1);
        BattleHudController hud = CreateHud(fire, 700, 1000, secondKnown: false, enemyStatus: 8192);
        hud.ScanHintMode = mode;
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Magic));
        Assert.True(hud.ShowingMagicList);
        UiTheme theme = new();
        var list = Draw(hud, theme);
        string weakness = TargetScanScreen.Percent(-100);
        UiColor expected = mode == BattleScanHintMode.Full ? BattleResistanceTone.Favorable.Color(theme) : theme.Foreground;
        Assert.Contains(list.TextDraws, draw => draw.Text == fire.Name && draw.Color == expected);
        Assert.Contains(list.TextDraws, draw => draw.Text == "7" && draw.Color == theme.Foreground);
        Assert.Equal(mode == BattleScanHintMode.Full, list.Texts.Contains(weakness));
        Assert.DoesNotContain("??", list.Texts);
        Assert.DoesNotContain(TargetScanScreen.Percent(200), list.Texts);
        hud.Choose(0);
        Assert.True(hud.SelectingTarget);
        var targets = Draw(hud, theme);
        Assert.Equal(mode != BattleScanHintMode.Off, targets.Texts.Contains(weakness));
        Assert.Equal(mode != BattleScanHintMode.Off, targets.Texts.Contains("??"));
        Assert.DoesNotContain(TargetScanScreen.Percent(200), targets.Texts);
        if (mode != BattleScanHintMode.Off)
        {
            Assert.Equal(2, targets.Images.Count(draw => draw.Source == new UiImageSource(UiImageKind.Icon, 288)));
            var enemy = hud.Session.Combatants[1];
            Assert.Same(hud.ResistanceBadges(enemy), hud.ResistanceBadges(enemy));
        }
        hud.ScanHintMode = BattleScanHintMode.Off;
        var plain = Draw(hud, theme);
        Assert.Equal(2, targets.Texts.Count(text => text == "Float"));
        Assert.Equal(plain.TextDraws.Where(draw => draw.Text == "Float"), targets.TextDraws.Where(draw => draw.Text == "Float"));
        foreach (string name in new[] { "first", "second" })
        {
            UiRect expectedBounds = Assert.Single(plain.TextDraws, draw => draw.Text == name).Bounds;
            UiRect actualBounds = Assert.Single(targets.TextDraws, draw => draw.Text == name).Bounds;
            Assert.Equal(expectedBounds, actualBounds with { Width = expectedBounds.Width });
        }
        Assert.Empty(hud.Session.QueuedActions);
        Assert.Equal(7, Assert.Single(hud.Session.Combatants[0].MagicStocks).Count);
    }

    [Theory]
    [InlineData(900, 1000, true, true)]
    [InlineData(900, 1000, false, false)]
    [InlineData(900, 850, true, false)]
    public void List_warns_only_when_every_eligible_enemy_is_known_and_immune_or_absorbing(int first, int second, bool known, bool warn)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        var fire = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 1);
        var hud = CreateHud(fire, first, second, known);
        hud.ScanHintMode = BattleScanHintMode.Full;
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Magic));
        UiTheme theme = new();
        var list = Draw(hud, theme);
        Assert.Contains(list.TextDraws, row => row.Text == fire.Name && row.Color == (warn ? BattleResistanceTone.Warning.Color(theme) : theme.Foreground));
        hud.Choose(0);
        Assert.True(hud.SelectingTarget);
    }

    [Fact]
    public void Non_elemental_magic_warns_against_invincible_targets_without_element_icons()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        var spell = assets.ActionDefinitions.First(action => action.Ability == BattleAbility.Magic && !action.Allies && action.NativeAction is { Element: 0, AttackType: 2, Power: > 0, Statuses0: 0, Statuses1: 0 });
        var hud = CreateHud(spell, 800, 800, secondKnown: true, enemyStatus: 0x800);
        hud.ScanHintMode = BattleScanHintMode.Full;
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Magic));
        UiTheme theme = new();
        Assert.Contains(Draw(hud, theme).TextDraws, row => row.Text == spell.Name && row.Color == BattleResistanceTone.Warning.Color(theme));
        Assert.Empty(hud.ResistanceBadges(hud.Session.Combatants[1]));
    }

    [Fact]
    public void Weapon_targeting_displays_installed_elemental_and_status_attack_junctions()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = vitals.Records[0];
        BattleActionDefinition sleep = assets.ActionDefinitions.First(action => action.Ability == BattleAbility.Magic && action.NativeAction is { Statuses1: 1, Power: 0 });
        byte[] junctions = character.Junction.MagicJunctions.ToArray();
        junctions[9] = 1;
        junctions[10] = (byte)sleep.Id;
        NativeMagicStock[] stocks = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        stocks[0] = new(1, 100);
        stocks[1] = new((byte)sleep.Id, 100);
        vitals = vitals with { Records = [character with { Junction = character.Junction with { MagicJunctions = junctions }, MagicInventory = stocks }, .. vitals.Records.Skip(1)] };
        var history = new NativeScannedModels(NativeInputConfiguration.OriginalInitHash, [0, 0, 0, 0, 0]).Add(16);
        BattleSession session = new(new BattleDefinition
        {
            NativeScannedModels = history,
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 0) },
                Enemy("first", 16, 700),
                Enemy("second", 17, 1000)
            ]
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, readPresentationUi: BattleHudTestPresentation.Visible);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        var targets = Draw(hud, new UiTheme());
        Assert.Equal(2, targets.Images.Count(draw => draw.Source == new UiImageSource(UiImageKind.Icon, 288)));
        Assert.Equal(2, targets.Images.Count(draw => draw.Source == new UiImageSource(UiImageKind.Icon, 279)));
        Assert.Contains(TargetScanScreen.Percent(-100), targets.Texts);
        Assert.Contains(TargetScanScreen.Percent(20), targets.Texts);
        Assert.Equal(2, targets.Texts.Count(label => label == "??"));
    }

    [Fact]
    public void Guardian_force_targeting_reads_the_summon_element()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        var ifrit = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.GuardianForce && action.Id == 2);
        var hud = CreateHud(ifrit, 1000, 900, secondKnown: true);
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.GuardianForce));
        hud.Choose(0);
        Assert.True(hud.SelectingTargetGroup);
        UiTheme theme = new();
        var targets = Draw(hud, theme);
        Assert.Contains(targets.TextDraws, row => row.Text == TargetScanScreen.Percent(200) && row.Color == BattleResistanceTone.Warning.Color(theme));
        Assert.Contains(targets.TextDraws, row => row.Text == TargetScanScreen.Percent(100) && row.Color == theme.Muted);
    }

    [Fact]
    public void Rating_visible_spell_rows_does_not_allocate_per_enemy()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition[] spells = assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.Magic).ToArray();
        var hud = CreateHud(spells[0], 900, 1000, secondKnown: true);
        var actor = hud.Session.Combatants[0];
        hud.ScanHintMode = BattleScanHintMode.Full;
        RateCatalog();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 0; frame < 100; frame++)
        {
            RateCatalog();
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, bytes);

        void RateCatalog()
        {
            foreach (BattleActionDefinition spell in spells)
            {
                hud.MagicHintTone(actor, spell);
            }
        }
    }

    [Fact]
    public void Render_magic_hover_and_multiple_guardian_force_statuses_when_output_is_requested()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_LIST_PREVIEW_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrEmpty(output), "Select an output directory for battle HUD previews.");
        Directory.CreateDirectory(output!);
        using BattleAssets assets = new(TestResources.GameDirectory);
        var fire = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 1);
        var doomtrain = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.GuardianForce && action.Id == 11);
        Render(fire, "fire", targets: false);
        Render(doomtrain, "doomtrain", targets: true);

        void Render(BattleActionDefinition action, string name, bool targets)
        {
            var hud = CreateHud(action, 700, 1000, secondKnown: !targets);
            hud.ScanHintMode = BattleScanHintMode.Full;
            hud.Choose(hud.Commands.ToList().IndexOf(action.Ability));
            if (targets)
            {
                hud.Choose(0);
                Assert.True(hud.SelectingTargetGroup);
            }
            UiView view = hud.BuildView();
            using SkiaSharp.SKBitmap bitmap = new(960, 672);
            using BattlePreviewCanvas canvas = new(bitmap, TestResources.GameDirectory);
            view.Resize(bitmap.Width, bitmap.Height);
            view.Draw(canvas, new UiTheme());
            bitmap.SavePng(Path.Combine(output!, name + ".png"));
        }
    }

    private static BattleHudController CreateHud(BattleActionDefinition action, int firstDefense, int secondDefense, bool secondKnown, uint enemyStatus = 0)
    {
        NativeScannedModels history = new(NativeInputConfiguration.OriginalInitHash, [0, 0, 0, 0, 0]);
        history = history.Add(16);
        int guardian = action.Ability == BattleAbility.GuardianForce ? action.Id : 2;
        BattleSession session = new(new BattleDefinition
        {
            NativeScannedModels = secondKnown ? history.Add(17) : history,
            ActionDefinitions = [action],
            NativeActions = [action.NativeAction!],
            NativeBattleSpeed = 2,
            GuardianForces = [new(guardian, 1000, 1000, true)],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [action.Ability], EquipmentIds = [guardian], GuardianForceCompatibility = new ushort[16], MagicStocks = [new(action.Id, 7)] },
                Enemy("first", 16, firstDefense) with { NativeStatus1 = enemyStatus },
                Enemy("second", 17, secondDefense) with { NativeStatus1 = enemyStatus }
            ]
        }, BattleAtbMode.TurnBased);
        return new(session, session.QueueAttack, () => { }, () => { }, readPresentationUi: BattleHudTestPresentation.Visible)
        {
            TargetModelBounds = id => new UiRect(id == "first" ? 160 : 520, 150, 60, 80)
        };
    }

    private static BattleCombatantDefinition Enemy(string id, int model, int fireDefense)
    {
        return new() { Id = id, Name = id, ModelId = model, ElementDefenses = [fireDefense, 800, 800, 800, 800, 800, 800, 800], NativeStatusResistances = Enumerable.Repeat((byte)120, 40).ToArray() };
    }

    private static BattleUiTests.TestCanvas Draw(BattleHudController hud, UiTheme theme)
    {
        BattleUiTests.TestCanvas canvas = new();
        hud.BuildView().Draw(canvas, theme);
        return canvas;
    }
}
