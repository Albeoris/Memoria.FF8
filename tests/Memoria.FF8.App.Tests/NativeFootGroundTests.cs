using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFootGroundTests
{
    [Fact]
    public void Reached_cavern_approach_queries_ordered_sliding_and_preserves_raw_velocity_salary_units()
    {
        string directory = GameDirectory();
        using WorldAssets assets = new(directory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(WorldTerrainSelection.ReplacementMask(0, 0));
        // Declared native geometry fixture at the observed boundary. It does not
        // fill the old164 snapshot's missing cache or claim authentic history.
        WorldGroundSelection initial = assets.LookupNativeGround(29980, -28845, replacements, [])!;
        List<WorldNativeGroundResult> outcomes = [];
        for (int group = 0; group < 2; group++)
        {
            NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(initial.Identity) with { Alternatives = [new(group, [], initial.Identity, initial.Identity) { FailedSelections = 0 }] };
            WorldNativeGroundResult ground = assets.ResolveNativeGround(29980, -28845, 31, 2, 691, null, cache, replacements, 0, false);
            outcomes.Add(ground);
            Console.WriteLine($"Native foot geometry group{group}: blocked={ground.Blocked}, XY=({ground.X},{ground.Z}), selectedYaw={ground.GroundYaw}.");
            Assert.NotEmpty(ground.Cache.Alternatives[0].Entries);
            Assert.Equal(ground.Blocked ? 1 : 0, ground.Cache.Alternatives[0].FailedSelections);
            Assert.Equal(ground.Blocked ? 0u : 3u, NativeWorldSalaryMovement.AcceptedVectorUnits(ground.Blocked ? 0 : 31, 0, ground.Blocked ? 0 : 2));
            if (!ground.Blocked)
            {
                Assert.NotEqual(0, ground.GroundYaw);
                Assert.NotEqual((30011, -28847), (ground.X, ground.Z));
                Assert.NotNull(ground.Center);
            }
        }
        Assert.Contains(outcomes, outcome => !outcome.Blocked);
        NativeWorldGroundCacheState ambiguous = NativeWorldGroundCacheState.CreateFresh(initial.Identity) with { Alternatives = [new(0, [], initial.Identity, initial.Identity) { FailedSelections = 0 }, new(1, [], initial.Identity, initial.Identity) { FailedSelections = 0 }] };
        string prior = JsonSerializer.Serialize(ambiguous);
        Assert.Throws<NotSupportedException>(() => assets.ResolveNativeGround(29980, -28845, 31, 2, 691, null, ambiguous, replacements, 0, false));
        Assert.Equal(prior, JsonSerializer.Serialize(ambiguous));

    }

    [Fact]
    public void Current_native_foot_cache_is_required_and_independent_callback_restore_matches()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameAssets assets = new(GameDirectory());
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.SetNativeEventOwners(session.WorldControls!.WithSalaryEnabled(true), session.SeeD!.WithSalaryUiEnabled(true));
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0) });
        using WorldMapScene scene = new(session, GameDirectory());
        Assert.All(scene.State.NativeFootRuntime!.GroundCache!.Alternatives, alternative => Assert.Empty(alternative.Entries));
        Assert.Null(scene.State.NativeFootRuntime.PreviousHorizontalInput);
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 3));
        Assert.Equal(1, scene.State.NativeFootRuntime.PreviousHorizontalInput);
        Assert.All(scene.State.NativeFootRuntime.GroundCache.Alternatives, alternative => Assert.Equal(0, alternative.FailedSelections));
        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession restored = new(saved);
        using WorldMapScene other = new(restored, GameDirectory());
        PlayerAction next = new(PlayerActionKind.Move, Horizontal: 1, Vertical: -1, Frames: 2);
        Assert.Equal(scene.Apply(next), other.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restored.State));
        Assert.Equal(scene.Render(320, 224), other.Render(320, 224));
        GameSession unknown = new(saved with { World = saved.World! with { NativeFootRuntime = saved.World.NativeFootRuntime! with { GroundCache = null } } });
        GameState before = unknown.State;
        Assert.Throws<NotSupportedException>(() => new WorldMapScene(unknown, GameDirectory()));
        Assert.Same(before, unknown.State);
        string json = JsonSerializer.Serialize(saved.World.NativeFootRuntime);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeFootWorldRuntimeState>(json.Replace("GroundCache", "MissingGroundCache", StringComparison.Ordinal)));
    }

    [Fact]
    public void Fresh_loader_clears_cache_and_preserves_proved_process_selector_and_failure_counter()
    {
        using WorldAssets assets = new(GameDirectory());
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(WorldTerrainSelection.ReplacementMask(0, 0));
        WorldGroundSelection ground = assets.LookupNativeGround(24576, -29406, replacements, [])!;
        NativeWorldGroundCacheState prior = NativeWorldGroundCacheState.CreateFresh(ground.Identity) with { Alternatives = [new(1, [ground.Identity], ground.Identity, ground.Identity) { FailedSelections = 4 }] };
        NativeWorldGroundCacheState fresh = NativeWorldGroundCacheState.CreateFresh(ground.Identity, prior);
        NativeWorldGroundAlternative selected = Assert.Single(fresh.Alternatives);
        Assert.Empty(selected.Entries);
        Assert.Equal(1, selected.PreferredGroup);
        Assert.Equal(4, selected.FailedSelections);
        Assert.Equal(ground.Identity, selected.CurrentGround);
        Assert.Equal(ground.Identity, selected.PreviousGround);
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        return directory;
    }
}
