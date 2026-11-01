using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticPresentationGateTests
{
    [Fact]
    public void Presentation_waits_for_release_executes_once_and_holds_the_completed_tail()
    {
        DiagnosticBattleDriverRecipe recipe = new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.Magic, ActionId: 1), 2)
        {
            BaselineSeconds = 0,
            Presentation = new("signal.json", "receipts", "test") { TailSeconds = 3 }
        };
        DiagnosticBattleDriver driver = new(recipe, 0);
        DiagnosticDriverObservation At(double seconds, long frame, bool ready = true) => new(seconds, frame, frame, ready, true, false, []);
        Assert.False(driver.Advance(At(0, 0), true));
        Assert.False(driver.Advance(At(0.1, 1), true));
        Assert.Equal(DiagnosticDriverStage.AwaitPresentation, driver.Stage);
        Assert.False(driver.Advance(At(1, 2)));
        Assert.Empty(driver.Dispatches);
        Assert.True(driver.Advance(At(2, 3), true));
        Assert.False(driver.Advance(At(2.1, 4), true));
        driver.ObserveDispatch(new(1, 1, "party-0", BattleAbility.Magic, 1, 2, 2));
        driver.Advance(At(2.2, 5));
        driver.Advance(At(2.3, 6));
        driver.Advance(At(2.4, 7));
        Assert.Equal(DiagnosticDriverStage.PresentationHold, driver.Stage);
        Assert.Equal(1, driver.Execution);
        Assert.False(driver.Advance(At(5.3, 8), true));
        Assert.Equal(DiagnosticDriverStage.PresentationHold, driver.Stage);
        driver.Advance(At(5.5, 9));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Single(driver.Dispatches);
    }

    [Fact]
    public void Readiness_loss_during_hold_blocks_submission()
    {
        DiagnosticBattleDriverRecipe recipe = new(new(BattleInputKind.Attack, ActorId: "party-0", TargetId: "enemy-0"), 2) { BaselineSeconds = 0, Presentation = new("signal", "receipts", "test") };
        DiagnosticBattleDriver driver = new(recipe, 0);
        driver.Advance(new(0, 0, 0, true, true, false, []));
        driver.Advance(new(1, 1, 1, true, true, false, []));
        Assert.False(driver.Advance(new(2, 2, 2, false, true, false, []), true));
        Assert.Equal("PRESENTATION_READINESS_LOST", driver.Blocker);
    }

    [Theory]
    [InlineData("test", 5, true)]
    [InlineData("different", 5, false)]
    [InlineData("test", -1, false)]
    public void Signal_requires_matching_identity_and_a_future_UTC_target(string runId, int offsetSeconds, bool valid)
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-presentation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string signal = Path.Combine(root, "start.json");
            string receipts = Path.Combine(root, "receipts");
            DiagnosticPresentationGate gate = new(new(signal, receipts, "test"), 2);
            DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.False(gate.Released(now));
            gate.Ready(10, 20, now);
            using JsonDocument ready = JsonDocument.Parse(File.ReadAllText(Path.Combine(receipts, "ready.json")));
            Assert.Equal("ready", ready.RootElement.GetProperty("state").GetString());
            Assert.Equal(Environment.ProcessId, ready.RootElement.GetProperty("pid").GetInt32());
            Assert.Equal(2, ready.RootElement.GetProperty("expected_effect").GetInt32());
            File.WriteAllText(signal, JsonSerializer.Serialize(new { run_id = runId, start_at_utc = now.AddSeconds(offsetSeconds) }));
            if (valid)
            {
                Assert.False(gate.Released(now));
                Assert.False(gate.Released(now.AddSeconds(4)));
                Assert.True(gate.Released(now.AddSeconds(5)));
            }
            else
            {
                Assert.Throws<InvalidDataException>(() => gate.Released(now));
            }
            Assert.Throws<InvalidDataException>(() => new DiagnosticPresentationGate(new(signal, receipts, "test"), 2));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
