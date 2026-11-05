using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveFieldFaceTests
{
    [Theory]
    [InlineData("blocking")]
    [InlineData("sync")]
    [InlineData("async")]
    [InlineData("endpoint")]
    [InlineData("immediate")]
    [InlineData("return")]
    [InlineData("return-endpoint")]
    public void Face_phase_and_wait_continue_without_reissuing_consumed_operands_or_initialization(string kind)
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
        for (int tick = 0; tick < (kind.StartsWith("return", StringComparison.Ordinal) ? 4 : 2); tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveActorFace face = continuation.Actors.Actors[0].Face;
        Assert.True(face.Progress > 0);
        Assert.Equal(kind is "immediate" or "return" or "return-endpoint", face.Returning);
        Assert.Equal(kind is "endpoint" or "return-endpoint", face.Progress == face.Duration);
        GameplaySaveScriptWait expected = kind == "sync" ? GameplaySaveScriptWait.NativeFaceSync : kind == "async" ? GameplaySaveScriptWait.Frames : GameplaySaveScriptWait.FaceDirection;
        Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.Wait == expected);
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
        int index = Array.FindIndex(fibers, fiber => fiber.Wait == expected);
        fibers[index] = fibers[index] with { Position = fibers[index].Position + 1, Wait = GameplaySaveScriptWait.NativeFaceSync };
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
        Assert.Equal(source.Events.Actors[0].FaceReturning, restored.Events.Actors[0].FaceReturning);
        Assert.Equal(source.Events.Actors[0].FirstControlRotation, restored.Events.Actors[0].FirstControlRotation);
        for (int tick = 0; tick < 8; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.Equal(source.Events.Actors[0].FaceProgress, restored.Events.Actors[0].FaceProgress);
            Assert.Equal(source.Events.Actors[0].FaceReturning, restored.Events.Actors[0].FaceReturning);
            Assert.Equal(source.Events.Actors[0].FirstControlRotation, restored.Events.Actors[0].FirstControlRotation);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
        }
        Assert.Equal(77, restoredSession.EventMemory.Read(600, 4).RequireExact());
        if (face.Returning)
        {
            Assert.Null(restored.Events.Actors[0].FirstControlRotation);
            Assert.False(restored.Events.Actors[0].FaceReturning);
        }
    }

    private static byte[] Program(string kind)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.FACEDIRINIT, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.FACEDIRLIMIT, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> ordinary = [(Jsm.Opcode.LBL, 1)];
        if (kind.StartsWith("return", StringComparison.Ordinal))
        {
            ordinary.AddRange([(Jsm.Opcode.PSHN_L, 32), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.RFACEDIRI, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, kind == "return-endpoint" ? 2 : 5), (Jsm.Opcode.FACEDIROFF, 0)]);
        }
        else
        {
            int[] operands = kind == "immediate" ? [32, 16, 64, 5] : [-700, -8500, 1000, kind == "endpoint" ? 2 : 5];
            ordinary.AddRange(operands.Select(value => (Jsm.Opcode.PSHN_L, value)));
            ordinary.Add((kind switch { "sync" or "async" => Jsm.Opcode.RFACEDIR, "immediate" => Jsm.Opcode.FACEDIRI, _ => Jsm.Opcode.FACEDIR }, 0));
            if (kind == "sync")
            {
                ordinary.Add((Jsm.Opcode.FACEDIRSYNC, 0));
            }
            if (kind == "async")
            {
                ordinary.AddRange([(Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0)]);
            }
        }
        ordinary.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        return GameplaySaveFieldScriptsTests.Encode([1], [initialize, ordinary.ToArray()]);
    }

    private sealed class ScriptOverride(string fieldName, byte[] program) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            if (Path.GetFileName(logicalPath).Equals(fieldName + ".jsm", StringComparison.OrdinalIgnoreCase))
            {
                content = program.ToArray();
                return true;
            }
            content = [];
            return false;
        }
    }
}
