using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeKeyScanContract()
    {
        (GameSession Session, EventEngine Engine) Create(int mask)
        {
            GameSession session = new(new GameState());
            session.StartNewGame();
            EventEngine engine = new(session, session.State.Variables);
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, mask), (Jsm.Opcode.KEYSCAN, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.JMP, -4), (Jsm.Opcode.RET, 0)]]);
            engine.LoadField(74, content, []);
            engine.Step();
            engine.Step();
            return (session, engine);
        }

        foreach (int mask in new[] { 0, 0x10, 0x40, 0x80, 0x1000, 0x2000, 0x4000, 0x8000, 0xF050, 0x10000, -1 })
        {
            var current = Create(mask);
            Require(current.Session.State.Variables[5000] == 0, "KEYSCAN invented held input.");
            foreach (int held in new[] { 0x10, 0x40, 0x1000, 0x2000, 0x4000, 0x8000, 0xF050 })
            {
                PlayerActionResult input = current.Engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons, held));
                Require(input.Accepted && input.FramesAdvanced == 0, "A physical button change invented a field callback.");
                current.Engine.Step();
                int expected = (mask & held) != 0 ? 1 : 0;
                Require(current.Session.State.Variables[5000] == expected && current.Engine.Scene.ButtonInput!.Held == held, "KEYSCAN lost its full DWORD mask or mapped held word.");
                JsonElement snapshot = current.Engine.Capture();
                var restored = Create(mask);
                restored.Engine.Restore(snapshot);
                current.Engine.Step();
                restored.Engine.Step();
                Require(current.Engine.Scene.ButtonInput is { Pressed: 0 } && current.Session.State.Variables[5000] == expected && JsonElement.DeepEquals(current.Engine.Capture(), restored.Engine.Capture()), "Held input was confused with a rising edge or changed after independent restore.");
                current.Engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0));
                current.Engine.Step();
                Require(current.Session.State.Variables[5000] == 0, "Released buttons stayed held.");
            }
        }

        var transient = Create(0x10);
        transient.Engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0x40));
        transient.Engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0));
        transient.Engine.Step();
        Require(transient.Engine.Scene.ButtonInput is { Held: 0, Pressed: 0 } && transient.Session.State.Variables[5000] == 0, "A press released before sampling became a held button or edge.");
        transient.Engine.Apply(new PlayerAction(PlayerActionKind.Cancel));
        Require(transient.Engine.Scene.ButtonInput is { Held: 0x10, Pressed: 0x10 } && transient.Session.State.Variables[5000] == 1, "Ordinary Cancel failed to supply its one-callback held pulse.");
        transient.Engine.Step();
        Require(transient.Engine.Scene.ButtonInput is { Held: 0, Pressed: 0 }, "A logical action pulse remained held.");
        JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(transient.Session.ReplayJournal))!.Validate();
        foreach (NativeFieldButtonInput invalid in new[] { new NativeFieldButtonInput(2, 0, 0x40, 2, 0, 0, 0), new NativeFieldButtonInput(2, 0, 0, 1, 0, 0, 0), new NativeFieldButtonInput(2, 0x1000, 0, 2, 0, 0, 0) })
        {
            try
            {
                invalid.Validate(2);
                throw new InvalidOperationException("Impossible mapped button history was accepted.");
            }
            catch (InvalidDataException)
            {
            }
        }
        System.Text.Json.Nodes.JsonNode missing = System.Text.Json.Nodes.JsonNode.Parse(transient.Engine.Capture().GetRawText())!;
        missing["Scene"]!["ButtonInput"] = null;
        try
        {
            transient.Engine.Restore(JsonSerializer.SerializeToElement(missing));
            throw new InvalidOperationException("A completed callback accepted absent button history.");
        }
        catch (InvalidDataException)
        {
        }

        var underflow = Create(0x40);
        System.Text.Json.Nodes.JsonNode empty = System.Text.Json.Nodes.JsonNode.Parse(underflow.Engine.Capture().GetRawText())!;
        System.Text.Json.Nodes.JsonNode query = empty["Fibers"]!.AsArray().Single(fiber => fiber!["ScriptId"]!.GetValue<int>() == 1)!;
        query["Position"] = 4;
        query["Stack"] = new System.Text.Json.Nodes.JsonArray();
        query["StackExpressions"] = new System.Text.Json.Nodes.JsonArray();
        underflow.Engine.Restore(JsonSerializer.SerializeToElement(empty));
        try
        {
            underflow.Engine.Step();
            throw new InvalidOperationException("KEYSCAN accepted a missing DWORD mask.");
        }
        catch (EventScriptException)
        {
        }
        Require(underflow.Engine.LastFault?.Opcode == "KEYSCAN" && underflow.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Position").GetInt32() == 4, "KEYSCAN underflow consumed or advanced the command.");
        Console.WriteLine("KEYSCAN: held/edge separation, all supported button bits, full DWORD masks, release, action pulses, independent restore and preserved underflow passed.");
    }
}
