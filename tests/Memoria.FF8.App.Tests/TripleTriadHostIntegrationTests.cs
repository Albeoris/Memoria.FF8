using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadHostIntegrationTests
{
    [Fact]
    public void OriginalMartineChallengeRestoresAndCompletesTheNativeHost()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        string source = Path.Combine(Repository(), "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Original diagnostic standalone and installed resources are required.");
        string output = OutputDirectory();
        string migrated = Path.Combine(output, "martine-field621-source-migrated.json");
        JsonObject root = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        root["FieldBars"] = null;
        root["EventState"]!["FieldBars"] = null;
        root["CardInventory"]!["TripleTriadRandomState"] = 0;
        root["EventState"]!["CardInventory"]!["TripleTriadRandomState"] = 0;
        root["CardInventory"]!["Rule"] = NativeCardInventory.InitializationRule;
        root["EventState"]!["CardInventory"]!["Rule"] = NativeCardInventory.InitializationRule;
        root["CardInventory"]!["TripleTriadStatistics"] = JsonSerializer.SerializeToNode(new TripleTriadStatistics(0, 0, 0));
        root["EventState"]!["CardInventory"]!["TripleTriadStatistics"] = JsonSerializer.SerializeToNode(new TripleTriadStatistics(0, 0, 0));
        root["CardInventory"]!["CollectionFlags"] = 0;
        root["EventState"]!["CardInventory"]!["CollectionFlags"] = 0;
        root["InputConfiguration"]!["BattleMessageSpeed"] = null;
        root["InputConfiguration"]!["NativeBattleSpeedByte"] = null;
        root["EventState"]!["InputConfiguration"]!["BattleMessageSpeed"] = null;
        root["EventState"]!["InputConfiguration"]!["NativeBattleSpeedByte"] = null;
        foreach (JsonNode? actor in root["EventState"]!["Actors"]!.AsArray())
        {
            actor!["NativeEyes"] = null;
        }
        File.WriteAllText(migrated, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        GameSession session = new(GameSession.Load(migrated));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(621, world.Events.FieldId);
        Assert.Equal(17, world.Events.Frame);
        Assert.Empty(world.Events.Scene.FieldPartyNativeActorIds);
        Assert.True(world.Events.TalkTo(6, true));
        string? boundary = null;
        try
        {
            for (int callback = 0; callback < 120 && world.Events.PendingTripleTriad is null; callback++)
            {
                if (world.Events.CurrentDialogue is { CanConfirm: true } dialogue)
                {
                    world.Events.ConfirmDialogue(dialogue.FirstChoice);
                }
                else
                {
                    world.Events.Step();
                }
            }
        }
        catch (Exception exception) when (exception is EventScriptException or NotSupportedException)
        {
            boundary = exception.Message;
        }
        session.SetEventState(world.Events.Capture());
        string reached = Path.Combine(output, "martine-field621-challenge-boundary.json");
        session.Save(reached);
        Console.WriteLine($"Original source: {source}\nMinimal diagnostic migration: {migrated}\nReached challenge boundary: {reached}\nBoundary: {boundary ?? world.Events.NextTrigger.ToString()}");
        Assert.Null(boundary);
        NativeTripleTriadRequest request = Assert.IsType<NativeTripleTriadRequest>(world.Events.PendingTripleTriad);
        Assert.Equal((621, 27, 6, 26, 145), (request.FieldId, request.Frame, request.ObjectId, request.ScriptId, request.Operation));
        Assert.Equal(new[] { 121, 144, 1, 50, 7, 4, 127 }, request.RawOperands);
        var controller = world.EnsureTripleTriad()!;
        Assert.True(controller.AwaitingRules);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 15)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        int[] hand = Enumerable.Range(0, 110).SelectMany(id => Enumerable.Repeat(id, Math.Max(0, session.CardInventory!.AvailableCount(id)))).Take(5).ToArray();
        foreach (int card in hand)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.TriadSelectCard, card)).Accepted);
        }
        Assert.NotNull(controller.Match);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.TriadAutoMove)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.TriadAutoMove)).Accepted);
        Assert.Equal(2, controller.Match!.Board.Count(card => card is not null));
        string midmatch = Path.Combine(output, "martine-field621-midmatch.json");
        session.Save(midmatch);
        GameSession restoredSession = new(GameSession.Load(midmatch));
        using GameWorld restored = new(restoredSession, directory);
        restored.EnsureField();
        var restoredController = restored.EnsureTripleTriad()!;
        int turns = 0;
        while (controller.Match.Outcome == TripleTriadOutcome.InProgress || controller.Match.CanRestartSuddenDeath)
        {
            Assert.True(turns++ < 200, "The bounded original match did not finish.");
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(controller.Match.CaptureSnapshot()), JsonSerializer.SerializeToElement(restoredController.Match!.CaptureSnapshot())));
            Assert.Equal(session.State.TripleTriadPlay!.RandomState, restoredSession.State.TripleTriadPlay!.RandomState);
            PlayerAction next = new(controller.Match.CanRestartSuddenDeath ? PlayerActionKind.Confirm : PlayerActionKind.TriadAutoMove);
            Assert.True(world.Apply(next).Accepted);
            Assert.True(restored.Apply(next).Accepted);
        }
        TripleTriadOutcome outcome = controller.Match.Outcome;
        Assert.Equal(27, world.Events.Frame);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        for (int slot = 0; slot < 5 && world.Events.PendingTripleTriad is not null; slot++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.TriadTradeCard, slot)).Accepted);
            Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.TriadTradeCard, slot)).Accepted);
        }
        Assert.Null(world.Events.PendingTripleTriad);
        Assert.Null(restored.Events.PendingTripleTriad);
        Assert.Null(session.State.TripleTriadPlay);
        Assert.Null(restoredSession.State.TripleTriadPlay);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.CardInventory), JsonSerializer.SerializeToElement(restoredSession.CardInventory)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(1, session.CardInventory!.TripleTriadStatistics!.Wins + session.CardInventory.TripleTriadStatistics.Losses + session.CardInventory.TripleTriadStatistics.Draws);
        Assert.Equal(outcome == TripleTriadOutcome.Draw ? (ushort)1 : (ushort)0, session.CardInventory.TripleTriadStatistics.Draws);
        string final = Path.Combine(output, "martine-field621-after-match.json");
        session.Save(final);
        _ = GameSession.Load(final);
        Console.WriteLine($"Native match outcome: {outcome}; final field/frame: {world.Events.FieldId}/{world.Events.Frame}; midmatch: {midmatch}; settled standalone: {final}");
    }

    private static string OutputDirectory()
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-triad-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        return output;
    }

    private static string Repository()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "docs", "recreate-checkpoint.md")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Research repository not found.");
    }
}
