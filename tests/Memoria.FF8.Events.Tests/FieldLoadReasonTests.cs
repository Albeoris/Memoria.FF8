using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFieldLoadReasonContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_B, 87), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHSM_B, 87), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.POPM_B, 87), (Jsm.Opcode.PSHSM_B, 87), (Jsm.Opcode.POPI_L, 5), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine first = new(new GameSession(new GameState()));
        first.LoadField(600, content, [], fieldLoadReason: 193);
        Require(first.Scene.FieldLoadReasonByte87 == 193 && first.Frame == 0, "Explicit field-load BYTE87 was not available before initialization.");
        first.Step();
        JsonElement snapshot = first.Capture();
        JsonElement results = snapshot.GetProperty("Fibers")[0].GetProperty("Results");
        Require(results.GetProperty("3").GetInt32() == 193 && results.GetProperty("4").GetInt32() == -63 && results.GetProperty("5").GetInt32() == -1 && first.Scene.FieldLoadReasonByte87 == 255 && first.Variables[87] == 255, "Field-load BYTE87 read, signed extension, original store truncation or projection changed native width.");
        Require(!first.EventMemory.Cells.ContainsKey(87), "Original BYTE87 writer created a competing generic memory cell.");
        Require(NativeEventMemoryOwnership.SceneOwners(snapshot.GetProperty("Scene")).HasFlag(NativeEventSceneMemoryOwners.FieldLoadReason), "Captured BYTE87 lost its canonical scene registration.");
        EventEngine restored = new(new GameSession(new GameState()));
        restored.LoadField(600, content, [], fieldLoadReason: 2);
        restored.Restore(snapshot);
        first.Step();
        restored.Step();
        Require(JsonElement.DeepEquals(first.Capture(), restored.Capture()), "Cold restoration replaced the script-written BYTE87 with a newer allocation cause.");
        Require(restored.ReadPersistentPredicateFingerprint([new("PSHM_B", 87, 1)]) is null, "Field-local BYTE87 was accepted as a portable source guard.");
        byte[] boundaryRead = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHSM_B, 87), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        foreach (byte reason in new byte[] { 127, 255 })
        {
            EventEngine boundary = new(new GameSession(new GameState()));
            boundary.LoadField(600, boundaryRead, [], fieldLoadReason: reason);
            boundary.Step();
            Require(boundary.Capture().GetProperty("Fibers")[0].GetProperty("Results").GetProperty("0").GetInt32() == unchecked((sbyte)reason), "Signed field-load BYTE87 changed its native sign boundary.");
        }

        JsonNode malformed = JsonNode.Parse(restored.Capture().GetRawText())!;
        SetCapturedCell(malformed, 87, 1, 255);
        string before = restored.Capture().GetRawText();
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Captured BYTE87 accepted a competing generic byte alias.");
        }
        catch (InvalidDataException)
        {
        }
        Require(before == restored.Capture().GetRawText(), "Refused BYTE87 checkpoint mutated the current VM.");

        EventEngine aliased = new(new GameSession(new GameState { EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [85] = 123 }) }));
        aliased.LoadField(600, content, []);
        before = aliased.Capture().GetRawText();
        try
        {
            aliased.LoadField(601, content, [], fieldLoadReason: 1);
            throw new InvalidOperationException("Field allocation overwrote retained memory spanning BYTE87.");
        }
        catch (NotSupportedException)
        {
        }
        Require(before == aliased.Capture().GetRawText(), "Refused field-load BYTE87 allocation mutated the current field.");

        byte[] overlap = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.POPM_W, 87), (Jsm.Opcode.RET, 0));
        EventEngine refused = new(new GameSession(new GameState()));
        refused.LoadField(600, overlap, [], fieldLoadReason: 1);
        try
        {
            refused.Step();
            throw new InvalidOperationException("A WORD store overlapped allocated BYTE87.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "POPM_W")
        {
        }
        Require(refused.Scene.FieldLoadReasonByte87 == 1 && refused.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 123, "Unsupported BYTE87 overlap consumed an operand or changed its owner.");

        byte[] unknownRead = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_B, 87), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.RET, 0));
        GameSession unknownSession = GameSession.CreateColdProcess();
        unknownSession.StartNewGame();
        EventEngine unknown = new(unknownSession);
        unknown.LoadField(600, unknownRead, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        JsonElement unknownSnapshot = unknown.Capture();
        Require(!unknownSnapshot.GetProperty("Scene").TryGetProperty("FieldLoadReasonByte87", out _), "Unknown field-load reason was serialized as an inferred byte.");
        GameSession coldUnknownSession = GameSession.CreateColdProcess();
        coldUnknownSession.StartNewGame();
        EventEngine coldUnknown = new(coldUnknownSession);
        coldUnknown.LoadField(600, unknownRead, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En, fieldLoadReason: 2);
        coldUnknown.Restore(unknownSnapshot);
        Require(coldUnknown.Scene.FieldLoadReasonByte87 is null, "Frozen unknown BYTE87 inherited a new load's allocation reason during restoration.");
        foreach (EventEngine engine in new[] { unknown, coldUnknown })
        {
            try
            {
                engine.Step();
                throw new InvalidOperationException("Verified native BYTE87 read inferred an unknown allocation reason.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == "PSHM_B")
            {
            }
            JsonElement stopped = engine.Capture().GetProperty("Fibers")[0];
            Require(engine.Scene.FieldLoadReasonByte87 is null && stopped.GetProperty("Position").GetInt32() == 1 && stopped.GetProperty("Stack").GetArrayLength() == 0, "Unknown BYTE87 read advanced PC, published a guessed value or changed operands.");
        }
    }
}
