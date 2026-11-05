using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveDoorRequestTests
{
    [Theory]
    [InlineData(true, 3, 24)]
    [InlineData(false, 3, 24)]
    [InlineData(true, 257, 65560)]
    [InlineData(false, -255, 65560)]
    public void Cold_first_call_door_wait_preserves_dword_comparison_and_never_requeues(bool start, int rawPriority, int rawScript)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(start, rawPriority, rawScript)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        GameplaySaveFieldContinuation continuation = null!;
        for (int tick = 0; tick < 5; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
            continuation = source.Events.CaptureGameplaySaveFieldContinuation();
            if (continuation.Scripts.Fibers.Any(fiber => fiber.Wait == GameplaySaveScriptWait.NativeDoorRequest))
            {
                break;
            }
        }
        Assert.True(source.Events.IsUserControlEnabled);
        GameplaySaveScriptFiber caller = Assert.Single(continuation.Scripts.Fibers, fiber => fiber.Wait == GameplaySaveScriptWait.NativeDoorRequest);
        Assert.Equal(rawScript, caller.Stack[0]);
        Assert.Equal(rawPriority, caller.Stack[1]);
        Assert.NotNull(caller.RequestSequence);
        Assert.Equal((byte)6, source.Events.Actors[1].NativeDoorScheduler!.CurrentSlot);
        GameState prepared = sourceSession.PrepareSnapshot();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, initial.Resume with { Field = continuation.GetLeaderPosition() });
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        GameplaySaveFieldContinuation valid = decoded.Progression!.FieldContinuation!;
        int callerIndex = Array.FindIndex(valid.Scripts.Fibers, fiber => fiber.Sequence == caller.Sequence);
        GameplaySaveScriptFiber[] fibers = valid.Scripts.Fibers.ToArray();
        fibers[callerIndex] = caller with { RequestSequence = null };
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Scripts = valid.Scripts with { Fibers = fibers } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        fibers[callerIndex] = caller with { RequestSequence = caller.Sequence };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        int[] operands = caller.Stack.ToArray();
        operands[0]++;
        fibers[callerIndex] = caller with { Stack = operands };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        GameState restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredState.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredState.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredState.ExamTimer!.ClockCalls);
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        JsonElement restoredCaller = restored.Events.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Sequence").GetInt64() == caller.Sequence);
        Assert.Equal(nameof(NativeDoorRequestWaitOrigin.GameplayContinuation), restoredCaller.GetProperty("NativeDoorRequestWait").GetProperty("Origin").GetString());
        for (int tick = 0; tick < 12; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.Equal(source.Events.Actors[1].NativeDoorScheduler, restored.Events.Actors[1].NativeDoorScheduler);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            Assert.Equal(sourceSession.State.Economy!.Gil, restoredSession.State.Economy!.Gil);
            using GameWorld exact = new(new GameSession(restoredSession.PrepareSnapshot()), directory, sharedAssets: assets);
            exact.EnsureField();
            Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), exact.Events.Capture()));
        }
        bool completes = rawPriority == 3;
        Assert.Equal(prepared.Economy!.Gil + (completes ? 30 : 13), restoredSession.State.Economy!.Gil);
        GameplaySaveFieldScripts result = restored.Events.CaptureGameplaySaveScripts();
        Assert.Equal(!completes, result.Fibers.Any(fiber => fiber.Wait == GameplaySaveScriptWait.NativeDoorRequest));
        Assert.Equal(completes ? 77 : 0, restored.Events.EventMemory.Read(600, 2).Values[0]);
        Assert.False(restored.Events.Actors[1].LineEnabled);
    }

    private static byte[] Program(bool start, int rawPriority, int rawScript)
    {
        (Jsm.Opcode, int)[] Empty(int label) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> higher = [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDGIL, 0)];
        higher.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), 62));
        higher.Add((Jsm.Opcode.RET, 8));
        List<(Jsm.Opcode, int)[]> scripts =
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.PSHN_L, 19), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.PSHN_L, rawPriority), (Jsm.Opcode.PSHN_L, rawScript), (start ? Jsm.Opcode.REQSW : Jsm.Opcode.REQEW, 1), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            Empty(18), higher.ToArray(), Empty(20), Empty(21), Empty(22), Empty(23),
            [(Jsm.Opcode.LBL, 24), (Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.LINEOFF, 0), (Jsm.Opcode.RET, 8)],
            Empty(25)
        ];
        byte[] program = GameplaySaveFieldScriptsTests.Encode([(16 << 7) | 9, 1], scripts);
        program[1] = 1;
        program[3] = 1;
        return program;
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
