using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeProcessClockContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_B, 205), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1500), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.RET, 8));
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine live = new(session);
        live.LoadField(999, content, []);
        for (int frame = 0; frame < 3; frame++)
        {
            live.Step();
        }
        Require(live.ExamTimer is { ClockCalls: 6, TimerMemoryActivated: true } && live.ExamTimer.RequireRemaining() == 60 && live.ExamTimer.Alternatives[0].FirstPhase == 6 * 2191, "First SET discarded genuine pre-SET callbacks or pre-SET clock tracking claimed unproved timer coordinate memory.");
        EventEngine resumed = new(GameSession.CreateColdProcess(), NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        resumed.LoadField(999, content, []);
        JsonElement issued = live.Capture();
        resumed.Restore(issued);
        uint expectedPhase = 6 * 2191;
        int expectedRemaining = 60;
        for (int callback = 0; callback < 1500; callback++)
        {
            for (int call = 0; call < 2; call++)
            {
                expectedPhase = unchecked(expectedPhase + 2191);
                if ((expectedPhase & 0xFFFE0000) != 0 && expectedRemaining != 0)
                {
                    expectedRemaining = unchecked(expectedRemaining - 1);
                    expectedPhase &= 0xFFFF;
                }
            }
        }
        for (int frame = 0; frame < 1500; frame++)
        {
            live.Step();
            resumed.Step();
        }
        Require(JsonElement.DeepEquals(live.Capture(), resumed.Capture()) && live.ExamTimer!.Alternatives.SequenceEqual([new NativeTimerAlternative(expectedPhase, expectedPhase, expectedRemaining)]), "Cold-process countdown or independent VM restore disagreed with native scalar clock through the fifty-second threshold.");
        Require(live.Capture().GetProperty("Fibers")[0].GetProperty("Results").GetProperty("0").GetInt32() == expectedRemaining, "GETTIMER lost the exact process-clock result.");
        foreach (string missing in new[] { "ExamTimer", "TimerMemoryActivated" })
        {
            JsonNode invalid = JsonNode.Parse(issued.GetRawText())!;
            if (missing == "ExamTimer")
            {
                invalid.AsObject().Remove(missing);
            }
            else
            {
                invalid["ExamTimer"]!.AsObject().Remove(missing);
            }
            JsonElement before = resumed.Capture();
            try
            {
                resumed.Restore(JsonSerializer.SerializeToElement(invalid));
                throw new InvalidOperationException("Missing current process-clock metadata was accepted.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
            }
            Require(JsonElement.DeepEquals(before, resumed.Capture()), "Invalid process-clock restore changed VM or services before refusal.");
        }
        GameSession unknown = new(new GameState());
        EventEngine refused = new(unknown);
        refused.LoadField(999, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.RET, 8)), []);
        try
        {
            refused.Step();
            throw new InvalidOperationException("SETTIMER invented a process-clock origin.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "SETTIMER")
        {
        }
        Require(unknown.ExamTimer is null && refused.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && refused.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 60, "Missing-clock SET refusal consumed the operand or changed its PC.");
        Console.WriteLine("Native process clock: explicit cold origin, pre-SET callbacks, exact fifty-second countdown, independent restore and atomic missing-clock refusal passed.");
    }
}
