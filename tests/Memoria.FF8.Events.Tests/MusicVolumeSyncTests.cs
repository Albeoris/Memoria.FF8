using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMusicVolumeSyncContract()
    {
        foreach (bool transition in new[] { false, true })
        {
            List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0)];
            if (transition)
            {
                operations.AddRange([(Jsm.Opcode.INITSOUND, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 90), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MUSICVOLTRANS, 0)]);
            }
            operations.AddRange([(Jsm.Opcode.PSHN_L, 987), (Jsm.Opcode.POPI_L, 2), (Jsm.Opcode.PSHN_L, 123456), (Jsm.Opcode.MUSICVOLSYNC, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.MUSICVOLSYNC, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
            byte[] content = SyntheticProgram(operations.ToArray());
            EventEngine Create()
            {
                GameSession session = CreateControlledMusicSession();
                EventEngine engine = new(session);
                engine.LoadField(100, content, []);
                SetControlledMusicChannels(engine);
                return engine;
            }
            EventEngine engine = Create();
            int syncCount = 0;
            int audioCount = 0;
            NativeMusicVolume? priorRequest = null;
            JsonElement? priorScene = null;
            JsonElement randomState = engine.Capture().GetProperty("EventRandom");
            engine.AudioCommandExecuted += _ => audioCount++;
            engine.CommandExecuted += command =>
            {
                if (command.Name == "MUSICVOLTRANS")
                {
                    priorRequest = engine.Scene.MusicVolumeRequest;
                    priorScene = engine.Capture().GetProperty("Scene");
                }
                if (command.Name != "MUSICVOLSYNC")
                {
                    return;
                }
                JsonElement saved = engine.Capture();
                JsonElement fiber = saved.GetProperty("Fibers")[0];
                Require(command.Arguments.Count == 0 && engine.Frame == 1 && fiber.GetProperty("WaitReason").GetString() == "None", "MUSICVOLSYNC consumed fabricated operands or yielded despite native return2.");
                Require(fiber.GetProperty("Results").GetProperty("2").GetInt32() == 987, "MUSICVOLSYNC overwrote a VM register with its native backend return value.");
                JsonElement stack = fiber.GetProperty("Stack");
                Require(stack.GetArrayLength() == (syncCount == 0 ? 1 : 0) && (syncCount != 0 || stack[0].GetInt32() == 123456), "MUSICVOLSYNC changed an unrelated operand.");
                Require(engine.Scene.MusicVolumeRequest == priorRequest && (priorScene is null || JsonElement.DeepEquals(priorScene.Value, saved.GetProperty("Scene"))), "MUSICVOLSYNC acknowledged, cancelled or changed an independent native fade request.");
                syncCount++;
            };
            engine.Step();
            JsonElement checkpoint = engine.Capture();
            Require(syncCount == 2 && audioCount == (transition ? 2 : 0) && engine.LastFault is null && checkpoint.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 123456 && JsonElement.DeepEquals(checkpoint.GetProperty("EventRandom"), randomState), "MUSICVOLSYNC did not continue its successors in the issuing callback or changed unrelated audio/RNG state.");
            if (transition)
            {
                Require(engine.Scene.MusicVolumeRequest is { RequestedDuration: 180, EffectiveDuration: 180, RequestedVolume: 0, StoredVolume: 0 }, "MUSICVOLSYNC waited for or erased the still-independent180-duration music ramp.");
            }
            EventEngine restored = Create();
            restored.Restore(checkpoint);
            Require(JsonElement.DeepEquals(checkpoint, restored.Capture()), "MUSICVOLSYNC introduced an unsaved acknowledgement or hidden wait state.");
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "MUSICVOLSYNC continuation differs after independent exact restore.");
        }

        // An actual GETTIMER produces a bounded typed scalar, rather than a seeded operand.
        byte[] uncertain = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.MUSICVOLSYNC, 0), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        GameSession timerSession = new(new GameState { ExamTimer = NativeExamTimerState.FromReachedSet(0) });
        EventEngine timer = new(timerSession) { AnimationFrameCount = (_, _) => 4 };
        timer.LoadField(100, uncertain, []);
        bool copiedTimer = false;
        timer.CommandExecuted += command => copiedTimer |= command.Name == "POPI_L";
        while (!copiedTimer && timer.Frame < 8)
        {
            timer.Step();
        }
        JsonElement results = timer.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Results");
        Require(timer.LastFault is null && JsonElement.DeepEquals(results.GetProperty("0"), results.GetProperty("1")) && results.GetProperty("1").GetProperty("Values").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 59, 60 }), "MUSICVOLSYNC refused or narrowed a typed unrelated operand although the native handler reads no stack.");
        Console.WriteLine("Native MUSICVOLSYNC329: authenticated zero helper, same-callback return2, empty/occupied/typed stacks, independent180-duration fade request and exact restore verified.");
    }
}
