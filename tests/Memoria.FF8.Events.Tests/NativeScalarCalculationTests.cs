using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeScalarCalculationContract()
    {
        EventEngine Create(byte[] content)
        {
            GameSession session = new(new GameState { ExamTimer = NativeExamTimerState.FromReachedSet(0) });
            EventEngine engine = new(session);
            engine.LoadField(999, content, []);
            return engine;
        }
        foreach ((int kind, int threshold, int? expected) in new[] { (9, 10, (int?)0), (7, 10, (int?)1), (6, 60, (int?)null), (9, 60, (int?)null), (13, -1, (int?)-1) })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHN_L, threshold), (Jsm.Opcode.CAL, kind), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8));
            EventEngine live = Create(content);
            live.Step();
            EventEngine resumed = Create(content);
            resumed.Restore(live.Capture());
            foreach (EventEngine engine in new[] { live, resumed })
            {
                try
                {
                    engine.Step();
                    Require(expected.HasValue, "Ambiguous CAL chose one timer alternative.");
                }
                catch (EventScriptException) when (engine.LastFault?.Opcode == "CAL")
                {
                    Require(!expected.HasValue, "Unanimous CAL refused an exact result.");
                }
            }
            JsonElement current = live.Capture();
            Require(JsonElement.DeepEquals(current, resumed.Capture()), "Timer/CAL continuation or refusal diverged after independent restore.");
            JsonElement fiber = current.GetProperty("Fibers")[0];
            Require(fiber.GetProperty("Results").GetProperty("0").GetProperty("Values").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 59, 60 }), "CAL changed the original GETTIMER alternatives or timer phase.");
            if (expected is int known)
            {
                Require(live.LastFault is null && fiber.GetProperty("Results").GetProperty("1").GetInt32() == known && fiber.GetProperty("Stack").GetArrayLength() == 0, "Unanimous CAL did not consume exactly two operands and produce its original signedDWORD result.");
            }
            else
            {
                Require(fiber.GetProperty("Position").GetInt32() == 8 && fiber.GetProperty("Stack")[0].GetInt32() == threshold && fiber.GetProperty("Stack")[1].GetProperty("Values").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 59, 60 }), "Ambiguous CAL changed issuing PC or original typed operands.");
                EventEngine frozen = Create(content);
                frozen.Restore(current);
                Require(JsonElement.DeepEquals(current, frozen.Capture()), "Ambiguous CAL boundary did not restore exactly.");
            }
        }
        byte[] correlated = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.RET, 8));
        EventEngine sameRead = Create(correlated);
        sameRead.Step();
        try
        {
            sameRead.Step();
            throw new InvalidOperationException("Unimplemented same-read correlation was silently assumed.");
        }
        catch (EventScriptException) when (sameRead.LastFault?.Opcode == "CAL")
        {
        }
        Require(sameRead.Capture().GetProperty("Fibers")[0].GetProperty("Stack").GetArrayLength() == 2, "Same-read CAL refusal consumed its source alternatives.");
        foreach (int kind in new[] { 3, 4 })
        {
            GameSession divisionSession = new(new GameState());
            divisionSession.SetEventMemory(divisionSession.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(int.MinValue)));
            EventEngine division = new(divisionSession);
            division.LoadField(999, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.CAL, kind), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.RET, 8)), []);
            try
            {
                division.Step();
                throw new InvalidOperationException("Native IDIV quotient overflow was converted to a remainder result.");
            }
            catch (EventScriptException) when (division.LastFault?.Opcode == "CAL")
            {
            }
            JsonElement failed = division.Capture().GetProperty("Fibers")[0];
            Require(failed.GetProperty("Position").GetInt32() == 3 && failed.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([-1, int.MinValue]) && !failed.GetProperty("Results").TryGetProperty("1", out _), "Native IDIV refusal consumed either operand, advanced CAL or wrote its successor result.");
        }
        Console.WriteLine("Native CAL: unanimous/mixed timer comparisons, DWORD bitwiseOR, signedIDIV quotient/remainder traps preserving PC/operands, provenance and independent restore passed.");
    }
}
