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
        Assert.Equal(new NativeWorldGroundSelectionAlternative(0, 0), Assert.Single(cold.GroundSelection!.Alternatives));
        session.StartNewGame();
        NativeWorldProcess executed = cold.AdvanceMainLoopCallbacks(7) with { CameraCooldown = 46, GroundSelection = new([new(1, 4)]) };
        session.SetWorldProcess(executed);
        session.StartNewGame();
        Assert.Equal(executed, session.WorldProcess);
        GameSession restored = new(session.State);
        Assert.Equal(executed, restored.WorldProcess);
        restored.SetWorldProcess(restored.WorldProcess!.AdvanceMainLoopCallbacks(1));
        Assert.Equal(8u, restored.WorldProcess.MainLoopCounter);
        Assert.Equal(46, restored.WorldProcess.CameraCooldown);
    }

    [Fact]
    public void FreshGroundUsesProcessSelectionWithoutRetainingLoadedTerrain()
    {
        NativeWorldGroundIdentity original = new(1, 1, 0, 0);
        NativeWorldGroundIdentity loaded = new(2, 2, 1, 3);
        NativeWorldGroundSelectionState process = new([new(1, int.MinValue), new(0, null)]);
        NativeWorldGroundCacheState fresh = NativeWorldGroundCacheState.CreateFresh(loaded, processSelection: process);
        Assert.Equal(2, fresh.Alternatives.Length);
        Assert.Equal(1, fresh.Alternatives[0].PreferredGroup);
        Assert.Equal(int.MinValue, fresh.Alternatives[0].FailedSelections);
        Assert.Null(fresh.Alternatives[1].FailedSelections);
        Assert.All(fresh.Alternatives, alternative =>
        {
            Assert.Empty(alternative.Entries);
            Assert.Equal(loaded, alternative.CurrentGround);
            Assert.Equal(loaded, alternative.PreviousGround);
        });
        NativeWorldGroundCacheState retained = NativeWorldGroundCacheState.CreateFresh(original, processSelection: new([new(0, 3)]));
        NativeWorldGroundCacheState retainedFresh = NativeWorldGroundCacheState.CreateFresh(loaded, retained, process);
        Assert.Equal(new NativeWorldGroundSelectionAlternative(0, 3), Assert.Single(NativeWorldGroundSelectionState.FromCache(retainedFresh).Alternatives));
    }

    [Fact]
    public void GroundSelectionSurvivesSerializationAndMissingLegacyOwnerStaysUnknown()
    {
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess() with { GroundSelection = new([new(1, 7), new(0, null)]) };
        NativeWorldProcess restored = JsonSerializer.Deserialize<NativeWorldProcess>(JsonSerializer.Serialize(process))!;
        restored.Validate();
        Assert.Equal(process.GroundSelection!.Alternatives.ToArray(), restored.GroundSelection!.Alternatives.ToArray());
        JsonObject legacy = JsonNode.Parse(JsonSerializer.Serialize(process))!.AsObject();
        legacy.Remove("GroundSelection");
        NativeWorldProcess unknown = JsonSerializer.Deserialize<NativeWorldProcess>(legacy.ToJsonString())!;
        Assert.Null(unknown.GroundSelection);
        NativeWorldGroundCacheState fresh = NativeWorldGroundCacheState.CreateFresh(new(0, 0, 0, 0), processSelection: unknown.GroundSelection);
        Assert.Equal(new[] { 0, 1 }, fresh.Alternatives.Select(alternative => alternative.PreferredGroup).ToArray());
        Assert.All(fresh.Alternatives, alternative => Assert.Null(alternative.FailedSelections));
        Assert.Throws<InvalidDataException>(() => (process with { GroundSelection = new([new(2, 0)]) }).Validate());
    }

    [Fact]
    public void MissingGroundCallbackPublicationInvalidatesSelectionOnlyAfterCallbacks()
    {
        WorldState before = new();
        GameState departure = new() { World = before, WorldProcess = NativeWorldProcess.FromColdProcess() };
        Assert.NotNull(GameSession.PrepareWorldProcess(departure, before).GroundSelection);
        Assert.Null(GameSession.PrepareWorldProcess(departure, before with { Frame = 1 }).GroundSelection);
    }

    [Fact]
    public void GroundCallbackPublishesOnlySelectionPairsAndDeduplicatesCacheHistories()
    {
        NativeWorldGroundIdentity ground = new(0, 0, 0, 0);
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground) with
        {
            Alternatives = [new(1, [], ground, ground) { FailedSelections = 4 }, new(1, [ground], ground, ground) { FailedSelections = 4 }]
        };
        NativeFootWorldRuntimeState foot = new()
        {
            ProofVersion = 1,
            Rule = NativeFootWorldRuntimeState.FirstLoadRule,
            EntityType = 0,
            AnimationVariant = 0,
            MapDisplayMode = 0,
            CameraOffsetY = -341,
            CameraOffsetZ = -7168,
            ReloadSourceWorldFrame = null,
            GroundCache = cache
        };
        WorldState before = new() { NativeFootRuntime = foot };
        GameState departure = new() { World = before, WorldProcess = NativeWorldProcess.FromColdProcess() };
        NativeWorldProcess published = GameSession.PrepareWorldProcess(departure, before with { Frame = 1 });
        Assert.Equal(new NativeWorldGroundSelectionAlternative(1, 4), Assert.Single(published.GroundSelection!.Alternatives));
        NativeWorldGroundCacheState reloaded = NativeWorldGroundCacheState.CreateFresh(new(1, 1, 0, 0), processSelection: published.GroundSelection);
        Assert.Empty(Assert.Single(reloaded.Alternatives).Entries);
        Assert.Equal(4, reloaded.Alternatives[0].FailedSelections);
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
