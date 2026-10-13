using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBackgroundClearContract(string gameDirectory)
    {
        EventEngine Create(byte[] content, int fieldId = 100)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(fieldId, content, []);
            return engine;
        }
        byte[] Content(int operand)
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.RBGANIMELOOP, 0), (Jsm.Opcode.PSHN_L, operand), (Jsm.Opcode.BGCLEAR, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            content[2] = 1;
            content[3] = 0;
            return content;
        }
        foreach (int operand in new[] { 1, 257, -255 })
        {
            byte[] content = Content(operand);
            EventEngine engine = Create(content);
            JsonElement beforeClear = default;
            bool checkedClear = false;
            engine.CommandExecuted += command =>
            {
                if (command.Name == "RBGANIMELOOP")
                {
                    beforeClear = engine.Capture();
                }
                if (command.Name == "BGCLEAR")
                {
                    JsonElement after = engine.Capture();
                    Require(JsonElement.DeepEquals(beforeClear.GetProperty("BackgroundStates"), after.GetProperty("BackgroundStates")) && JsonElement.DeepEquals(beforeClear.GetProperty("BackgroundAnimations"), after.GetProperty("BackgroundAnimations")), "BGCLEAR changed module selection, animation flags, speed or counters.");
                    Require(engine.Scene.BackgroundClearValue == 1 && command.Arguments.SequenceEqual(new[] { operand }), "BGCLEAR lost its original operand or low-BYTE store.");
                    checkedClear = true;
                }
            };
            engine.Step();
            Require(checkedClear && engine.Capture().GetProperty("Fibers")[0].GetProperty("Stack").GetArrayLength() == 0, "BGCLEAR did not execute immediately and consume its one operand.");
            JsonElement saved = engine.Capture();
            EventEngine restored = Create(content);
            restored.Restore(saved);
            Require(JsonElement.DeepEquals(saved, restored.Capture()), "Background clear byte and complete module animation state did not restore independently.");
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "BGCLEAR changed ordinary restored wait/animation progression.");
            restored.LoadField(101, content, []);
            Require(restored.Scene.BackgroundClearValue == 0, "Normal field load retained the departed field clear byte.");
        }
        foreach (int operand in new[] { 0, 2, -1, 256 })
        {
            byte[] content = Content(operand);
            EventEngine engine = Create(content);
            try
            {
                engine.Step();
                throw new InvalidOperationException("Unsupported retained-color BGCLEAR was silently accepted.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement saved = engine.Capture();
            JsonElement fiber = saved.GetProperty("Fibers")[0];
            Require(engine.Scene.BackgroundClearValue == 0 && engine.LastFault?.Opcode == "BGCLEAR" && fiber.GetProperty("Position").GetInt32() == 7 && fiber.GetProperty("Stack")[0].GetInt32() == operand, "BGCLEAR refusal changed issuing PC, operand or clear byte.");
            EventEngine restored = Create(content);
            restored.Restore(saved);
            Require(JsonElement.DeepEquals(saved, restored.Capture()), "Unsupported BGCLEAR boundary did not restore exactly.");
        }
        byte[] valid = Content(1);
        EventEngine special = Create(valid, 1);
        try
        {
            special.Step();
            throw new InvalidOperationException("BGCLEAR special field context was accepted.");
        }
        catch (EventScriptException)
        {
        }
        Require(special.Scene.BackgroundClearValue == 0 && special.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 1, "BGCLEAR special field refusal consumed its operand.");
        EventEngine underflow = Create(valid);
        underflow.Step();
        JsonNode empty = JsonNode.Parse(underflow.Capture().GetRawText())!;
        empty["Scene"]!["BackgroundClearValue"] = 0;
        empty["Fibers"]![0]!["Position"] = 7;
        empty["Fibers"]![0]!["WaitReason"] = "None";
        underflow.Restore(JsonSerializer.SerializeToElement(empty));
        try
        {
            underflow.Step();
            throw new InvalidOperationException("BGCLEAR empty runtime stack was accepted.");
        }
        catch (EventScriptException)
        {
        }
        Require(underflow.Scene.BackgroundClearValue == 0 && underflow.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 7, "BGCLEAR underflow changed PC or clear byte.");
        EventEngine width = Create(valid);
        JsonElement widthBefore = width.Capture();
        JsonNode malformed = JsonNode.Parse(widthBefore.GetRawText())!;
        malformed["Scene"]!["BackgroundClearValue"] = 256;
        try
        {
            width.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Saved background clear value outside BYTE was accepted.");
        }
        catch (JsonException)
        {
        }
        Require(JsonElement.DeepEquals(widthBefore, width.Capture()), "Invalid background clear byte changed the restored VM.");
        malformed["Scene"]!.AsObject().Remove("BackgroundClearValue");
        width.Restore(JsonSerializer.SerializeToElement(malformed));
        Require(width.Scene.BackgroundClearValue == 0, "Legacy missing clear byte was not retained as the fresh-field default.");

        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(170);
        string source = Path.Combine("../fixtures/external-fixture.json");
        GameSession session = new(GameSession.Load(source));
        EventEngine actual = new(session, session.State.Variables) { AnimationFrameCount = field.GetAnimationFrameCount };
        actual.LoadField(170, field.Jsm, field.Messages, session.State.FieldArrival);
        actual.Restore(session.State.EventState!.Value);
        Require(actual.Frame == 0 && actual.Scene.BackgroundClearValue == 0, "Original170 BGCLEAR preboundary changed.");
        actual.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(actual.Frame == 1 && actual.Scene.BackgroundClearValue == 1 && actual.LastFault is null, "Original170 ordinary Wait1 did not store the clear byte immediately.");
        GameSession independentSession = new(GameSession.Load(source));
        EventEngine independent = new(independentSession, independentSession.State.Variables) { AnimationFrameCount = field.GetAnimationFrameCount };
        independent.LoadField(170, field.Jsm, field.Messages, independentSession.State.FieldArrival);
        independent.Restore(actual.Capture());
        Require(JsonElement.DeepEquals(actual.Capture(), independent.Capture()), "Actual170 clear byte and complete VM did not restore independently.");
        Console.WriteLine("BGCLEAR: native low-BYTE1 immediate store, module states/animations untouched, field-load reset and independent restore; non1/special-field/underflow refusals preserve PC/operands, malformed saved BYTE rejected; actual170 frame0 ordinary Wait1 crosses to1.");
    }
}
