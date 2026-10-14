using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldProcessTests
{
    [Fact]
    public void ColdProcessOwnsZeroGlobalsAndWarmNewGamePreservesTheirExecutedHistory()
    {
        GameSession session = GameSession.CreateColdProcess();
        NativeWorldProcess cold = session.WorldProcess!;
        Assert.True(cold.IsColdProcess);
        session.StartNewGame();
        Assert.True(session.ReplayJournal!.IsComplete);
        NativeWorldProcess executed = cold.AdvanceMainLoopCallbacks(7) with { CameraCooldown = 46 };
        session.SetWorldProcess(executed);
        session.StartNewGame();
        Assert.Equal(executed, session.WorldProcess);
        Assert.False(session.ReplayJournal!.IsComplete);
        Assert.Contains("main-loop counter", session.ReplayJournal.UnavailableReason);
        GameSession restored = new(session.State);
        Assert.Equal(executed, restored.WorldProcess);
        restored.SetWorldProcess(restored.WorldProcess!.AdvanceMainLoopCallbacks(1));
        Assert.Equal(8u, restored.WorldProcess.MainLoopCounter);
        Assert.Equal(46, restored.WorldProcess.CameraCooldown);
    }

    [Fact]
    public void CounterUsesUnsignedDwordWrappingAndNeverResetsTheCooldown()
    {
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess().AdvanceMainLoopCallbacks(uint.MaxValue) with { CameraCooldown = -19 };
        NativeWorldProcess next = process.AdvanceMainLoopCallbacks(1);
        Assert.Equal(0u, next.MainLoopCounter);
        Assert.Equal(4294967296L, next.MainLoopCallbacks);
        Assert.Equal(-19, next.CameraCooldown);
        Assert.False(next.IsColdProcess);
        next.Validate();
        Assert.Throws<ArgumentOutOfRangeException>(() => next.AdvanceMainLoopCallbacks(-1));
        Assert.Throws<InvalidDataException>(() => (next with { MainLoopCounter = 2 }).Validate());
    }

    [Fact]
    public void MissingProcessStateAndMissingRequiredMembersStayUnknown()
    {
        GameSession unknown = new(new GameState());
        Assert.Null(unknown.WorldProcess);
        string json = JsonSerializer.Serialize(NativeWorldProcess.FromColdProcess());
        foreach (string member in new[] { "Version", "Rule", "MainLoopCounter", "MainLoopCallbacks", "CameraCooldown" })
        {
            JsonObject missing = JsonNode.Parse(json)!.AsObject();
            missing.Remove(member);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldProcess>(missing.ToJsonString()));
        }
        JsonObject state = JsonNode.Parse(JsonSerializer.Serialize(new GameState()))!.AsObject();
        state.Remove("WorldProcess");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameState>(state.ToJsonString()));
    }

    [Fact]
    public void ForestCameraClampsThenRestoresWithTheNativeFinalOvershoot()
    {
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess();
        NativeForestCameraResult entered = process.StepNormalFootCamera(-112, 0);
        Assert.Equal((short)-118, entered.Pitch);
        NativeForestCameraResult exit1 = entered.Process.StepNormalFootCamera(entered.Pitch, 9);
        NativeForestCameraResult exit2 = exit1.Process.StepNormalFootCamera(exit1.Pitch, 9);
        Assert.Equal((short)-114, exit1.Pitch);
        Assert.Equal((short)-110, exit2.Pitch);
        Assert.Equal(-2, exit2.Process.CameraCooldown);
        Assert.Equal((short)-110, exit2.Process.StepNormalFootCamera(exit2.Pitch, 9).Pitch);
        NativeForestCameraResult clamped = process.StepNormalFootCamera(-510, 5);
        Assert.Equal((short)-512, clamped.Pitch);
        Assert.Equal((short)-512, clamped.Process.StepNormalFootCamera(clamped.Pitch, 5).Pitch);
        Assert.Equal((short)0, process.StepNormalFootCamera(10, 0).Pitch);
    }

    [Fact]
    public void CollisionCooldownAndHeldInputDeductionsFollowNativeBranchOrder()
    {
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess();
        NativeForestCameraResult collision = process.StepNormalFootCamera(-112, 9, collision: true, held8: true, held11: true, held13: true);
        Assert.Equal((short)-118, collision.Pitch);
        Assert.Equal(46, collision.Process.CameraCooldown);
        NativeForestCameraResult held = collision.Process.StepNormalFootCamera(collision.Pitch, 9, held13: true);
        Assert.Equal(collision.Pitch, held.Pitch);
        Assert.Equal(36, held.Process.CameraCooldown);
        NativeForestCameraResult forestCollision = process.StepNormalFootCamera(-112, 0, collision: true);
        Assert.Equal(0, forestCollision.Process.CameraCooldown);
        NativeForestCameraResult wrap = (process with { CameraCooldown = int.MinValue }).StepNormalFootCamera(-112, 9, held8: true);
        Assert.Equal(int.MaxValue - 1, wrap.Process.CameraCooldown);
    }
}
