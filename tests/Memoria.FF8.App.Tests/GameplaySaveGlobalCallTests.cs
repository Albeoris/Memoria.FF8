using System.Text.Json;
using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveGlobalCallTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Cold_nested_global_calls_resume_ret9_without_replaying_committed_rewards(int depth)
    {
        CheckResume(depth, 0);
    }

    [Fact]
    public void Exhausted_native_budget_before_global_lbl_preserves_unentered_call_frame()
    {
        CheckResume(1, 10);
    }

    private static void CheckResume(int depth, int padding)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(depth, padding)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(source.Events.IsUserControlEnabled);
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveScriptFiber caller = Assert.Single(continuation.Scripts.Fibers, fiber => fiber.GlobalCalls is { Length: > 0 });
        Assert.Equal(depth, caller.GlobalCalls!.Length);
        Assert.Equal(padding == 0 ? 1 + 9 * depth : 2, caller.Stack.Length);
        Assert.All(caller.GlobalCalls, frame => Assert.Equal(padding == 0, frame.Entered));
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
        GameplaySaveGlobalCall[] calls = caller.GlobalCalls.ToArray();
        calls[0] = calls[0] with { CallOperation = int.MaxValue };
        fibers[callerIndex] = caller with { GlobalCalls = calls };
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Scripts = valid.Scripts with { Fibers = fibers } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        calls[0] = caller.GlobalCalls[0] with { OperandBase = caller.GlobalCalls[0].OperandBase + 1 };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        calls[0] = caller.GlobalCalls[0];
        int[] stack = caller.Stack.ToArray();
        stack[stack.Length - calls[0].OperandBase - 1]++;
        fibers[callerIndex] = caller with { Stack = stack };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        GameState restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredState.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredState.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredState.ExamTimer!.ClockCalls);
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        for (int tick = 0; tick < 7; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            Assert.Equal(sourceSession.State.Economy!.Gil, restoredSession.State.Economy!.Gil);
            using GameWorld exact = new(new GameSession(restoredSession.PrepareSnapshot()), directory, sharedAssets: assets);
            exact.EnsureField();
            Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), exact.Events.Capture()));
        }
        Assert.Equal(prepared.Economy!.Gil + (padding == 0 ? 17 : 30), restoredSession.State.Economy!.Gil);
        Assert.Equal(333, restored.Events.EventMemory.Read(600, 2).Values[0]);
        Assert.Equal(77, restored.Events.EventMemory.Read(602, 2).Values[0]);
        Assert.Equal(11, restored.Events.EventMemory.Read(604, 2).Values[0]);
        Assert.All(restored.Events.CaptureGameplaySaveScripts().Fibers, fiber => Assert.Null(fiber.GlobalCalls));
    }

    private static byte[] Program(int depth, int padding)
    {
        List<(Jsm.Opcode, int)[]> scripts =
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, 333), (Jsm.Opcode.GJMP, 2), (Jsm.Opcode.POPM_W, 600), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_W, 602), (Jsm.Opcode.PSHI_L, 6), (Jsm.Opcode.POPM_W, 604), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]
        ];
        List<(Jsm.Opcode, int)> caller = scripts[1].ToList();
        caller.InsertRange(6, Enumerable.Repeat((Jsm.Opcode.NOP, 0), padding));
        scripts[1] = caller.ToArray();
        for (int index = 0; index < depth; index++)
        {
            scripts.Add(index == depth - 1
                ? [(Jsm.Opcode.LBL, index + 2), (Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 9)]
                : [(Jsm.Opcode.LBL, index + 2), (Jsm.Opcode.GJMP, index + 3), (Jsm.Opcode.RET, 9)]);
        }
        byte[] program = GameplaySaveFieldScriptsTests.Encode([(ushort)(depth + 1)], scripts);
        for (int offset = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(6)); offset < program.Length; offset += 4)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(program.AsSpan(offset)) == (int)Jsm.Opcode.PSHI_L)
            {
                BinaryPrimitives.WriteInt32LittleEndian(program.AsSpan(offset), (int)Jsm.Opcode.PSHI_L << 24);
            }
        }
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
