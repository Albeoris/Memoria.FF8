using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudSavedStatusPresentationTests
{
    [Fact]
    public void SelectedStandaloneShowsExistingHasteAndZombieWithInstalledArtwork()
    {
        string source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_STATUS_SNAPSHOT") ?? string.Empty;
        Assert.SkipUnless(File.Exists(source), "Set MEMORIA_FF8_BATTLE_STATUS_SNAPSHOT to an optional standalone save with an active Haste party member and Zombie enemy.");
        string game = TestResources.GameDirectory;
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source));
        GameState saved = GameSession.Load(source);
        BattleSnapshot component = saved.Battle ?? throw new InvalidDataException("The selected standalone has no active battle.");
        BattleSession battle = BattleSession.Restore(component);
        BattleCombatant[] hasteParty = battle.Combatants.Where(actor => actor.IsPlayer && (battle.ReadStatusPresentation(actor.Id).Battle & 2) != 0).ToArray();
        BattleCombatant zombie = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive && (battle.ReadStatusPresentation(actor.Id).Saved & 64) != 0);
        Assert.NotEmpty(hasteParty);
        BattleHudController hud = new(battle, (_, _) => throw new InvalidOperationException("The preview must not submit an attack."), () => { }, () => { });
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas partyCanvas = new();
        string beforeDraw = JsonSerializer.Serialize(battle.Capture());
        view.Draw(partyCanvas, new UiTheme());
        Assert.Equal(hasteParty.Length, partyCanvas.TextDraws.Count(draw => draw.Text == "Haste" && draw.Bounds.X >= 496));
        Assert.Contains(partyCanvas.Fills, fill => fill.Bounds.X == 832 && fill.Color == new UiColor(240, 5, 58));
        Assert.Equal(beforeDraw, JsonSerializer.Serialize(battle.Capture()));
        SavePreview(view, game, "hud.png");

        int attack = hud.Commands.ToList().IndexOf(BattleAbility.Attack);
        Assert.True(attack >= 0, "The selected source needs a ready actor with the Attack command for the target preview.");
        hud.Choose(attack);
        Assert.True(hud.SelectingTarget, "The selected source must allow command and target selection without advancing gameplay.");
        hud.SelectTarget(zombie.Id, false);
        BattleUiTests.TestCanvas targetCanvas = new();
        beforeDraw = JsonSerializer.Serialize(battle.Capture());
        view.Draw(targetCanvas, new UiTheme());
        Assert.Contains(targetCanvas.TextDraws, draw => draw.Text == zombie.Name && draw.Bounds.X < 496);
        Assert.Contains(targetCanvas.TextDraws, draw => draw.Text == "Zombie" && draw.Bounds.X < 496);
        Assert.Contains(targetCanvas.Images, image => image.Source == new UiImageSource(UiImageKind.Icon, 278) && image.Bounds.X < 496);
        Assert.Equal(beforeDraw, JsonSerializer.Serialize(battle.Capture()));
        SavePreview(view, game, "target.png");
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_STATUS_EVIDENCE") is string directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "source.battle.json"), JsonSerializer.Serialize(component));
        }
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source)));
    }

    [Fact]
    public void OptionalInstalledPreviewComparesHasteSlowAndStop()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_STATUS_EVIDENCE") is string, "Set MEMORIA_FF8_BATTLE_STATUS_EVIDENCE to export the optional status palette comparison.");
        string game = TestResources.GameDirectory;
        BattleSession battle = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "haste", Name = "Haste", IsPlayer = true, NativeStatus1 = 2, InitialAtb = BattleSession.AtbMaximum / 2 },
                new() { Id = "slow", Name = "Slow", IsPlayer = true, NativeStatus1 = 4, InitialAtb = BattleSession.AtbMaximum / 2 },
                new() { Id = "stop", Name = "Stop", IsPlayer = true, NativeStatus1 = 8, InitialAtb = BattleSession.AtbMaximum / 2 },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.Normal);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        SavePreview(view, game, "status-palette-comparison.png");
    }

    private static void SavePreview(UiView view, string game, string name)
    {
        using Bitmap bitmap = new(960, 672, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
        using (BattlePreviewCanvas canvas = new(bitmap, game))
        {
            view.Draw(canvas, new UiTheme());
        }
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_STATUS_EVIDENCE") is string directory)
        {
            Directory.CreateDirectory(directory);
            bitmap.SavePng(Path.Combine(directory, name));
        }
    }
}
