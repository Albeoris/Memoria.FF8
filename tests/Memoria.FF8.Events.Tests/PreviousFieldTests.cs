using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePreviousFieldContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_W, 84), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 65535), (Jsm.Opcode.POPM_W, 84), (Jsm.Opcode.PSHSM_W, 84), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        GameSession session = new(new GameState());
        EventEngine first = new(session, session.State.Variables);
        first.LoadField(159, content, [], previousFieldId: 160);
        Require(first.Scene.PreviousFieldWord84 == 160 && first.Frame == 0, "Native previous field was not available before original initialization.");
        first.Step();
        JsonElement snapshot = first.Capture();
        JsonElement results = snapshot.GetProperty("Fibers")[0].GetProperty("Results");
        Require(results.GetProperty("3").GetInt32() == 160 && results.GetProperty("4").GetInt32() == -1 && first.Scene.PreviousFieldWord84 == ushort.MaxValue, "Previous-field WORD read, original write or signed read changed native width.");
        GameSession resumedSession = new(new GameState());
        EventEngine resumed = new(resumedSession, resumedSession.State.Variables);
        resumed.LoadField(159, content, []);
        resumed.Restore(snapshot);
        first.Step();
        resumed.Step();
        Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Independent restore replaced the saved current WORD84 with loader provenance.");
        first.LoadField(160, content, [], previousFieldId: 159);
        Require(first.Scene.PreviousFieldWord84 == 159, "The next actual field load retained an earlier script-written WORD84.");

        JsonNode malformed = JsonNode.Parse(resumed.Capture().GetRawText())!;
        malformed["Variables"]!["84"] = 160;
        string before = resumed.Capture().GetRawText();
        try
        {
            resumed.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Mismatching previous-field memory alias was restored.");
        }
        catch (InvalidDataException)
        {
        }
        Require(before == resumed.Capture().GetRawText(), "Previous-field restore refusal mutated the current VM.");

        GameSession ambiguousSession = new(new GameState { Variables = new Dictionary<int, int> { [83] = 123 } });
        EventEngine ambiguous = new(ambiguousSession, ambiguousSession.State.Variables);
        ambiguous.LoadField(160, content, []);
        before = ambiguous.Capture().GetRawText();
        try
        {
            ambiguous.LoadField(159, content, [], previousFieldId: 160);
            throw new InvalidOperationException("Previous-field load overwrote an ambiguous overlapping alias.");
        }
        catch (NotSupportedException)
        {
        }
        Require(before == ambiguous.Capture().GetRawText(), "Refused previous-field load mutated field state.");

        byte[] overlap = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.POPM_L, 84), (Jsm.Opcode.RET, 0));
        GameSession refusedSession = new(new GameState());
        EventEngine refused = new(refusedSession, refusedSession.State.Variables);
        refused.LoadField(159, overlap, [], previousFieldId: 160);
        try
        {
            refused.Step();
            throw new InvalidOperationException("A DWORD write overlapped known previous-field WORD84.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "POPM_L")
        {
        }
        Require(refused.Scene.PreviousFieldWord84 == 160 && refused.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 123, "Refused previous-field write consumed its operand or changed memory.");
    }
}
