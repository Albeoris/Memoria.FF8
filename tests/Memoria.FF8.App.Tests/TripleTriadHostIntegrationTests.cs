using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadHostIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalMartineChallengeRestoresAndCompletesTheNativeHost(bool acceptReplay)
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.TripleTriadHostIntegrationTests.1");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Original diagnostic standalone and installed resources are required.");
        string output = OutputDirectory();
        string migrated = Path.Combine(output, "martine-field621-source-physical-reset-migrated.json");
        Assert.Equal("564A32FC6A26F8EB174E117A17546523914E39560DDA0FA2317D6E5AA7C65B62", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
        JsonObject root = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        root["FieldBars"] = null;
        root["EventState"]!["FieldBars"] = null;
        NativeCardInventory legacyCards = root["CardInventory"]!.Deserialize<NativeCardInventory>()!;
        // Preserve every diagnostic ownership byte; no missing historical rare-card state is inferred.
        NativeCardInventory migratedCards = NativeCardInventoryMigration.MigrateLegacyInitialization(legacyCards, [], 0, new TripleTriadStatistics(0, 0, 0), 0);
        root["CardInventory"] = JsonSerializer.SerializeToNode(migratedCards);
        root["EventState"]!["CardInventory"] = JsonSerializer.SerializeToNode(migratedCards);
        root["InputConfiguration"]!["BattleMessageSpeed"] = null;
        root["InputConfiguration"]!["NativeBattleSpeedByte"] = null;
        root["EventState"]!["InputConfiguration"]!["BattleMessageSpeed"] = null;
        root["EventState"]!["InputConfiguration"]!["NativeBattleSpeedByte"] = null;
        foreach (JsonNode? actor in root["EventState"]!["Actors"]!.AsArray())
        {
            actor!["NativeEyes"] = null;
        }
        MigrateHaltedDefaultOperandDepths(root, directory);
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
        for (int slot = 0; slot < 5 && !controller.AwaitingReplay; slot++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.TriadTradeCard, slot)).Accepted);
            Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.TriadTradeCard, slot)).Accepted);
        }
        Assert.True(controller.AwaitingReplay);
        Assert.True(restoredController.AwaitingReplay);
        Assert.NotNull(world.Events.PendingTripleTriad);
        Assert.NotNull(restored.Events.PendingTripleTriad);
        Assert.Equal(27, world.Events.Frame);
        Assert.Equal(27, restored.Events.Frame);
        if (acceptReplay)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
            Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
            var replay = world.EnsureTripleTriad()!;
            var restoredReplay = restored.EnsureTripleTriad()!;
            Assert.NotSame(controller, replay);
            Assert.NotSame(restoredController, restoredReplay);
            Assert.False(replay.AwaitingRules);
            Assert.False(replay.AwaitingReplay);
            Assert.Empty(replay.Deck);
            Assert.Null(replay.Match);
            Assert.Equal(27, world.Events.Frame);
            Assert.NotNull(world.Events.PendingTripleTriad);
            Assert.Equal(1, session.CardInventory!.TripleTriadStatistics!.Wins + session.CardInventory.TripleTriadStatistics.Losses + session.CardInventory.TripleTriadStatistics.Draws);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.CardInventory), JsonSerializer.SerializeToElement(restoredSession.CardInventory)));
            Assert.Equal(replay.Available, restoredReplay.Available);
            foreach (int id in replay.Available)
            {
                Assert.Equal(session.CardInventory.AvailableCount(id), replay.Remaining(id));
            }
            int selected = replay.Available.First();
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.TriadSelectCard, selected)).Accepted);
            string replaySelection = Path.Combine(output, "martine-field621-replay-selection.json");
            session.Save(replaySelection);
            GameSession replaySession = new(GameSession.Load(replaySelection));
            using GameWorld reloadedReplay = new(replaySession, directory);
            reloadedReplay.EnsureField();
            var reloadedController = reloadedReplay.EnsureTripleTriad()!;
            Assert.False(reloadedController.AwaitingRules);
            Assert.False(reloadedController.AwaitingReplay);
            Assert.Equal(new[] { selected }, reloadedController.Deck);
            Assert.Null(reloadedController.Match);
            Assert.Equal(27, reloadedReplay.Events.Frame);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.CardInventory), JsonSerializer.SerializeToElement(replaySession.CardInventory)));
            return;
        }
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
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

    [Fact]
    public void UnmigratedDiagnosticRefusesUnknownPhysicalReturnMemory()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.TripleTriadHostIntegrationTests.2");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Original diagnostic standalone and installed resources are required.");
        using GameWorld world = new(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        Assert.Null(world.Events.Actors[9].NativeOperandMemory);
        Assert.True(world.Events.TalkTo(6, true));
        EventScriptException exception = Assert.Throws<EventScriptException>(() =>
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
        });
        Assert.Contains("Native RET requires eight actual retained physical DWORDs", exception.Message);
        JsonElement blocked = world.Events.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 9 && fiber.GetProperty("ScriptId").GetInt32() == 60 && !fiber.GetProperty("Complete").GetBoolean());
        Assert.Equal(940, blocked.GetProperty("Position").GetInt32());
        Assert.Empty(blocked.GetProperty("Stack").EnumerateArray());
        Assert.Null(world.Events.Actors[9].NativeOperandMemory!.LiveDepth);
    }

    private static void MigrateHaltedDefaultOperandDepths(JsonObject root, string directory)
    {
        JsonObject events = root["EventState"]!.AsObject();
        Assert.Equal(621, events["FieldId"]!.GetValue<int>());
        Assert.Equal(17, events["Frame"]!.GetValue<int>());
        Assert.True(events["DefaultsStarted"]!.GetValue<bool>());
        long[] sequences = events["Fibers"]!.AsArray().Select(item => item!["Sequence"]!.GetValue<long>()).Order().ToArray();
        Assert.Equal(Enumerable.Range(0, sequences.Length).Select(index => sequences[0] + index), sequences);
        Assert.Equal(sequences[^1], events["Sequence"]!.GetValue<long>());
        using GameAssets assets = new(directory);
        byte[] original = assets.LoadField(621).Jsm;
        Assert.Equal("415FDCF94D0476A9218322F75384CE5E3FDE056C73610F32893BEB43AEE481A4", Convert.ToHexString(SHA256.HashData(original)));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(original)), events["ProgramHash"]!.GetValue<string>());
        JsmProgram program = JsmProgram.Read(original);
        foreach (int objectId in new[] { 6, 9 })
        {
            JsonObject actor = events["Actors"]![objectId]!.AsObject();
            Assert.Equal(objectId, actor["ObjectId"]!.GetValue<int>());
            Assert.Null(actor["NativeOperandMemory"]);
            JsonNode fiber = Assert.Single(events["Fibers"]!.AsArray(), item => item!["ObjectId"]!.GetValue<int>() == objectId && !item["Complete"]!.GetValue<bool>())!;
            var script = program.Objects[objectId].Scripts[1];
            Assert.Equal(script.Id, fiber["ScriptId"]!.GetValue<int>());
            Assert.True(fiber["NativeDoorDefaultFrame"]!.GetValue<bool>());
            Assert.False(fiber["NativeResultFrameEntered"]!.GetValue<bool>());
            Assert.Equal("NativeHalt", fiber["WaitReason"]!.GetValue<string>());
            Assert.Empty(fiber["Stack"]!.AsArray());
            Assert.Equal(script.Start + 1, fiber["Position"]!.GetValue<int>());
            Assert.Equal(Jsm.Opcode.LBL, program.Operations[script.Start].Opcode);
            Assert.Equal(Jsm.Opcode.HALT, program.Operations[script.Start + 1].Opcode);
            // Native52C960 reset depth388 to FF; this immediate HALT executed no operands.
            // Preserve every unobserved physical DWORD and saved-slot depth as unknown.
            actor["NativeOperandMemory"] = JsonSerializer.SerializeToNode(NativeActorOperandMemory.Unknown() with { LiveDepth = -1 });
        }
    }

    private static string OutputDirectory()
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-triad-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        return output;
    }

    private static string Repository()
    {
        DirectoryInfo? current = new(TestResources.FixtureDirectory);
        return current?.FullName ?? throw new DirectoryNotFoundException("Research repository not found.");
    }
}
