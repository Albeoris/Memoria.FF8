using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticVisualPresentationTests
{
    [Fact]
    public void Visual_hold_has_a_distinct_release_and_restarts_the_dispatch_deadline()
    {
        DiagnosticBattleDriverRecipe recipe = new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.Magic, ActionId: 1), 2)
        {
            BaselineSeconds = 0,
            ExpectedNativeCommand = 2,
            ExpectedNativeAction = 1,
            DispatchTimeoutSeconds = 5,
            Presentation = new("command.json", "receipts", "test") { VisualSignalPath = "visual.json" }
        };
        DiagnosticBattleDriver driver = new(recipe, 0);
        DiagnosticDriverObservation At(double seconds, long frame) => new(seconds, frame, frame, true, true, false, []);
        driver.Advance(At(0, 0));
        driver.Advance(At(1, 1));
        Assert.True(driver.Advance(At(2, 2), true));
        driver.HoldVisualPresentation();
        Assert.False(driver.Advance(At(60, 3), true));
        Assert.Equal(DiagnosticDriverStage.AwaitVisualPresentation, driver.Stage);
        Assert.Empty(driver.Dispatches);
        driver.ReleaseVisualPresentation(60);
        Assert.False(driver.Advance(At(64, 4)));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        driver.ObserveDispatch(new(1, 1, "party-0", BattleAbility.Magic, 1, 2, 2, 2));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        Assert.Throws<InvalidOperationException>(() => driver.ReleaseVisualPresentation(65));
        Assert.Throws<NotSupportedException>(() => (recipe with { ExpectedNativeCommand = 1 }).Validate());
    }

    [Fact]
    public void Command_signal_cannot_release_the_independent_visual_gate()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-visual-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string command = Path.Combine(root, "command.json");
            string visual = Path.Combine(root, "visual.json");
            DiagnosticPresentationGate gate = new(new(command, root, "test") { VisualSignalPath = visual }, 2);
            DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            gate.Ready(10, 20, now);
            File.WriteAllText(command, JsonSerializer.Serialize(new { run_id = "test", start_at_utc = now.AddSeconds(2) }));
            Assert.False(gate.Released(now));
            Assert.True(gate.Released(now.AddSeconds(2)));
            Assert.False(gate.VisualReleased(now.AddSeconds(3)));
            gate.VisualReady(30, 40, now.AddSeconds(3), new { actor = 0, command = 2, action = 1, effect = 2 });
            Assert.False(gate.VisualReleased(now.AddSeconds(3)));
            File.WriteAllText(visual, JsonSerializer.Serialize(new { run_id = "test", start_at_utc = now.AddSeconds(5) }));
            Assert.False(gate.VisualReleased(now.AddSeconds(3)));
            Assert.True(gate.VisualReleased(now.AddSeconds(5)));
            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "visual-ready.json")));
            Assert.Equal(2, receipt.RootElement.GetProperty("native_identity").GetProperty("effect").GetInt32());
            Assert.Equal("visual-ready", receipt.RootElement.GetProperty("state").GetString());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void In_frame_admission_waits_for_its_own_signal_and_has_a_finite_deadline()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-visual-wait-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string visual = Path.Combine(root, "visual.json");
            DiagnosticPresentationGate gate = new(new(Path.Combine(root, "command.json"), root, "test") { VisualSignalPath = visual }, 2);
            gate.VisualReady(30, 40, DateTimeOffset.UtcNow, new { effect = 2 });
            Assert.ThrowsAny<OperationCanceledException>(() => gate.WaitForVisualRelease(TimeSpan.FromMilliseconds(20)));
            DateTimeOffset target = DateTimeOffset.UtcNow.AddMilliseconds(100);
            File.WriteAllText(visual, JsonSerializer.Serialize(new { run_id = "test", start_at_utc = target }));
            gate.WaitForVisualRelease(TimeSpan.FromSeconds(2));
            Assert.True(DateTimeOffset.UtcNow >= target);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
