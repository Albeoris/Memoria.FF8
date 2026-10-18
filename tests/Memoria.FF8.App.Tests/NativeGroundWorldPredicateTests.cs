using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGroundWorldPredicateTests
{
    [Fact]
    public void GroundTypeMatchesOnlyTheLowOperandByte()
    {
        GameSession session = UniformEntry();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        using WorldAssets assets = new(TestResources.GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(scene.State.ReplacementMask);
        WorldGroundSelection ground = assets.LookupNativeGround(29980, -28845, replacements, [])!;
        Assert.NotNull(ground);
        WorldState candidate = WithCache(scene.State, NativeWorldGroundCacheState.CreateFresh(ground.Identity));
        byte type = ground.Ground.Triangle.GroundType;
        string before = JsonSerializer.Serialize(session.State);
        string candidateBefore = JsonSerializer.Serialize(candidate);
        bool flagAbsent = (ground.Ground.Triangle.TextureFlags & 8) == 0;
        Assert.Equal(!flagAbsent, scene.EvaluateWorldEventPredicate(candidate, 0, session.State.SavedWorld!, 0xFF32, 1));
        Assert.True(scene.EvaluateWorldEventPredicate(candidate, 0, session.State.SavedWorld!, 0xFF32, 0x100));
        Assert.True(scene.EvaluateWorldEventPredicate(candidate, 0, session.State.SavedWorld!, 0xFF33, type));
        Assert.False(scene.EvaluateWorldEventPredicate(candidate, 0, session.State.SavedWorld!, 0xFF33, unchecked((byte)(type + 1))));
        Assert.True(scene.EvaluateWorldEventPredicate(candidate, 0, session.State.SavedWorld!, 0xFF33, (ushort)(0xAB00 | type)));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Equal(candidateBefore, JsonSerializer.Serialize(candidate));
    }

    [Fact]
    public void SavedCoastCallbackContinuesAndEvaluatesGroundTypeTen()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeGroundWorldPredicateTests.1")));
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        long frame = scene.State.Frame;
        Assert.False(scene.EvaluateWorldEventPredicate(scene.State, 0, session.State.SavedWorld!, 0xFF33, 10));
        Assert.False(scene.EvaluateWorldEventPredicate(scene.State, 0, session.State.SavedWorld!, 0xFF33, 0xFF0A));
        Assert.True(scene.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(frame + 1, scene.State.Frame);
    }

    [Fact]
    public void DivergentGroundAlternativesRejectWithoutMutation()
    {
        GameSession session = UniformEntry();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        using WorldAssets assets = new(TestResources.GameDirectory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(scene.State.ReplacementMask);
        WorldGroundSelection first = assets.LookupNativeGround(29980, -28845, replacements, [])!;
        Assert.NotNull(first);
        (int X, int Z)[] coordinates = [(12884, -26807), (24576, -29406), (0, 0), (65536, -65536), (-65536, -65536)];
        WorldGroundSelection? second = coordinates.Select(position => assets.LookupNativeGround(position.X, position.Z, replacements, [])).FirstOrDefault(ground => ground is not null && ground.Ground.Triangle.GroundType != first.Ground.Triangle.GroundType);
        Assert.NotNull(second);
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(first.Identity) with
        {
            Alternatives = [new(0, [], first.Identity, first.Identity), new(1, [], second.Identity, second.Identity)]
        };
        WorldState candidate = WithCache(scene.State, cache);
        string before = JsonSerializer.Serialize(session.State);
        string candidateBefore = JsonSerializer.Serialize(candidate);
        Assert.Throws<NotSupportedException>(() => scene.EvaluateWorldEventPredicate(candidate, 0, session.State.SavedWorld!, 0xFF33, first.Ground.Triangle.GroundType));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Equal(candidateBefore, JsonSerializer.Serialize(candidate));
    }

    [Fact]
    public void MissingAcceptedGroundOwnerRejectsWithoutMutation()
    {
        GameSession session = UniformEntry();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        WorldState candidate = WithCache(scene.State, null);
        string before = JsonSerializer.Serialize(session.State);
        string candidateBefore = JsonSerializer.Serialize(candidate);
        Assert.Throws<NotSupportedException>(() => scene.EvaluateWorldEventPredicate(candidate, 0, session.State.SavedWorld!, 0xFF33, 10));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Equal(candidateBefore, JsonSerializer.Serialize(candidate));
    }

    private static WorldState WithCache(WorldState state, NativeWorldGroundCacheState? cache)
    {
        Assert.NotNull(state.NativeFootRuntime);
        return state with { NativeFootRuntime = state.NativeFootRuntime with { GroundCache = cache } };
    }

    private static GameSession UniformEntry()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed original world resources are required.");
        // Declared loader fixture using original initialization and spawn1.
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameAssets resources = new(TestResources.GameDirectory);
        session.InitializeNewGameCharacterVitals(resources.ReadNewGameCharacterVitals());
        session.Restore(session.State with
        {
            Scene = SceneKind.WorldMap,
            WorldArrival = new WorldTransition(1, 1, -1),
            WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 135),
            SavedWorld = session.State.SavedWorld! with { Slot4 = new NativeSavedWorldVehicleSlot(255, new NativeSavedWorldPose(12884, -26807, -304, 2699)) }
        });
        return session;
    }
}
