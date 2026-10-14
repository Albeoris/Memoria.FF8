using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeScrollRatioContract()
    {
        foreach (int layer in new[] { 0, 1, 7 })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, layer), (Jsm.Opcode.PSHN_L, 35), (Jsm.Opcode.PSHN_L, -19), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.SCROLLMODE2, 0), (Jsm.Opcode.PSHN_L, layer), (Jsm.Opcode.PSHN_L, 65537), (Jsm.Opcode.PSHN_L, -65537), (Jsm.Opcode.SCROLLRATIO2, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            EventEngine Create()
            {
                GameSession session = new(new GameState());
                EventEngine engine = new(session, session.State.Variables) { CanSetBackgroundScroll = (_, _) => true, CanSetBackgroundScrollRatio = (_, _) => true };
                engine.LoadField(100, content, []);
                return engine;
            }
            EventEngine live = Create();
            live.Step();
            Require(live.LastFault is null && live.Scene.BackgroundScrollLayers[layer] == new BackgroundScrollSettings(35, -19, 1, -1) && live.Capture().GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 9, "SCROLLRATIO2 changed offset words, lost signedWORD narrowing or yielded instead of return2.");
            EventEngine restored = Create();
            restored.Restore(live.Capture());
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "SCROLLRATIO2 retained offset/ratio authority failed independent continuation.");
        }

        byte[] allocated = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SCROLLRATIO2, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine CreateNative()
        {
            GameSession session = GameSession.CreateColdProcess();
            EventEngine engine = new(session, session.State.Variables) { CanSetBackgroundScrollRatio = (_, _) => true };
            engine.LoadField(100, allocated, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine native = CreateNative();
        Require(native.Scene.BackgroundScrollLayers.Count == 8 && native.Scene.BackgroundScrollLayers.Values.All(settings => settings == new BackgroundScrollSettings(0, 0, 256, 256)), "The actual native loader did not write all eight current four-word records.");
        native.Step();
        Require(native.Scene.BackgroundScrollLayers[1] == new BackgroundScrollSettings(0, 0, 0, 0) && native.Scene.BackgroundScrollLayers[0] == new BackgroundScrollSettings(0, 0, 256, 256), "Ratio-only mutation failed to retain the executed allocation offsets and other records.");
        EventEngine receiver = CreateNative();
        receiver.Restore(native.Capture());
        JsonNode invalid = JsonNode.Parse(native.Capture().GetRawText())!;
        invalid["Scene"]!["BackgroundScrollLayers"]!.AsObject().Remove("7");
        JsonElement before = receiver.Capture();
        try
        {
            receiver.Restore(JsonSerializer.SerializeToElement(invalid));
            throw new InvalidOperationException("Absent native loader record was accepted as a renderer default.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, receiver.Capture()), "Rejected absent native record changed the receiver.");
        native.LoadField(101, allocated, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(native.Scene.BackgroundScrollLayers.Count == 8 && native.Scene.BackgroundScrollLayers[1] == new BackgroundScrollSettings(0, 0, 256, 256), "A new executed native field load failed to reset the old field ratio.");

        foreach (int layer in new[] { -1, 8, 65537, 1 })
        {
            GameSession session = new(new GameState());
            EventEngine refused = new(session, session.State.Variables) { CanSetBackgroundScrollRatio = (_, _) => true };
            refused.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, layer), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SCROLLRATIO2, 0), (Jsm.Opcode.RET, 0)), []);
            try
            {
                refused.Step();
                throw new InvalidOperationException("Unknown offsets or out-of-range fullDWORD layer were accepted.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == "SCROLLRATIO2")
            {
            }
            JsonElement fiber = refused.Capture().GetProperty("Fibers")[0];
            Require(fiber.GetProperty("Position").GetInt32() == 4 && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 0, 0, layer }) && refused.Scene.BackgroundScrollLayers.Count == 0, "SCROLLRATIO2 refusal truncated the DWORD index, consumed operands or invented offset defaults.");
        }
        Console.WriteLine("SCROLLRATIO2: retained offsets, fullDWORD index, low signedWORD ratios, return2, executed8-row allocation/reset, unknown admission refusal and atomic native restore verified.");
    }
}
