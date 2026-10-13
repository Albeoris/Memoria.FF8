using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFadeNoneContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.FADEOUT, 0), (Jsm.Opcode.PSHN_L, 1234), (Jsm.Opcode.FADENONE, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHM_W, 216), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine Create(byte[]? bytes = null)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, bytes ?? content, []);
            return engine;
        }
        EventEngine first = Create();
        first.Step();
        NativeFadeNone none = first.Scene.FadeNone ?? throw new InvalidOperationException("FADENONE lost original provenance.");
        JsonElement begun = first.Capture();
        Require(first.Scene.FadeMode216 == 0 && first.Scene.FadeOpacity == 0 && begun.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 1234 && begun.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("4").GetInt32() == 0 && begun.GetProperty("FadeEndFrame").GetInt64() > 0, "FADENONE consumed an operand, reset a prior fade counter or lost exact WORD216/immediate continuation.");
        EventEngine resumed = Create();
        resumed.Restore(begun);
        for (int tick = 0; tick < 4; tick++)
        {
            first.Step();
            resumed.Step();
            Require(first.Scene.FadeOpacity == 0 && JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Mode0 fade overlay resumed a retained transition or changed after restore.");
        }
        byte[] legacy = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.FADENONE, 0), (Jsm.Opcode.FADEBLACK, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine changed = Create(legacy);
        changed.Step();
        Require(changed.Scene.FadeMode216 is null && changed.Scene.FadeNone is not null && changed.Scene.FadeOpacity == 1 && !changed.Capture().GetProperty("Variables").TryGetProperty("216", out _), "Legacy fade changed overlay while retaining stale native mode0 or erased none history.");
        byte[] overlap = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.FADENONE, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 215), (Jsm.Opcode.RET, 0));
        EventEngine refused = Create(overlap);
        try
        {
            refused.Step();
            throw new InvalidOperationException("Mixed-width fade alias bypassed canonical WORD216.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "POPM_L")
        {
        }
        Require(refused.Scene.FadeMode216 == 0 && refused.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 77, "Fade alias refusal consumed the write or changed native mode.");
        EventEngine ambiguous = Create();
        JsonNode source = JsonNode.Parse(ambiguous.Capture().GetRawText())!;
        source["Variables"]!["215"] = 65536;
        ambiguous.Restore(JsonSerializer.SerializeToElement(source));
        try
        {
            ambiguous.Step();
            throw new InvalidOperationException("FADENONE overwrote an ambiguous preexisting alias.");
        }
        catch (EventScriptException) when (ambiguous.LastFault?.Opcode == "FADENONE")
        {
        }
        Require(ambiguous.Scene.FadeNone is null && ambiguous.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 1234, "Refused FADENONE changed state or consumed its sentinel.");
        JsonNode malformed = JsonNode.Parse(begun.GetRawText())!;
        malformed["Scene"]!["FadeMode216"] = 1;
        try
        {
            Create().Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Unsupported native fade mode passed restore.");
        }
        catch (InvalidDataException)
        {
        }
        Require(none.Operation == 3, "FADENONE provenance lost its original operation.");
        Console.WriteLine("Native FADENONE: zero operands/return2, mode0 and WORD216 zero, no overlay with retained counters, exact reads, restore and mixed-width/unknown alias guards verified.");
    }
}
