using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldTerrainReplayTests
{
    [Fact]
    public void Regenerated_authentic_train_checkpoint_matches_cold_replay_full_state_pixels_and_next_inputs()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }
        string evidence = Path.Combine(directory!.FullName, "../fixtures");
        string path = Path.Combine(evidence, "world-terrain-train-current-checkpoint.json");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        GameState source = GameSession.Load(path);
        Stopwatch watch = Stopwatch.StartNew();
        GameState replayed = GameWorld.RecreateSave(GameSession.LoadReplaySource(path), game);
        watch.Stop();
        EqualState(source, replayed);
        GameSession direct = new(source);
        GameSession independent = new(replayed);
        using GameWorld world = new(direct, game, persistReplayJournal: false);
        using GameWorld replay = new(independent, game, persistReplayJournal: false);
        world.EnsureField();
        replay.EnsureField();
        world.SetRenderSize(640, 448);
        replay.SetRenderSize(640, 448);
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
        foreach (PlayerAction input in new PlayerAction[] { new(PlayerActionKind.Wait), new(PlayerActionKind.Move, Horizontal: 1) })
        {
            Assert.Equal(world.Apply(input), replay.Apply(input));
            EqualState(direct.State, independent.State);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
        }
        var proof = new { Baseline = "main53bba7db plus world terrain/forest/ladder changes", Implementations = new[] { "Memoria current checkpoint continuation", "Memoria cold-process NewGame causal reconstruction" }, OriginalFF8Parity = "Separate156 installed original permission/disembark/height measurements; replay/pixels do not establish original GPU parity", SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), SourceBytes = new FileInfo(path).Length, SourceField = source.FieldId, JournalRecords = source.ReplayJournal!.EntryCount, RecreateSeconds = watch.Elapsed.TotalSeconds, WholeStateExceptJournal = true, CpuPixels640x448 = true, NextWaitAndMoveStateVmPixels = true };
        File.WriteAllText(Path.Combine(evidence, "world-terrain-train-current-proof.json"), JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void EqualState(GameState left, GameState right)
    {
        JsonObject a = JsonNode.Parse(JsonSerializer.Serialize(left))!.AsObject();
        JsonObject b = JsonNode.Parse(JsonSerializer.Serialize(right))!.AsObject();
        a.Remove("ReplayJournal");
        b.Remove("ReplayJournal");
        Assert.True(JsonNode.DeepEquals(a, b));
    }
}
