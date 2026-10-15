using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMessageVariableContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, -7), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.MESVAR, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.MESVAR, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MESSYNC, 0), (Jsm.Opcode.RET, 8));
        EventEngine Create(bool verified = true)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            EventEngine engine = new(session);
            engine.LoadField(999, content, ["Score {Var 49}"], nativeRuntimeProfile: verified ? NativeFieldRuntimeProfile.FF8En : null);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        Require(live.CurrentDialogue?.Text == "Score 100" && live.MessageVariables?.At(1) == 100, "MESVAR did not mask its signed DWORD index or continue to opening in the same native VM turn.");
        JsonElement saved = live.Capture();
        Require(saved.GetProperty("Dialogues")[0].GetProperty("Text").GetString() == "Score {Var 49}", "Saved message replaced the live template with stale expanded text.");
        EventEngine restored = Create();
        restored.Restore(saved);
        live.Step();
        restored.Step();
        Require(live.CurrentDialogue?.Text == "Score 4294967295" && live.NextTrigger.Text == live.CurrentDialogue.Text && live.Dialogues.Single().Text == live.CurrentDialogue.Text, "Already-open message did not read live unsigned DWORD after MESVAR.");
        Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Message bank/template continuation changed across independent restore.");
        NativeMessageVariables bank = live.MessageVariables!;
        live.LoadField(998, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)), [], previousFieldId: 999, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(live.MessageVariables == bank, "Field allocation cleared process-owned message slots.");
        EventEngine unproved = Create(false);
        try
        {
            unproved.Step();
            throw new InvalidOperationException("MESVAR executed without native handler provenance.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement fiber = unproved.Capture().GetProperty("Fibers")[0];
        Require(fiber.GetProperty("Position").GetInt32() == 3 && fiber.GetProperty("Stack")[0].GetInt32() == 100 && fiber.GetProperty("Stack")[1].GetInt32() == -7 && unproved.MessageVariables == NativeMessageVariables.FromColdProcess(), "Refused MESVAR consumed operands, advanced PC or wrote the bank.");
        byte[] uncertain = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.MESVAR, 0), (Jsm.Opcode.RET, 8));
        GameSession uncertainSession = new(new GameState { ExamTimer = NativeExamTimerState.FromReachedSet(0) });
        EventEngine uncertainEngine = new(uncertainSession);
        uncertainEngine.LoadField(997, uncertain, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        uncertainEngine.Step();
        try
        {
            uncertainEngine.Step();
            throw new InvalidOperationException("MESVAR selected a representative timer alternative.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement uncertainFiber = uncertainEngine.Capture().GetProperty("Fibers")[0];
        Require(uncertainEngine.LastFault?.Opcode == "MESVAR" && uncertainFiber.GetProperty("Position").GetInt32() == 8 && uncertainFiber.GetProperty("Stack")[0].GetProperty("Values").GetArrayLength() > 1 && uncertainFiber.GetProperty("Stack")[1].GetInt32() == 1 && uncertainSession.MessageVariables is null, "MESVAR refusal lost its typed DWORD alternatives or wrote an invented message bank.");
        Console.WriteLine("MESVAR fullDWORD/masked slot, unsigned text, live open-message expansion, raw template persistence, field retention and atomic refusal passed.");
    }
}
