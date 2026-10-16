using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeRootTranslationAndBoundary(string gameDirectory)
    {
        EventEngine Create(byte[] content, int fieldId = 100)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session);
            engine.LoadField(fieldId, content, []);
            return engine;
        }

        foreach (int operand in new[] { 0, -30, 32767, 32768, 65535, 65566, -65566 })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 242), (Jsm.Opcode.SETGETA, 0), (Jsm.Opcode.PSHN_L, operand), (Jsm.Opcode.SETROOTTRANS, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            EventEngine engine = Create(content);
            engine.Step();
            ActorState actor = engine.Actors[0];
            JsonElement saved = engine.Capture();
            Require(actor.RootTranslationZ == unchecked((short)operand) && actor.GetaParameter == 242 && actor.X == 0 && actor.Y == 0 && actor.Z == 0 && actor.AnimationId == 0 && actor.DisplayOffset == default && saved.GetProperty("Fibers")[0].GetProperty("Stack").GetArrayLength() == 0, "SETROOTTRANS did not preserve its low signed WORD independently of GETA, placement and animation.");
            EventEngine independent = Create(content);
            independent.Restore(saved);
            Require(JsonElement.DeepEquals(saved, independent.Capture()), "SETROOTTRANS root word did not restore with the complete independent VM.");
            engine.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "Restored root word changed subsequent ordinary wait progression.");
        }

        byte[] unsupported = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, -30), (Jsm.Opcode.SETROOTTRANS, 0), (Jsm.Opcode.RET, 0));
        foreach (bool missingModel in new[] { true, false })
        {
            byte[] content = missingModel ? unsupported : SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, -30), (Jsm.Opcode.SETROOTTRANS, 0), (Jsm.Opcode.RET, 0));
            EventEngine engine = Create(content, missingModel ? 100 : 1);
            try
            {
                engine.Step();
                throw new InvalidOperationException("SETROOTTRANS silently admitted an absent model or unproved special-field mode.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement saved = engine.Capture();
            JsonElement fiber = saved.GetProperty("Fibers")[0];
            Require(engine.Actors[0].RootTranslationZ == 0 && engine.LastFault?.Opcode == "SETROOTTRANS" && fiber.GetProperty("Position").GetInt32() == (missingModel ? 2 : 3) && fiber.GetProperty("Stack")[0].GetInt32() == -30, "SETROOTTRANS refusal consumed its operand or changed PC/root word.");
            EventEngine restored = Create(content, missingModel ? 100 : 1);
            restored.Restore(saved);
            Require(JsonElement.DeepEquals(saved, restored.Capture()), "Refused SETROOTTRANS boundary did not restore exactly.");
        }

        byte[] empty = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 30), (Jsm.Opcode.SETROOTTRANS, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine underflow = Create(empty);
        underflow.Step();
        // The bytecode decoder rejects a statically empty stack. Exercise a restored runtime stack instead.
        JsonObject emptyStack = JsonNode.Parse(underflow.Capture().GetRawText())!.AsObject();
        emptyStack["Actors"]![0]!["RootTranslationZ"] = 0;
        emptyStack["Fibers"]![0]!["Position"] = 3;
        emptyStack["Fibers"]![0]!["WaitReason"] = "None";
        underflow.Restore(JsonSerializer.SerializeToElement(emptyStack));
        try
        {
            underflow.Step();
            throw new InvalidOperationException("SETROOTTRANS accepted an empty operand stack.");
        }
        catch (EventScriptException)
        {
        }
        Require(underflow.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 3 && underflow.Actors[0].RootTranslationZ == 0, "SETROOTTRANS underflow advanced PC or changed root translation.");
        byte[] module = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, -30), (Jsm.Opcode.SETROOTTRANS, 0), (Jsm.Opcode.RET, 0));
        module[2] = 1;
        module[3] = 0;
        EventEngine nonactor = Create(module);
        try
        {
            nonactor.Step();
            throw new InvalidOperationException("SETROOTTRANS accepted a background-module entity without a native actor backend.");
        }
        catch (EventScriptException)
        {
        }
        Require(nonactor.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 3 && nonactor.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == -30 && nonactor.Actors[0].RootTranslationZ == 0, "Nonactor root refusal changed PC, stack or root word.");
        JsonObject malformed = JsonNode.Parse(underflow.Capture().GetRawText())!.AsObject();
        malformed["Actors"]![0]!["RootTranslationZ"] = 32768;
        try
        {
            Create(empty).Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("A checkpoint root word outside signed16 was accepted.");
        }
        catch (JsonException)
        {
        }

        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(225);
        string source = TestResources.FixturePath("Events.Tests.RootTranslationTests.1");
        GameSession session = new(GameSession.Load(source));
        EventEngine actual = new(session) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted, ResolveGlobalSoundResource = MutedGlobalSample };
        actual.LoadField(225, field.Jsm, field.Messages, session.State.FieldArrival);
        actual.Restore(session.State.EventState!.Value);
        Require(actual.Frame == 2 && actual.Actors[11].RootTranslationZ == 0 && actual.Actors[11].ModelId == 10, "Original225 root-translation source changed its frame, actor model or initialized root word.");
        List<ScriptCommand> commands = [];
        actual.CommandExecuted += commands.Add;
        Require(actual.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted && actual.Frame == 3 && actual.LastFault is null && actual.Actors[11].RootTranslationZ == -30 && commands.Any(command => command.ObjectId == 11 && command.ScriptId == 47 && command.Operation == 437 && command.Name == "SETROOTTRANS"), "Original225 ordinary Wait did not execute the original negative root translation.");
        Require(actual.Actors[11].X == -547 && actual.Actors[11].Y == 192 && actual.Actors[11].Z == -245, "Root translation incorrectly changed original225 actor placement.");
        GameSession independentSession = new(GameSession.Load(source));
        EventEngine replay = new(independentSession) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted, ResolveGlobalSoundResource = MutedGlobalSample };
        replay.LoadField(225, field.Jsm, field.Messages, independentSession.State.FieldArrival);
        replay.Restore(actual.Capture());
        for (int tick = 0; tick < 4; tick++)
        {
            Require(JsonElement.DeepEquals(actual.Capture(), replay.Capture()), "Original225 independent VM root state differs.");
            Require(actual.Apply(new PlayerAction(PlayerActionKind.Wait)) == replay.Apply(new PlayerAction(PlayerActionKind.Wait)) && actual.LastFault is null && replay.LastFault is null, "Original225 restored root translation changed ordinary continuation.");
        }
        Require(JsonElement.DeepEquals(actual.Capture(), replay.Capture()), "Original225 final independent VM root state differs.");
        Console.WriteLine("Original225 SETROOTTRANS: ordinary Wait frame2 to3, actor11/model10 root Z=-30; signed WORD truncation, safe refusal and independent complete VM restoration verified.");
    }
}
