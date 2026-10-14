using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeKeyOnContract()
    {
        void Press(EventEngine engine, ushort mask)
        {
            Require(engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons, mask)).Accepted, "Ordinary mapped physical input was refused.");
            engine.Step();
            Require(engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons)).Accepted, "Ordinary mapped release was refused.");
        }

        (GameSession Session, EventEngine Engine) Create(int mask)
        {
            GameSession session = new(new GameState());
            session.StartNewGame();
            EventEngine engine = new(session, session.State.Variables);
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, mask), (Jsm.Opcode.KEYON, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.JMP, -4), (Jsm.Opcode.RET, 0)]]);
            engine.LoadField(74, content, []);
            engine.Step();
            engine.Step();
            return (session, engine);
        }

        foreach (int mask in new[] { 0, 0x40, 0x80, 0xC0, -1 })
        {
            var current = Create(mask);
            Require(current.Session.State.Variables[5000] == 0, "KEYON invented a button edge without player input.");
            Press(current.Engine, 0x40);
            Require(current.Session.State.Variables[5000] == ((mask & 0x40) == 0 ? 0 : 1), "Ordinary primary input did not deliver one original mapped edge to the DWORD mask.");
            Require(current.Engine.Scene.ButtonInput is { PendingPressed: 0, Pressed: 0x40 }, "Confirm was not sampled and consumed by the field callback.");
            JsonElement saved = current.Engine.Capture();
            var independent = Create(mask);
            independent.Engine.Restore(saved);
            current.Engine.Apply(new PlayerAction(PlayerActionKind.Wait));
            independent.Engine.Apply(new PlayerAction(PlayerActionKind.Wait));
            Require(current.Session.State.Variables[5000] == 0 && JsonElement.DeepEquals(current.Engine.Capture(), independent.Engine.Capture()), "A sampled edge persisted into the next frame or changed after independent restore.");
            Press(current.Engine, 0x40);
            Require(current.Session.State.Variables[5000] == ((mask & 0x40) == 0 ? 0 : 1), "A new ordinary Confirm failed to create its own edge.");
            current.Engine.Apply(new PlayerAction(PlayerActionKind.Cancel));
            Require(current.Engine.Scene.ButtonInput is { Pressed: 0x10 } && current.Session.State.Variables[5000] == ((mask & 0x10) == 0 ? 0 : 1), "Cancel was fabricated as the primary/card edge.");
            ReplayJournalEntry cancel = current.Session.ReplayJournal!.Endpoint!;
            Require(cancel.Code == (int)PlayerActionKind.Cancel && cancel.Advanced == 1, "Cancel input did not retain its own causal action.");
            JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(current.Session.ReplayJournal))!.Validate();
        }

        var source = Create(0xC0);
        PlayerAction[] actions = [new(PlayerActionKind.FieldButtons, 0x40), new(PlayerActionKind.Wait), new(PlayerActionKind.FieldButtons), new(PlayerActionKind.Wait), new(PlayerActionKind.Cancel), new(PlayerActionKind.FieldButtons, 0x40), new(PlayerActionKind.Wait)];
        var replayed = Create(0xC0);
        source.Engine.Replay(actions);
        replayed.Engine.Replay(actions);
        Require(JsonElement.DeepEquals(source.Engine.Capture(), replayed.Engine.Capture()), "Replaying ordinary button actions changed complete VM state.");

        GameSession dialogueSession = new(new GameState());
        EventEngine dialogue = new(dialogueSession, dialogueSession.State.Variables);
        byte[] dialogueContent = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MES, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MESSYNC, 0), (Jsm.Opcode.PSHN_L, 0x40), (Jsm.Opcode.KEYON, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0));
        dialogue.LoadField(100, dialogueContent, ["Continue"]);
        dialogue.Step();
        Require(dialogue.Apply(new PlayerAction(PlayerActionKind.Confirm)) is { Accepted: true, FramesAdvanced: 0 } && dialogue.Scene.ButtonInput is { PendingPressed: 0x40, Pressed: 0 }, "Dialogue completion lost the edge before its next field callback.");
        JsonElement pending = dialogue.Capture();
        GameSession restoredSession = new(new GameState());
        EventEngine restored = new(restoredSession, restoredSession.State.Variables);
        restored.LoadField(100, dialogueContent, ["Continue"]);
        restored.Restore(pending);
        dialogue.Step();
        restored.Step();
        Require(dialogueSession.State.Variables[5000] == 1 && JsonElement.DeepEquals(dialogue.Capture(), restored.Capture()), "Saving between confirmation and the next callback lost or repeated its pending edge.");

        GameSession invalidSession = new(new GameState());
        EventEngine invalid = new(invalidSession, invalidSession.State.Variables);
        invalid.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0x40), (Jsm.Opcode.KEYON, 0), (Jsm.Opcode.RET, 0)), []);
        System.Text.Json.Nodes.JsonNode empty = System.Text.Json.Nodes.JsonNode.Parse(invalid.Capture().GetRawText())!;
        empty["Fibers"]![0]!["Position"] = 2;
        invalid.Restore(JsonSerializer.SerializeToElement(empty));
        try
        {
            invalid.Step();
            throw new InvalidOperationException("KEYON accepted a missing original mask.");
        }
        catch (EventScriptException)
        {
        }
        Require(invalid.LastFault?.Opcode == "KEYON" && invalid.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2, "KEYON underflow advanced the original operation.");
        foreach (NativeFieldButtonInput malformed in new[] { new NativeFieldButtonInput(1, 0, 0, 0, 0, 0, 0), new NativeFieldButtonInput(2, 0x80, 0, 0, 0, 0, 0), new NativeFieldButtonInput(2, 0, 0, 10, 0, 0, 0) })
        {
            try
            {
                malformed.Validate(2);
                throw new InvalidOperationException("Malformed native button history was accepted.");
            }
            catch (InvalidDataException)
            {
            }
        }
        Console.WriteLine("KEYON: real confirm/cancel edge masks, next-frame clearing, pending/sampled save continuation and ordinary action replay passed; missing mask and invalid input history refuse.");
    }
}
