using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveDisplayOffsetTests
{
    [Theory]
    [InlineData("linear")]
    [InlineData("cosine")]
    [InlineData("explicit")]
    [InlineData("zero")]
    public void Display_offset_phase_resumes_and_completed_snapshot_remains_valid(string kind)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(kind)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        if (kind != "zero")
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveActorOffset offset = continuation.Actors.Actors[0].Offset!;
        Assert.Equal(kind == "zero" ? 3 : kind == "cosine" ? 2 : 1, offset.Mode);
        Assert.Equal(kind == "zero" ? 0 : 2, offset.Counter);
        Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.Frames);
        GameState prepared = sourceSession.PrepareSnapshot();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, initial.Resume with { Field = continuation.GetLeaderPosition() });
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        GameState restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredState.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredState.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredState.ExamTimer!.ClockCalls);
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        Assert.Equal(continuation.Scripts.Frame, restored.Events.Frame);
        Assert.Null(restored.Events.Actors[0].NativeDisplayOffset!.Command);
        Assert.Equal(source.Events.Actors[0].NativeMovementCompletion542, restored.Events.Actors[0].NativeMovementCompletion542);
        Assert.Equal(source.Events.Actors[0].NativeLandingTriangle508, restored.Events.Actors[0].NativeLandingTriangle508);
        for (int tick = 0; tick < 8; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            ActorState actual = restored.Events.Actors[0];
            Assert.Equal(source.Events.Actors[0].DisplayOffset, actual.DisplayOffset);
            Assert.Equal(source.Events.Actors[0].NativeDisplayOffset! with { Command = null }, actual.NativeDisplayOffset);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            GameState exactState = restoredSession.PrepareSnapshot();
            using GameWorld exact = new(new GameSession(exactState), directory, sharedAssets: assets);
            exact.EnsureField();
            Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), exact.Events.Capture()));
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
        }
        Assert.Equal(77, restoredSession.EventMemory.Read(600, 4).RequireExact());
        Assert.Equal(3, restored.Events.Actors[0].NativeDisplayOffset!.Mode);
        JsonObject malformed = JsonNode.Parse(restored.Events.Capture().GetRawText())!.AsObject();
        malformed["Actors"]![0]!["NativeDisplayOffset"]!["TargetX"] = 99;
        Assert.Throws<InvalidDataException>(() => restored.Events.Restore(JsonSerializer.SerializeToElement(malformed)));
    }

    private static byte[] Program(string kind)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.DOFFSET, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> ordinary = [(Jsm.Opcode.LBL, 1)];
        int[] operands = kind == "explicit" ? [3, 4, 5, 20, -10, 12, 5] : [20, -10, 12, kind == "zero" ? 0 : 5];
        ordinary.AddRange(operands.Select(value => (Jsm.Opcode.PSHN_L, value)));
        ordinary.Add((kind switch { "cosine" => Jsm.Opcode.COFFSET, "explicit" => Jsm.Opcode.LOFFSETS, _ => Jsm.Opcode.LOFFSET }, 0));
        ordinary.AddRange([(Jsm.Opcode.OFFSETSYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        return GameplaySaveFieldScriptsTests.Encode([1], [initialize, ordinary.ToArray()]);
    }

    private sealed class ScriptOverride(string fieldName, byte[] script) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            if (Path.GetFileName(logicalPath).Equals(fieldName + ".jsm", StringComparison.OrdinalIgnoreCase))
            {
                content = script.ToArray();
                return true;
            }
            content = [];
            return false;
        }
    }
}
