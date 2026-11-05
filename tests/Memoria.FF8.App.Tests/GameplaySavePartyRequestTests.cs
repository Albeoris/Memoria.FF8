using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySavePartyRequestTests
{
    [Theory]
    [InlineData(Jsm.Opcode.PREQ, 4)]
    [InlineData(Jsm.Opcode.PREQSW, 65540)]
    [InlineData(Jsm.Opcode.PREQEW, -65532)]
    public void Cross_declared_code_resumes_on_original_receiving_body_with_raw_selector(Jsm.Opcode opcode, int selector)
    {
        CheckResume(opcode, selector, true);
    }

    [Fact]
    public void Queued_cross_declared_request_does_not_enter_lbl_during_reconstruction()
    {
        CheckResume(Jsm.Opcode.PREQEW, 4, false);
    }

    private static void CheckResume(Jsm.Opcode opcode, int selector, bool entered)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(opcode, selector)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        GameplaySaveFieldContinuation continuation = null!;
        for (int tick = 0; tick < 6; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
            continuation = source.Events.CaptureGameplaySaveFieldContinuation();
            if (continuation.Scripts.Fibers.Any(fiber => fiber.PartyRequestSelector is not null && (!entered || fiber.Started && fiber.Wait == GameplaySaveScriptWait.Frames)))
            {
                break;
            }
        }
        Assert.True(source.Events.IsUserControlEnabled);
        GameplaySaveScriptFiber requested = Assert.Single(continuation.Scripts.Fibers, fiber => fiber.PartyRequestSelector is not null);
        Assert.Equal(selector, requested.PartyRequestSelector);
        Assert.Equal(0, requested.ObjectId);
        Assert.Equal(18, requested.ScriptId);
        Assert.Equal((byte?)1, requested.ActorSlot);
        Assert.Equal(entered, requested.Started);
        Assert.Equal(entered, requested.ResultFrameEntered);
        Assert.Equal(!entered, source.Events.Actors[0].Visible);
        Assert.True(source.Events.Actors[1].Visible);
        bool callerCompleted = source.Events.EventMemory.Read(600, 2).Values[0] == 77;
        GameState prepared = sourceSession.PrepareSnapshot();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, initial.Resume with { Field = continuation.GetLeaderPosition() });
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        GameplaySaveFieldContinuation valid = decoded.Progression!.FieldContinuation!;
        int requestedIndex = Array.FindIndex(valid.Scripts.Fibers, fiber => fiber.Sequence == requested.Sequence);
        GameplaySaveScriptFiber[] fibers = valid.Scripts.Fibers.ToArray();
        fibers[requestedIndex] = requested with { PartyRequestSelector = selector + 1 };
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Scripts = valid.Scripts with { Fibers = fibers } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        fibers[requestedIndex] = requested with { ScriptId = 17 };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        fibers[requestedIndex] = requested with { ActorSlot = null };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        GameState restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredState.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredState.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredState.ExamTimer!.ClockCalls);
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        Assert.Equal(!entered, restored.Events.Actors[0].Visible);
        Assert.True(restored.Events.Actors[1].Visible);
        for (int tick = 0; tick < 8; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            Assert.Equal(source.Events.Actors[0].Visible, restored.Events.Actors[0].Visible);
            Assert.True(restored.Events.Actors[1].Visible);
            Assert.Equal(sourceSession.State.Economy!.Gil, restoredSession.State.Economy!.Gil);
            using GameWorld exact = new(new GameSession(restoredSession.PrepareSnapshot()), directory, sharedAssets: assets);
            exact.EnsureField();
            Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), exact.Events.Capture()));
        }
        Assert.Equal(prepared.Economy!.Gil + (entered ? 0 : 13) + 19 + (callerCompleted ? 0 : 17), restoredSession.State.Economy!.Gil);
        Assert.Equal(77, restored.Events.EventMemory.Read(600, 2).Values[0]);
        Assert.True(restored.Events.Actors[0].Visible);
    }

    private static byte[] Program(Jsm.Opcode request, int selector)
    {
        List<(Jsm.Opcode, int)[]> scripts =
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, selector), (request, 0), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.HIDE, 0), (Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.SHOW, 0), (Jsm.Opcode.PSHN_L, 19), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)]
        ];
        byte[] program = GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 2], scripts);
        int table = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(4));
        ushort entry = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(table + 8));
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(table + 8), (ushort)(entry | 0x8000));
        for (int offset = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(6)); offset < program.Length; offset += 4)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(program.AsSpan(offset)) == (int)request)
            {
                BinaryPrimitives.WriteInt32LittleEndian(program.AsSpan(offset), (int)request << 24);
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
