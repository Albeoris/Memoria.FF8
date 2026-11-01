using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSavedTargetPerformanceTests
{
    [Fact]
    public void Standalone_target_cursor_uses_the_rendered_body_bounds()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_PERFORMANCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_PERFORMANCE_OUTPUT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && output is not null && directory is not null, "Select a battle fixture, installed resources and an output directory.");
        GameState source = GameSession.Load(input!);
        BattleSnapshot snapshot = Assert.IsType<BattleSnapshot>(source.Battle);
        BattleSession battle = BattleSession.Restore(snapshot);
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        UiRect viewport = new(0, 0, 1280, 720);
        using SKBitmap bitmap = new(1280, 720, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        BattleHudController hud = new(battle, (_, _) => Assert.Fail("Drawing must not submit an action."), () => { }, () => { })
        {
            TargetModelBounds = id => scene.GetActorBounds(id, viewport)
        };
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Magic));
        hud.Choose(0);
        Assert.True(hud.SelectingTarget);
        using (BattlePreviewCanvas canvas = new(bitmap, directory))
        {
            canvas.DrawRgba(scene.Render(battle, 1280, 720), 1280, 720, viewport);
            UiView view = hud.BuildView();
            view.Resize(1280, 720);
            view.Draw(canvas, new UiTheme());
        }
        BattleCombatant target = Assert.IsType<BattleCombatant>(hud.SelectedTarget);
        UiRect body = Assert.IsType<UiRect>(scene.GetActorBounds(target.Id, viewport));
        BattleUiTests.TestCanvas pointers = new();
        hud.BuildView().Draw(pointers, new UiTheme());
        Assert.Contains(pointers.Pointers, pointer => pointer.Y >= body.Y && pointer.Y <= body.Bottom && pointer.Right <= body.X);
        Assert.Equal(snapshot.Frame, battle.Frame);
        Directory.CreateDirectory(output!);
        bitmap.SavePng(Path.Combine(output!, "saved-magic-target.png"));
    }

    [Theory]
    [InlineData(BattleAbility.Attack)]
    [InlineData(BattleAbility.Magic)]
    public void Standalone_ready_menu_reports_draw_cost_without_advancing_gameplay(BattleAbility ability)
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(input), "Select a standalone battle fixture with a ready party member.");
        GameState state = GameSession.Load(input!);
        BattleSession battle = BattleSession.Restore(Assert.IsType<BattleSnapshot>(state.Battle));
        long frame = battle.Frame;
        BattleHudController hud = new(battle, (_, _) => Assert.Fail("Drawing the menu must not queue an attack."), () => { }, () => Assert.Fail("Drawing the menu must not advance gameplay."))
        {
            TargetModelBounds = _ => new UiRect(400, 120, 100, 200)
        };
        BattleCombatant actor = Assert.IsType<BattleCombatant>(hud.SelectedActor);
        Assert.True(battle.CanChoosePlayerCommand(actor.Id), "The supplied fixture must already have a ready party member.");
        Assert.False(hud.CommandMenuHidden);
        List<object> samples = [];
        BattleUiTests.TestCanvas canvas = new();
        UiTheme theme = new();
        Measure("commands");

        int commandIndex = hud.Commands.ToList().IndexOf(ability);
        Assert.True(commandIndex >= 0, $"The ready actor must offer {ability}.");
        hud.Choose(commandIndex);
        Assert.Equal(ability, hud.SelectedCommand);
        if (ability == BattleAbility.Magic)
        {
            Assert.True(hud.SelectingAction);
            Assert.NotEmpty(hud.ActionChoices);
            Measure("actions");
            hud.Choose(0);
        }
        Assert.True(hud.SelectingTarget);
        Assert.NotEmpty(hud.VisibleTargets);
        Measure("targets");
        Assert.Equal(frame, battle.Frame);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_PERFORMANCE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, $"target-performance-{ability.ToString().ToLowerInvariant()}.json"), JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
        }

        void Measure(string phase)
        {
            Draw();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (int index = 0; index < 10; index++)
            {
                Draw();
            }
            double milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            if (phase == "targets")
            {
                Assert.True(bytes < 64L * 1024 * 1024, $"Ten target draws allocated {bytes:N0} bytes.");
            }
            string[] targets = hud.SelectingTarget ? hud.VisibleTargets.Select(target => target.Id).ToArray() : [];
            samples.Add(new { Ability = ability.ToString(), Phase = phase, Actor = actor.Id, Command = hud.SelectedCommand.ToString(), Action = hud.SelectedAction?.Id, Targets = targets, Frame = frame, Draws = 10, Milliseconds = milliseconds, AllocatedBytes = bytes });
            Console.WriteLine($"{ability} {phase}: actor {actor.Id}, command {hud.SelectedCommand}, targets [{string.Join(", ", targets)}], 10 draws {milliseconds:F3} ms, {bytes:N0} bytes.");
            Assert.Equal(frame, battle.Frame);
        }

        void Draw()
        {
            canvas.Images.Clear();
            canvas.Texts.Clear();
            canvas.TextDraws.Clear();
            canvas.Pointers.Clear();
            canvas.Fills.Clear();
            canvas.Clips.Clear();
            UiView view = hud.BuildView();
            view.Resize(960, 672);
            view.Draw(canvas, theme);
        }
    }
}
