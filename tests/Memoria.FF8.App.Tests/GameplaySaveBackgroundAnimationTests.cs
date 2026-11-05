using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveBackgroundAnimationTests
{
    [Theory]
    [InlineData("blocking")]
    [InlineData("reverse")]
    [InlineData("sync")]
    [InlineData("loop")]
    [InlineData("absent")]
    public void Background_animation_wait_preserves_consumed_pc_phase_and_absence(string kind)
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
        for (int tick = 0; tick < 2; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        if (kind == "absent")
        {
            JsonObject historical = JsonNode.Parse(source.Events.Capture().GetRawText())!.AsObject();
            JsonArray animations = historical["BackgroundAnimations"]!.AsArray();
            animations.Remove(animations.Single(item => item!["Id"]!.GetValue<int>() == 1));
            source.Events.Restore(JsonSerializer.SerializeToElement(historical));
        }
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.BackgroundAnimation);
        if (kind == "absent")
        {
            Assert.DoesNotContain(continuation.Scene.BackgroundAnimations, animation => animation.Id == 1);
        }
        else
        {
            Assert.Contains(continuation.Scene.BackgroundAnimations, animation => animation.Id == 1 && animation.Active && animation.Loop == (kind == "loop"));
        }
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
        int index = Array.FindIndex(fibers, fiber => fiber.Wait == GameplaySaveScriptWait.BackgroundAnimation);
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
        for (int tick = 0; tick < 9; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            Assert.Equal(source.Events.BackgroundStates, restored.Events.BackgroundStates);
            Assert.Equal(source.Events.CaptureGameplaySaveFieldContinuation().Scene.BackgroundAnimations, restored.Events.CaptureGameplaySaveFieldContinuation().Scene.BackgroundAnimations);
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
        }
        if (kind == "loop")
        {
            Assert.Contains(restored.Events.CaptureGameplaySaveScripts().Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.BackgroundAnimation);
        }
        else
        {
            Assert.Equal(77, restoredSession.EventMemory.Read(600, 4).RequireExact());
        }
    }

    private static byte[] Program(string kind)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> background = [(Jsm.Opcode.LBL, 17)];
        if (kind != "absent")
        {
            background.AddRange([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.PSHN_L, kind == "reverse" ? 3 : 0), (Jsm.Opcode.PSHN_L, kind == "reverse" ? 0 : 3)]);
            background.Add((kind switch { "loop" => Jsm.Opcode.RBGANIMELOOP, "sync" => Jsm.Opcode.RBGANIME, _ => Jsm.Opcode.BGANIME }, 0));
        }
        if (kind is "sync" or "loop" or "absent")
        {
            background.Add((Jsm.Opcode.BGANIMESYNC, 0));
        }
        background.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)]);
        byte[] result = GameplaySaveFieldScriptsTests.Encode([32 << 7, (16 << 7) | 1, 1], [initialize, [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)], background.ToArray(), [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 0)]]);
        result[2] = 2;
        result[3] = 1;
        return result;
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
