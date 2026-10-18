using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleDamageFeedbackTests
{
    [Fact]
    public void Damage_digits_use_outlined_system_text_at_native_fixed_advances()
    {
        RecordingCanvas canvas = new();
        BattleDamageTextPresentation.Draw([new(148, 88, 0x808080, [57, 58, 59, 0]), new(32, 50, 0x04408040, [60, 61, 0])], canvas, new(20, 30, 640, 480));
        Assert.Equal(new[] { "1", "2", "3", "4", "5" }, canvas.Draws.Select(draw => draw.Text));
        Assert.Equal(new float[] { 316, 332, 348, 84, 100 }, canvas.Draws.Select(draw => draw.Bounds.X));
        Assert.All(canvas.Draws, draw => Assert.Equal(new UiColor(0, 0, 0, draw.Color.A), draw.Outline));
        Assert.Equal(new UiColor(255, 255, 255), canvas.Draws[0].Color);
        Assert.Equal(new UiColor(128, 255, 128, 128), canvas.Draws[3].Color);
    }

    [Fact]
    public void Miss_and_double_triple_announcements_preserve_the_original_labels_and_offsets()
    {
        RecordingCanvas canvas = new();
        BattleDamageTextPresentation.Draw([new(152, 88, 0x808080, [16, 0]), new(160, 88, 0x808080, [69, 0])], canvas, new(0, 0, 320, 240));
        Assert.Equal(new[] { "Miss", "TRIPLE!!", "DOUBLE!" }, canvas.Draws.Select(draw => draw.Text));
        Assert.Equal(new float[] { 144, 136, 136 }, canvas.Draws.Select(draw => draw.Bounds.X));
        Assert.Equal(new float[] { 88, 88, 76 }, canvas.Draws.Select(draw => draw.Bounds.Y));
    }

    [Fact]
    public void Named_standalone_continues_to_a_real_hit_and_restores_the_same_text_and_next_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_DAMAGE_FEEDBACK_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DAMAGE_FEEDBACK_EVIDENCE");
        Assert.SkipUnless(source is not null && File.Exists(source) && !string.IsNullOrWhiteSpace(evidence), "An optional standalone battle snapshot and distinct evidence directory are required.");
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed game resources are required.");
        Directory.CreateDirectory(evidence!);
        BattleSession battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(source!)) ?? throw new InvalidDataException("The standalone battle is empty."));
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleScenePresentation scene = new(assets, battle.Capture().Definition, true);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        string? magic = Environment.GetEnvironmentVariable("MEMORIA_FF8_DAMAGE_FEEDBACK_MAGIC");
        bool captured = false;
        for (int tick = 0; tick < 300 && !captured; tick++)
        {
            if (battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.IsReady && actor.IsAlive) is { } ready)
            {
                BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive && !actor.Hidden && !actor.Untargetable);
                if (magic is null)
                {
                    battle.QueueAttack(ready.Id, target.Id);
                }
                else
                {
                    battle.QueueAction(ready.Id, target.Id, BattleAbility.Magic, int.Parse(magic, System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            battle.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            if (battle.NativeGraphics!.Effects.TextDraws.IsEmpty)
            {
                continue;
            }
            string standalone = Path.Combine(evidence!, "hit-standalone.battle.json");
            File.WriteAllText(standalone, JsonSerializer.Serialize(battle.Capture()));
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(standalone))!);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            using Bitmap output = new(960, 720);
            using (BattlePreviewCanvas canvas = new(output, TestResources.GameDirectory))
            {
                UiRect viewport = new(0, 0, 960, 720);
                canvas.DrawRgba(scene.Render(battle, 960, 720), 960, 720, viewport);
                scene.DrawEffects(battle, canvas, viewport);
            }
            output.Save(Path.Combine(evidence!, "hit.png"), ImageFormat.Png);
            File.WriteAllText(Path.Combine(evidence!, "hit-text.json"), JsonSerializer.Serialize(new { battle.Frame, battle.NativeHostCallbacks, battle.NativeGraphics.Effects.TextDraws }));
            battle.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            restored.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            File.WriteAllText(Path.Combine(evidence!, "after-hit-standalone.battle.json"), JsonSerializer.Serialize(battle.Capture()));
            captured = true;
        }
        Assert.True(captured, "The bounded continuation must publish actual native contact text.");
        for (int tick = 0; tick < 180 && (battle.ActiveAction is not null || battle.NativeGraphics!.Effects.PublishedCount != 0); tick++)
        {
            battle.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        }
        Assert.Null(battle.ActiveAction);
        Assert.Equal(0, battle.NativeGraphics!.Effects.PublishedCount);
        string completed = Path.Combine(evidence!, "completed-standalone.battle.json");
        File.WriteAllText(completed, JsonSerializer.Serialize(battle.Capture()));
        BattleSession completedRestore = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(completed))!);
        battle.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        completedRestore.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(completedRestore.Capture()));
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(string Text, UiRect Bounds, UiColor Color, UiColor Outline)> Draws { get; } = [];

        public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
        {
            Draws.Add((text, bounds, color, outline));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            throw new InvalidOperationException("Damage values require an outline.");
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            throw new InvalidOperationException("Damage values require an outline.");
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
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
