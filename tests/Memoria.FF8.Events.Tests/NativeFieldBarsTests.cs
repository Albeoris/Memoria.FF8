using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFieldBarsContract(string gameDirectory)
    {
        NativeFieldBarsState hidden = NativeFieldBarsState.CreateHidden();
        NativeFieldBarsState fullWidth = hidden.Display(0, 65546, -65516, 1, 65536, 65636, 50);
        Require(fullWidth.First is { Mirror: { X: 10, Y: 20, Minimum: 0, Maximum: 100, Value: 50 }, Display.Fill: 0 }, "DISPBAR narrowed initial scale operands before the original full-DWORD UI call.");
        Require(fullWidth.Advance(2).First?.Display.Fill == 4 && fullWidth.Advance(4).First?.Display.Fill == 8, "Native bar smoother did not use signed retained WORDs and two units per UI tick.");
        Require(hidden.Display(0, 0, 0, 0, 10, 10, 9).First?.Display.Fill == 0 && hidden.Display(0, 0, 0, 0, 10, 10, 10).First?.Display.Fill == 96, "Equal bounds introduced division by zero instead of native branch ordering.");
        NativeFieldBarsState immediate = hidden.Display(0, 10, 20, 3, 0, 100, 100).SetValue(0, 25);
        Require(immediate.First?.Display.Fill == 24 && immediate.Advance(2).First?.Display.Flash == true && immediate.Advance(2).Advance(2).First?.Display.Flash == false, "SETBAR bit2 or inclusive quarter-range draw flash changed.");
        NativeFieldBarsState smooth = hidden.Display(1, 30, 40, 0, -100, 100, 100).SetValue(1, -50);
        Require(smooth.Second?.Display.Fill == 96 && smooth.Advance(2).Second?.Display.Fill == 92 && smooth.Advance(4).Second?.Display.Fill == 88, "SETBAR discarded smoothing or signed negative range arithmetic.");
        NativeFieldBarsState killed = smooth.Hide(1);
        Require(killed.Second is { Mirror.Mode: 0, Display.Visible: false, Display.Value: -50, Display.Fill: 96 } && killed.Advance(2) == killed, "KILLBAR changed retained geometry/value or hidden smoother state.");
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        (Jsm.Opcode, int)[] operations = [
            (Jsm.Opcode.PSHN_L, 77),
            (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.DISPBAR, 0),
            (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETBAR, 0),
            (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0),
            (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.KILLBAR, 0), (Jsm.Opcode.PSHN_L, 78), (Jsm.Opcode.POPM_B, 5001)
        ];
        (EventEngine live, GameSession session) = CreateNativeBarFixture(vitals, economy, operations);
        for (int tick = 0; tick < 16 && !CapturedVariables(live.Capture()).TryGetProperty("5000", out _); tick++)
        {
            live.Step();
        }
        Require(CapturedVariables(live.Capture()).TryGetProperty("5000", out JsonElement sentinel) && sentinel.GetInt32() == 77 && !CapturedVariables(live.Capture()).TryGetProperty("5001", out _) && session.FieldBars?.First is { Mirror.Value: 0, Display.Fill: 92, Display.Flash: true }, "Bar command operands, sentinel, return2 continuation or ordinary UI callback progression changed.");
        JsonElement issued = live.Capture();
        (EventEngine restored, GameSession restoredSession) = CreateNativeBarFixture(vitals, economy, operations);
        restored.Restore(issued);
        for (int tick = 0; tick < 3; tick++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()) && session.FieldBars == restoredSession.FieldBars, "Native bar mirror/UI state diverged after independent restore.");
        }
        Require(CapturedVariables(live.Capture()).GetProperty("5001").GetInt32() == 78 && session.FieldBars?.First?.Display.Visible == false, "KILLBAR yielded or failed to remove its actual displayed bar.");
        session.SetFieldBars(smooth);
        NativeFieldBarsState retained = session.FieldBars!;
        session.RequestField(152, 0);
        Require(session.FieldBars == retained, "Ordinary field load reset the global bar records needed by field152.");
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.DISPBAR, Jsm.Opcode.SETBAR, Jsm.Opcode.KILLBAR })
        {
            int count = opcode == Jsm.Opcode.DISPBAR ? 7 : opcode == Jsm.Opcode.SETBAR ? 2 : 1;
            (Jsm.Opcode, int)[] missing = Enumerable.Repeat((Jsm.Opcode.PSHN_L, 0), count - 1).Append((opcode, 0)).ToArray();
            (EventEngine underflow, GameSession underflowSession) = CreateNativeBarFixture(vitals, economy, missing);
            AssertNativeBarRefusal(underflow, underflowSession, opcode, count - 1);
        }
        (EventEngine invalid, GameSession invalidSession) = CreateNativeBarFixture(vitals, economy, [(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.KILLBAR, 0)]);
        AssertNativeBarRefusal(invalid, invalidSession, Jsm.Opcode.KILLBAR, 1);
        (EventEngine opaque, GameSession opaqueSession) = CreateNativeBarFixture(vitals, economy, [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 50), (Jsm.Opcode.SETBAR, 0)]);
        AssertNativeBarRefusal(opaque, opaqueSession, Jsm.Opcode.SETBAR, 2);
        (EventEngine unknown, GameSession unknownSession) = CreateNativeBarFixture(vitals, economy, operations, known: false);
        AssertNativeBarRefusal(unknown, unknownSession, Jsm.Opcode.DISPBAR, 8);
        JsonObject malformed = JsonNode.Parse(issued.GetRawText())!.AsObject();
        malformed["FieldBars"]!["First"]!["Display"]!["Width"] = 95;
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Malformed native bar geometry was accepted.");
        }
        catch (InvalidDataException)
        {
        }
        Console.WriteLine("Native field bars: original operand order/widths, immediate return2, two global mirrors/UI records, signed scaling, 2/4 smoothing, draw flash, KILL retention, field transition, atomic underflow/index/unknown refusal and independent restore passed.");
    }

    private static (EventEngine Engine, GameSession Session) CreateNativeBarFixture(NativeCharacterVitals vitals, NativeEconomy economy, (Jsm.Opcode, int)[] operations, bool known = true)
    {
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        if (known)
        {
            session.SetFieldBars(NativeFieldBarsState.CreateHidden());
        }
        else
        {
            session.Restore(session.State with { FieldBars = null });
        }
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 3 };
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] main = new[] { (Jsm.Opcode.LBL, 1) }.Concat(operations).Concat([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]).ToArray();
        engine.LoadField(600, SyntheticObjects([1], [init, main]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return (engine, session);
    }

    private static void AssertNativeBarRefusal(EventEngine engine, GameSession session, Jsm.Opcode opcode, int expectedStack)
    {
        NativeFieldBarsState? before = session.FieldBars;
        try
        {
            for (int tick = 0; tick < 8; tick++)
            {
                engine.Step();
            }
            throw new InvalidOperationException("Unsupported native bar command was accepted.");
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == opcode.ToString())
        {
        }
        JsonElement fiber = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(value => value.GetProperty("ScriptId").GetInt32() == 1);
        Require(fiber.GetProperty("Position").GetInt32() == engine.LastFault!.Operation && fiber.GetProperty("Stack").GetArrayLength() == expectedStack && session.FieldBars == before, "Native bar refusal consumed operands, advanced its PC or mutated its global records.");
    }
}
