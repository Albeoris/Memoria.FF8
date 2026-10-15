using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SemanticProducerInvocationRecreationTests
{
    [Fact]
    public void Actual_332_prefix_preserves_two_distinct_push_invocations_and_repeats_to_321()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        string sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_LEGACY_INVOCATION_SOURCE") ?? Path.Combine("../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(sourcePath), "The installed original corpus and preserved authentic legacy journal are required.");
        GameState authentic = GameSession.LoadReplaySource(sourcePath);
        ReplayJournal original = Assert.IsType<ReplayJournal>(authentic.ReplayJournal);
        Assert.Equal(1, original.Version);
        ReplayJournalEntry[] inputs = original.GetCommittedPrefix().Entries.ToArray();
        int arrival = Array.FindIndex(inputs, input => input.Kind == ReplayEntryKind.FieldLoad && input.FieldId == 332);
        Assert.True(arrival >= 0);
        int departure = Array.FindIndex(inputs, arrival + 1, input => input.Kind == ReplayEntryKind.FieldLoad);
        Assert.True(departure > arrival);
        Assert.Equal(321, inputs[departure].FieldId);
        ReplayReceipt[] originalPushWrites = inputs.Skip(arrival).Take(departure - arrival).SelectMany(input => input.Receipts).Where(receipt => receipt.Kind == ReplayReceiptKind.Command && receipt.ObjectId == 3 && receipt.ScriptId == 45 && receipt.Operation is 559 or 567).ToArray();
        Assert.Equal(new[] { 559, 567 }, originalPushWrites.Select(receipt => receipt.Operation));

        using (GameAssets assets = new(directory))
        {
            JsmProgram program = JsmProgram.Read(assets.LoadField(332).Jsm);
            EventScript push = program.Objects[3].Scripts[3];
            Assert.Equal(45, push.Id);
            Assert.Equal(Jsm.Opcode.POPM_B, program.Operations[559].Opcode);
            Assert.Equal(Jsm.Opcode.POPM_B, program.Operations[567].Opcode);
            //Both recorded sites belong to the native push script, but neither can reach the other before RET.
            Assert.False(CanReachWithinInvocation(program, push, 559, 567));
            Assert.False(CanReachWithinInvocation(program, push, 567, 559));
        }
        ReplayJournal prefix = LiteralPrefix(sourcePath, original, inputs, departure);
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        MovieClock clock = new(cold, directory);
        Assert.SkipUnless(File.Exists(Path.Combine(clock.ToolsDirectory, "ffmpeg.exe")) && File.Exists(Path.Combine(clock.ToolsDirectory, "ffprobe.exe")), "The authentic movie decoder tools are required.");
        //The endpoint shell contains no old coordinates, local scratch values or snapshot story state.
        GameState rebuilt = GameWorld.RecreateSave(new GameState { Scene = SceneKind.Field, FieldId = 321, ReplayJournal = prefix }, directory, clock.ToolsDirectory);
        Assert.Equal(SceneKind.Field, rebuilt.Scene);
        Assert.Equal(321, rebuilt.FieldId);
        Assert.True(rebuilt.ReplayJournal!.IsComplete);
        Assert.NotEqual(prefix.SessionId, rebuilt.ReplayJournal.SessionId);
        AssertPushInvocations(rebuilt);

        GameState repeated = GameWorld.RecreateSave(rebuilt, directory, clock.ToolsDirectory);
        Assert.Equal(rebuilt.Scene, repeated.Scene);
        Assert.Equal(rebuilt.FieldId, repeated.FieldId);
        Assert.True(repeated.ReplayJournal!.IsComplete);
        AssertPushInvocations(repeated);
        Assert.Equal(rebuilt.Variables.Where(pair => pair.Key is >= 256 and < 1024).OrderBy(pair => pair.Key).ToArray(), repeated.Variables.Where(pair => pair.Key is >= 256 and < 1024).OrderBy(pair => pair.Key).ToArray());
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(rebuilt.CharacterVitals), JsonSerializer.SerializeToElement(repeated.CharacterVitals)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(rebuilt.Economy), JsonSerializer.SerializeToElement(repeated.Economy)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(rebuilt.CardInventory), JsonSerializer.SerializeToElement(repeated.CardInventory)));
        JsonElement currentScene = rebuilt.EventState!.Value.GetProperty("Scene");
        JsonElement repeatedScene = repeated.EventState!.Value.GetProperty("Scene");
        Assert.True(JsonElement.DeepEquals(currentScene.GetProperty("Party"), repeatedScene.GetProperty("Party")));
        Assert.True(JsonElement.DeepEquals(currentScene.GetProperty("Names"), repeatedScene.GetProperty("Names")));
    }

    private static void AssertPushInvocations(GameState state)
    {
        ReplayCompletedVisit visit = Assert.Single(state.ReplayJournal!.CompletedVisits, visit => visit.Scene == SceneKind.Field && visit.FieldId == 332);
        Assert.Equal(321, visit.Exit.FieldId);
        ReplaySemanticTrigger[] pushes = visit.Intents.Where(intent => intent.Kind == ReplayEntryKind.SemanticTrigger).Select(intent => intent.SemanticTrigger!).Where(trigger => trigger.Kind == ReplaySemanticTriggerKind.PushContact && trigger.ObjectId == 3 && trigger.ScriptId == 45).ToArray();
        Assert.Equal(2, pushes.Length);
        Assert.All(pushes, trigger =>
        {
            Assert.Equal(3, trigger.LocalScript);
            Assert.Equal(6, trigger.NativeSlot);
            Assert.Equal(visit.EntryFieldLoad!.NativeSourceHash, trigger.ProgramHash);
            Assert.Equal(visit.EntryFieldLoad.Text, trigger.ExecutableHash);
        });
        Assert.DoesNotContain(visit.Intents, intent => intent.Kind == ReplayEntryKind.Action && intent.Code == 4);
        Assert.NotNull(state.EventState);
        Assert.Equal(JsonValueKind.Null, state.EventState.Value.GetProperty("LastFault").ValueKind);
    }

    private static bool CanReachWithinInvocation(JsmProgram program, EventScript script, int start, int target)
    {
        Stack<int> pending = new();
        HashSet<int> visited = [];
        pending.Push(start);
        while (pending.TryPop(out int index))
        {
            if (index < script.Start || index >= script.End || !visited.Add(index))
            {
                continue;
            }
            if (index == target)
            {
                return true;
            }
            JsmOperation operation = program.Operations[index];
            if (operation.Opcode is Jsm.Opcode.RET or Jsm.Opcode.HALT)
            {
                continue;
            }
            if (operation.Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF)
            {
                pending.Push(index + operation.Parameter);
            }
            if (operation.Opcode != Jsm.Opcode.JMP)
            {
                pending.Push(index + 1);
            }
        }
        return false;
    }

    private static ReplayJournal LiteralPrefix(string sourcePath, ReplayJournal original, ReplayJournalEntry[] inputs, int departure)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(sourcePath));
        JsonElement capsule = document.RootElement.GetProperty("ReplayJournal");
        using MemoryStream bytes = new(Convert.FromBase64String(capsule.GetProperty("Data").GetString()!));
        using BrotliStream reader = new(bytes, CompressionMode.Decompress);
        using JsonDocument recorded = JsonDocument.Parse(reader);
        Assert.Equal(original.EntryCount, recorded.RootElement.GetArrayLength());
        JsonElement[] literalPrefix = recorded.RootElement.EnumerateArray().Take(departure + 1).Select(input => input.Clone()).ToArray();
        using MemoryStream compressed = new();
        using (BrotliStream writer = new(compressed, CompressionLevel.Optimal, true))
        {
            JsonSerializer.Serialize(writer, literalPrefix);
        }
        JsonObject prefixCapsule = JsonNode.Parse(capsule.GetRawText())!.AsObject();
        prefixCapsule["EntryCount"] = literalPrefix.Length;
        prefixCapsule["Data"] = Convert.ToBase64String(compressed.ToArray());
        prefixCapsule["IsComplete"] = true;
        prefixCapsule.Remove("UnavailableReason");
        ReplayJournal prefix = prefixCapsule.Deserialize<ReplayJournal>()!;
        prefix.Validate();
        Assert.Equal(original.SessionId, prefix.SessionId);
        Assert.Equal(JsonSerializer.Serialize(inputs.Take(departure + 1)), JsonSerializer.Serialize(prefix.Entries));
        return prefix;
    }
}
