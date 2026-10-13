using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePreviousWorldEntityContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHSM_B, 86), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHM_B, 86), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        foreach ((short mode, int signed) in new[] { ((short)33, 33), ((short)255, -1), ((short)128, -128) })
        {
            GameSession session = new(new GameState { Scene = SceneKind.Field, WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(new WorldTransition(63, mode, -1), 262) });
            EventEngine first = new(session, session.State.Variables);
            first.LoadField(106, content, [], previousFieldId: 262);
            Require(first.Scene.PreviousWorldEntityByte86 == unchecked((byte)mode) && first.Frame == 0, "Native world entity byte was unavailable before field initialization.");
            first.Step();
            JsonElement snapshot = first.Capture();
            JsonElement results = snapshot.GetProperty("Fibers")[0].GetProperty("Results");
            Require(results.GetProperty("3").GetInt32() == signed && results.GetProperty("4").GetInt32() == unchecked((byte)mode), "Signed/unsigned native byte projections disagree.");
            GameSession boundSession = new(session.State with { Variables = new(session.State.Variables), EventState = snapshot });
            EventEngine bound = new(boundSession, boundSession.State.Variables);
            bound.LoadField(106, content, [], worldEntityAllocationContext: snapshot);
            bound.Restore(snapshot);
            Require(JsonElement.DeepEquals(snapshot, bound.Capture()), "Fresh receiver rejected or replaced the exact saved native allocation receipt.");
            string savePath = Path.Combine(Path.GetTempPath(), "Memoria.FF8-world-entity-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                boundSession.Save(savePath, registerJournalReference: false);
                Require(GameSession.Load(savePath).WorldConfiguration == boundSession.WorldConfiguration, "Ordinary save load lost process world entity authority.");
                JsonNode changedRoot = JsonNode.Parse(File.ReadAllText(savePath))!;
                changedRoot["WorldConfiguration"] = JsonSerializer.SerializeToNode(NativeWorldConfiguration.FromFieldPacket(new WorldTransition(63, 0, -1), 262));
                File.WriteAllText(savePath, changedRoot.ToJsonString());
                try
                {
                    GameSession.Load(savePath);
                    throw new InvalidOperationException("Tampered root entity mode disagreed with allocated byte but loaded.");
                }
                catch (InvalidDataException)
                {
                }
            }
            finally
            {
                File.Delete(savePath);
            }
            GameSession resumedSession = new(session.State with { Variables = [] });
            EventEngine resumed = new(resumedSession, resumedSession.State.Variables);
            resumed.LoadField(106, content, []);
            resumed.Restore(snapshot);
            first.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Independent byte-authority restore changed the next callback.");
            JsonNode malformed = JsonNode.Parse(resumed.Capture().GetRawText())!;
            malformed["Variables"]!["86"] = 0;
            string before = resumed.Capture().GetRawText();
            try
            {
                resumed.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Disagreeing byte-authority checkpoint was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            Require(before == resumed.Capture().GetRawText(), "Byte checkpoint refusal mutated the live VM.");
            first.LoadField(102, content, [], previousFieldId: 106);
            Require(first.Scene.PreviousWorldEntityByte86 == unchecked((byte)mode), "Ordinary field reload lost the retained process world configuration.");
        }
        GameSession unknownSession = new(new GameState());
        unknownSession.StartNewGame();
        Require(unknownSession.WorldConfiguration is null, "New Game invented previous process world configuration.");
        EventEngine unknown = new(unknownSession, unknownSession.State.Variables);
        unknown.LoadField(106, content, []);
        try
        {
            unknown.Step();
            throw new InvalidOperationException("Unknown previous world entity byte acquired a zero default.");
        }
        catch (EventScriptException) when (unknown.LastFault?.Opcode == "PSHSM_B")
        {
        }
        Require(unknown.Capture().GetProperty("Fibers")[0].GetProperty("Stack").GetArrayLength() == 0 && unknown.Scene.PreviousWorldEntityByte86 is null, "Unknown byte refusal changed stack or authority.");
        byte[] overlap = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.POPM_W, 86), (Jsm.Opcode.RET, 0));
        GameSession refusedSession = new(new GameState { WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(new WorldTransition(63, 33, -1), 262) });
        EventEngine refused = new(refusedSession, refusedSession.State.Variables);
        refused.LoadField(106, overlap, []);
        try
        {
            refused.Step();
            throw new InvalidOperationException("Overlapping world entity write was accepted.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "POPM_W")
        {
        }
        Require(refused.Scene.PreviousWorldEntityByte86 == 33 && refused.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 123, "Refused byte overlap consumed its operand.");
    }
}
