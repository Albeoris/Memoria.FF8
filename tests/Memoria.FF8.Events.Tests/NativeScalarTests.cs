using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeScalarMemoryContract()
    {
        byte[] content = BattleModeCallbackProgram((Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.POPM_W, 432), (Jsm.Opcode.KILLTIMER, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0));
        GameSession session = new(new GameState { ExamTimer = NativeExamTimerState.FromReachedSet(0), WorldControls = NativeWorldControlState.CreateNewGame(), WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame(), EventRandom = NativeEventRandomState.FromColdProcess().ClearSavedStateForNewGame() });
        EventEngine engine = new(session);
        engine.LoadField(999, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        Require(engine.Frame == 1 && session.ExamTimer!.ClockCalls == 0, "Scalar fixture initialization advanced its timer clock.");
        engine.Step();
        try
        {
            engine.Step();
            throw new InvalidOperationException("WAIT chose one uncertain native operand stack value.");
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == "WAIT")
        {
        }
        JsonElement held = engine.Capture();
        JsonElement heldMain = held.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(heldMain.GetProperty("Position").GetInt32() == 10 && heldMain.GetProperty("Stack")[1].GetProperty("Values").GetArrayLength() == 2, "Uncertain command refusal did not preserve its PC and typed stack.");
        GameSession resumedSession = new(new GameState());
        EventEngine resumed = new(resumedSession);
        resumed.LoadField(999, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        resumed.Restore(held);
        Require(JsonElement.DeepEquals(held, resumed.Capture()), "Typed stack/result restore changed its native alternatives.");

        // The native default frame remains parked at HALT; it is not a requested LBL frame that can RET.
        byte[] storeContent = BattleModeCallbackProgram((Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_W, 432), (Jsm.Opcode.KILLTIMER, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0));
        GameSession storeSession = new(new GameState { ExamTimer = NativeExamTimerState.FromReachedSet(0), WorldControls = NativeWorldControlState.CreateNewGame(), WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame(), EventRandom = NativeEventRandomState.FromColdProcess().ClearSavedStateForNewGame() });
        EventEngine stored = new(storeSession);
        ScriptCommand? write = null;
        stored.CommandExecuted += command =>
        {
            if (command.Name == "POPM_W")
            {
                write = command;
            }
        };
        stored.LoadField(999, storeContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        stored.Step();
        Require(stored.Frame == 1 && storeSession.ExamTimer!.ClockCalls == 0, "Scalar store fixture initialization advanced its timer clock.");
        stored.Step();
        stored.Step();
        NativeMemoryValue entry = storeSession.State.NativeVariableValues![432];
        Require(!storeSession.State.Variables.ContainsKey(432) && entry.Width == 2 && entry.Value.Values.SequenceEqual([599, 600]) && write!.Arguments.Count == 0 && write.ScalarArguments![0].Values.SequenceEqual([599, 600]), "POPM_W fabricated a scalar or lost its original argument alternatives.");
        GameSession restoredSession = new(new GameState());
        EventEngine restored = new(restoredSession);
        restored.LoadField(999, storeContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        JsonElement saved = stored.Capture();
        restored.Restore(saved);
        Require(JsonElement.DeepEquals(saved, restored.Capture()), "Independent typed memory restore lost the read snapshot or width.");
        JsonNode malformed = JsonNode.Parse(saved.GetRawText())!;
        malformed["EventMemory"]!["Cells"]!["432"]!["Value"]!["Values"]![0] = 598;
        try
        {
            using JsonDocument invalid = JsonDocument.Parse(malformed.ToJsonString());
            restored.Restore(invalid.RootElement);
            throw new InvalidOperationException("Malformed timer projection restored.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(saved, restored.Capture()), "Invalid native scalar restore mutated the original VM.");
        restored.LoadField(999, storeContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        JsonElement reentered = restored.Capture();
        Require(restored.Frame == 0 && restored.Scene.NativeFieldInitialization!.TimerClockCallsAtAllocation == 4, "Same-field reentry did not retain its global allocation clock.");
        GameSession reentrySession = new(new GameState());
        EventEngine reentry = new(reentrySession);
        reentry.LoadField(999, storeContent, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        reentry.Restore(reentered);
        Require(JsonElement.DeepEquals(reentered, reentry.Capture()) && reentrySession.State.NativeVariableValues![432].Value.Values.SequenceEqual([599, 600]), "Same-field reentry compared a previous read's frame with the reset current frame.");
        byte[] copy = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_W, 432), (Jsm.Opcode.POPM_W, 434), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 432), (Jsm.Opcode.RET, 0));
        stored.LoadField(998, copy, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        stored.Step();
        Require(storeSession.State.Variables[432] == 7 && storeSession.State.NativeVariableValues!.Count == 1 && storeSession.State.NativeVariableValues[434].Value.Values.SequenceEqual([599, 600]), "Cross-field typed copy or exact overwrite did not preserve native memory semantics.");
        Console.WriteLine("Native scalar memory: uncertain GETTIMER, result/stack/WORD transfer, cross-field copy, exact overwrite, source validation and preserved unsupported operands verified.");
    }
}
