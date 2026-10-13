using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeActorShadowContract()
    {
        EventEngine Create(byte[] content, bool authenticated = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, content, [], nativeRuntimeProfile: authenticated ? NativeFieldRuntimeProfile.FF8En : null);
            return engine;
        }
        foreach (int size in new[] { 0, 1, 3, -1, -3, 4, -4, 300, 1024, 1027, -1024, -1027, 1028, -1028 })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.SHADELEVEL, 0), (Jsm.Opcode.PSHN_L, size), (Jsm.Opcode.SHADESET, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            EventEngine engine = Create(content);
            Require(engine.Actors[0].ShadeLevel == 96 && engine.Actors[0].NativeShadowLevel is null && engine.Actors[0].NativeShadowSet is null && engine.Actors[0].NativeShadowAllocation is { Level609: 96 }, "Authenticated allocation lost native96 or invented an executed shadow command.");
            engine.Step();
            ActorState actor = engine.Actors[0];
            Require(actor.NativeShadowSet is { } set && set.RawSize == size && set.Radius601To608 == unchecked((byte)(size / 4)) && actor.NativeShadowLevel is { RawLevel: 511, Level609: 255 } && actor.ShadeLevel == 255 && actor.PolygonColor == new Vector3(128) && engine.Capture().GetProperty("Variables").GetProperty("500").GetInt32() == 7, "SHADESET/SHADELEVEL narrowing, immediate return or independent polygon color changed.");
            JsonElement saved = engine.Capture();
            EventEngine independent = Create(content);
            independent.Restore(saved);
            Require(JsonElement.DeepEquals(saved, independent.Capture()), "Executed actor shadow bytes did not restore independently.");
            engine.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "Restored actor shadow changed ordinary callbacks.");

            JsonNode malformed = JsonNode.Parse(saved.GetRawText())!;
            malformed["Actors"]![0]!["NativeShadowSet"]!["Radius601To608"] = unchecked((byte)(size / 4 + 1));
            string before = engine.Capture().GetRawText();
            try
            {
                engine.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("A nonzero or conflicting shadow shape was restored.");
            }
            catch (InvalidDataException)
            {
            }
            Require(before == engine.Capture().GetRawText(), "Malformed shadow checkpoint refusal mutated the VM.");
            engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            Require(engine.Actors[0].NativeShadowSet is null && engine.Actors[0].NativeShadowLevel is null && engine.Actors[0].ShadeLevel == 96, "New allocation retained another field actor's executed shadow receipt.");
        }
        foreach (int size in new[] { 4, -4, 1028, -1028 })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, size), (Jsm.Opcode.SHADESET, 0), (Jsm.Opcode.RET, 0));
            EventEngine engine = Create(content, false);
            try
            {
                engine.Step();
                throw new InvalidOperationException("Unknown native shadow geometry acquired table/byte authority.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == "SHADESET")
            {
            }
            JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
            Require(engine.Actors[0].NativeShadowSet is null && fiber.GetProperty("Position").GetInt32() == 2 && fiber.GetProperty("Stack")[0].GetInt32() == size, "Unsupported shadow shape consumed its operand or mutated the actor.");
        }
        EventEngine unproved = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SHADESET, 0), (Jsm.Opcode.RET, 0)), false);
        try
        {
            unproved.Step();
            throw new InvalidOperationException("Unknown native profile acquired shadow-radius authority.");
        }
        catch (EventScriptException) when (unproved.LastFault?.Opcode == "SHADESET")
        {
        }
        Require(unproved.Actors[0].NativeShadowSet is null, "Unknown profile refusal changed shadow state.");
        Require(unproved.Actors[0].ShadeLevel == 96 && unproved.Actors[0].NativeShadowAllocation is null, "Legacy default96 was converted to native allocation authority.");
    }
}
