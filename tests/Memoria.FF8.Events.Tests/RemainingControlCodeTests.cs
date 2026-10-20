using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static byte[] DefaultRoutineProgram(params (Jsm.Opcode Opcode, int Parameter)[] operations)
    {
        return SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)], operations]);
    }

    private static void RemainingSoundCodeContract()
    {
        NativeZeroMaskDepartureLookupContract();
        foreach (bool transition in new[] { false, true })
        {
            foreach (int raw in new[] { -1, 0, 1, 127, 128, 255, 256, int.MaxValue })
            {
                List<(Jsm.Opcode, int)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1234)];
                if (transition)
                {
                    operations.Add((Jsm.Opcode.PSHN_L, 261));
                }
                operations.AddRange([(Jsm.Opcode.PSHM_L, 512), (transition ? Jsm.Opcode.ALLSEPOSTRANS : Jsm.Opcode.ALLSEPOS, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
                byte[] content = SyntheticProgram(operations.ToArray());
                EventEngine Create()
                {
                    GameSession session = CreateControlledMusicSession();
                    session.SetEventMemory(session.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(raw)));
                    EventEngine created = new(session);
                    created.LoadField(100, content, []);
                    return created;
                }
                EventEngine engine = Create();
                List<object> outputs = [];
                engine.AudioCommandExecuted += outputs.Add;
                engine.Step();
                NativeAllSoundPosition pan = engine.Scene.AllSoundPosition ?? throw new InvalidOperationException("Missing all-voice pan.");
                int expected = transition ? (byte)unchecked(2 * raw) >> 1 : (byte)raw >> 1;
                Require(pan.Pan == expected && pan.RawPan == raw && pan.DurationByte == (transition ? 5 : 0) && pan.DurationMilliseconds == (transition ? 50 : 0) && outputs.Count == 1, "All-voice pan changed native BYTE order, duration or output count.");
                JsonElement snapshot = engine.Capture();
                Require(snapshot.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 1234, "All-voice pan consumed an unrelated operand.");
                EventEngine restored = Create();
                restored.Restore(snapshot);
                engine.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "All-voice pan restore diverged.");
                JsonObject corrupt = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
                corrupt["Scene"]!["AllSoundPosition"]!["Pan"] = pan.Pan ^ 1;
                bool rejected = false;
                try
                {
                    Create().Restore(JsonSerializer.SerializeToElement(corrupt));
                }
                catch (InvalidDataException)
                {
                    rejected = true;
                }
                Require(rejected, "All-voice pan accepted a forged BYTE projection.");
            }
        }

        foreach (int keep in new[] { 0, 1, 0xFFFFFF, -1 })
        {
            byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.Unknown14, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            GameSession session = CreateControlledMusicSession();
            session.SetEventMemory(session.State.EventMemory.Write(512, 4, NativeScalarValue.Exact(keep)));
            EventEngine engine = new(session);
            engine.LoadField(100, content, []);
            engine.Step();
            Require(engine.Scene.NativeFieldSoundStopMask == ~unchecked((uint)keep), "Unknown14 lost DWORD complement semantics.");
            EventEngine restored = new(CreateControlledMusicSession());
            restored.LoadField(100, content, []);
            restored.Restore(engine.Capture());
            NativeFieldSoundDeparture? departure = null;
            restored.AudioCommandExecuted += command => departure = command as NativeFieldSoundDeparture;
            restored.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
            int[] selected = keep is -1 or 0xFFFFFF or 0 ? [] : Enumerable.Range(2, 23).ToArray();
            Require(departure is not null && departure.Voices.SequenceEqual(selected) && restored.Scene.NativeFieldSoundStopMask == 0xFFFFFF, "Field departure lost the custom keep mask, voice bits or loader reset.");
        }
    }

    private static void NativeZeroMaskDepartureLookupContract()
    {
        GameSession session = CreateControlledMusicSession();
        session.SetGlobalSoundProcess(session.GlobalSoundProcess! with { OriginalEffectIds = session.GlobalSoundProcess!.OriginalEffectIds.SetItem(7, 5534).SetItem(8, 5534) });
        EventEngine engine = new(session);
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.Unknown14, 0), (Jsm.Opcode.RET, 0));
        engine.LoadField(100, content, []);
        engine.Step();
        NativeFieldSoundDeparture? departure = null;
        engine.AudioCommandExecuted += command => departure = command as NativeFieldSoundDeparture;
        engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        Require(departure is not null && departure.Voices.SequenceEqual(new[] { 7 }), "Zero-mask departure failed the original mapped ushort(-2) first-voice lookup.");
    }

    private static void RemainingControlCodeContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.STOPVIBRATE, 0), (Jsm.Opcode.KEYSIGHNCHANGE, 0), (Jsm.Opcode.DEBUG, 0), (Jsm.Opcode.PSHN_L, 71), (Jsm.Opcode.POPI_L, 2), (Jsm.Opcode.ENDING, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            EventEngine created = new(CreateDirectedLinearTurnSession(vitals, economy));
            created.LoadField(100, content, [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
            return created;
        }
        EventEngine engine = Create();
        engine.Step();
        engine.Step();
        Require(engine.NativeDebugRequested && !engine.Capture().GetProperty("Fibers")[1].GetProperty("Results").TryGetProperty("2", out _), "DEBUG failed to yield its native turn.");
        engine.Step();
        JsonElement state = engine.Capture();
        Require(state.GetProperty("Fibers")[1].GetProperty("Position").GetInt32() == 9 && state.GetProperty("Fibers")[1].GetProperty("Results").GetProperty("2").GetInt32() == 71, "ENDING failed to retain its PC or no-op handlers consumed the wrong operands.");
        EventEngine restored = Create();
        restored.Restore(state);
        engine.Step();
        restored.Step();
        Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "ENDING/DEBUG restore diverged.");

        GameSession controls = CreateControlledMusicSession();
        byte[] suppressed = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.MOVIECUT, 0), (Jsm.Opcode.PSHN_L, 9999), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.MOVIESYNC, 0), (Jsm.Opcode.BATTLECUT, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.RET, 0));
        EventEngine cut = new(controls);
        cut.LoadField(100, suppressed, []);
        cut.Step();
        Require(controls.State.WorldControls is { MusicKeep207: 1 } native && (native.EventFlags104 & 0x120) == 0x120 && cut.Scene.MovieActors is null && cut.Capture().GetProperty("Fibers")[0].GetProperty("Complete").GetBoolean(), "Cut commands lost canonical flags, consumed an unsupported movie resource or yielded a suppressed movie.");
    }

    private static void RemainingDialogueCodeContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession Session()
        {
            GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            return session;
        }
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RAMESW, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RAMESW, 0), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.MESFORCUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8));
        EventEngine Create()
        {
            EventEngine created = new(Session());
            created.LoadField(100, content, ["First", "Second"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return created;
        }
        EventEngine engine = Create();
        engine.Step();
        engine.Step();
        engine.Step();
        Require(engine.Scene.NativeDialogueFocus == 0 && engine.CurrentDialogue?.Channel == 0, "MESFORCUS failed to truncate to BYTE or select the explicit window.");
        EventEngine restored = Create();
        restored.Restore(engine.Capture());
        Require(engine.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && restored.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted && engine.Dialogues.Count == 0 && engine.CurrentDialogue is null, "Global confirm did not preserve native ascending broadcast closure of ready automatic windows.");
        Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Explicit focus restore/decision diverged.");

        byte[] choices = DefaultRoutineProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.MESFORCUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.ASK, 0), (Jsm.Opcode.RET, 8));
        EventEngine ask = new(Session());
        ask.LoadField(100, choices, ["Choose"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        ask.Step();
        ask.Step();
        Require(ask.Scene.NativeDialogueFocus == 7 && ask.Scene.NativeAskPreviousFocus == 7 && ask.CurrentDialogue is null && ask.Dialogues.Single() is { Channel: 2, X: 0, Y: 0 }, "ASK must capture the shared previous marker and preserve focus on issue.");
        PlayerActionResult firstChoicePoll = ask.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(firstChoicePoll.Accepted && firstChoicePoll.FramesAdvanced == 1 && firstChoicePoll.Trigger.Kind == PlayerTriggerKind.Dialogue && ask.CurrentDialogue is { Channel: 2 }, "ASK failed to select its channel on the first incomplete poll.");
        EventEngine askRestored = new(Session());
        askRestored.LoadField(100, choices, ["Choose"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        askRestored.Restore(ask.Capture());
        Require(ask.Apply(new PlayerAction(PlayerActionKind.Choose, 4)).Accepted && askRestored.Apply(new PlayerAction(PlayerActionKind.Choose, 4)).Accepted, "Ordinary Choose did not accept the polled native choice.");
        Require(ask.Scene.NativeDialogueFocus == 2, "Choice selection restored focus before the VM completion poll.");
        for (int callback = 0; callback < 8 && ask.Dialogues.Count != 0; callback++)
        {
            ask.Step();
            askRestored.Step();
        }
        Require(ask.Scene.NativeDialogueFocus == 7 && ask.Capture().GetProperty("Fibers")[1].GetProperty("Results").GetProperty("0").GetInt32() == 1 && JsonElement.DeepEquals(ask.Capture(), askRestored.Capture()), "ASK failed to restore focus or publish relative choice R0 across independent restore.");

        byte[] message = DefaultRoutineProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MESW, 0), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8));
        EventEngine mesw = new(Session());
        mesw.LoadField(100, message, ["Wait"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        mesw.Step();
        mesw.Step();
        Require(mesw.CurrentDialogue is { Channel: 3, CanConfirm: true }, "MESW failed to open its default-position blocking window.");
        mesw.ConfirmDialogue();
        for (int callback = 0; callback < 8 && !mesw.Capture().GetProperty("Fibers")[1].GetProperty("Results").TryGetProperty("4", out _); callback++)
        {
            mesw.Step();
        }
        Require(mesw.Capture().GetProperty("Fibers")[1].GetProperty("Results").GetProperty("4").GetInt32() == 17, "MESW did not release its fiber after confirmation.");
    }
}
