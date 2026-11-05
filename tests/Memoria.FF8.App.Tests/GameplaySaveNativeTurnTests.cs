using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveNativeTurnTests
{
    [Theory]
    [InlineData("linear")]
    [InlineData("cosine")]
    [InlineData("negative")]
    [InlineData("positive")]
    [InlineData("party")]
    [InlineData("poll")]
    [InlineData("zero")]
    [InlineData("endpoint")]
    public void Native_body_turn_restores_scalar_worker_and_wait_without_requery_or_callback(string kind)
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
        int ticks = kind == "zero" ? 1 : kind == "endpoint" ? 3 : 2;
        for (int tick = 0; tick < ticks; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveActorTurn? turn = continuation.Actors.Actors[1].Turn;
        Assert.Equal(kind is "zero" or "endpoint", turn is null);
        GameplaySaveScriptWait wait = kind is "party" or "poll" ? GameplaySaveScriptWait.NativeTurnCompletion : GameplaySaveScriptWait.Rotation;
        Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.ObjectId == 1 && fiber.Wait == wait);
        GameState prepared = sourceSession.PrepareSnapshot();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, initial.Resume with { Field = continuation.GetLeaderPosition() });
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        GameplaySaveFieldContinuation valid = decoded.Progression!.FieldContinuation!;
        GameplaySaveScriptFiber[] fibers = valid.Scripts.Fibers.ToArray();
        int index = Array.FindIndex(fibers, fiber => fiber.ObjectId == 1 && fiber.Wait == wait);
        fibers[index] = fibers[index] with { Position = fibers[index].Position + 1 };
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Scripts = valid.Scripts with { Fibers = fibers } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        GameState restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredState.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredState.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredState.ExamTimer!.ClockCalls);
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        Assert.Equal(continuation.Scripts.Frame, restored.Events.Frame);
        Assert.Equal(source.Events.Actors[1].Direction, restored.Events.Actors[1].Direction);
        for (int tick = 0; tick < 8; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.Equal(source.Events.Actors[1].Direction, restored.Events.Actors[1].Direction);
            Assert.Equal(source.Events.Actors[1].RotationFrames, restored.Events.Actors[1].RotationFrames);
            Assert.Equal(source.Events.Actors[1].NativeTurnMode580, restored.Events.Actors[1].NativeTurnMode580);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            GameState exactState = restoredSession.PrepareSnapshot();
            using GameWorld exact = new(new GameSession(exactState), directory, sharedAssets: assets);
            exact.EnsureField();
            Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), exact.Events.Capture()));
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
        }
        Assert.Equal(77, restoredSession.EventMemory.Read(600, 4).RequireExact());
        Assert.Null(restored.Events.Actors[1].RotationTarget);
        Assert.Equal((byte?)3, restored.Events.Actors[1].NativeTurnMode580);
    }

    private static byte[] Program(string kind)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        int target = kind is "party" or "poll" ? 0 : kind == "negative" ? 240 : kind == "positive" ? -16 : 64;
        int duration = kind == "zero" ? 0 : kind == "endpoint" ? 2 : 5;
        List<(Jsm.Opcode, int)> ordinary = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, target), (Jsm.Opcode.PSHN_L, duration)];
        ordinary.Add((kind switch { "cosine" => Jsm.Opcode.CTURNL, "negative" => Jsm.Opcode.Unknown8, "positive" => Jsm.Opcode.Unknown9, "party" => Jsm.Opcode.PLTURN, "poll" => Jsm.Opcode.Unknown11, _ => Jsm.Opcode.LTURNL }, 0));
        if (kind == "poll")
        {
            ordinary.Add((Jsm.Opcode.Unknown12, 0));
        }
        ordinary.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 1], [initialize, [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], ordinary.ToArray()]);
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
