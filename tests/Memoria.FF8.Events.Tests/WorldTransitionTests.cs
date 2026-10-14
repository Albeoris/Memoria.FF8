using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeWorldTransitionTransaction()
    {
        NativeWorldControlState known = NativeWorldControlState.CreateNewGame() with { Menu209 = 0xA5 };
        (EventEngine Engine, GameSession Session) Create(byte[] program, NativeWorldControlState? controls = null, bool bind = true)
        {
            GameSession session = new(new GameState { FieldId = 100, WorldControls = controls });
            EventEngine engine = new(session, session.State.Variables);
            if (bind)
            {
                engine.PrepareWorldTransition = request => () =>
                {
                    // Explicit synthetic backend. Production App's prepared
                    // Runtime commit publishes packet and controls atomically.
                    session.SetWorldControls(request.ControlsAtLoader);
                    session.RequestWorld(request.Transition);
                };
            }
            engine.LoadField(100, program, []);
            return (engine, session);
        }
        byte[] jump = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, -65536), (Jsm.Opcode.PSHN_L, 0x12345), (Jsm.Opcode.WORLDMAPJUMP, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0));
        foreach (bool displayed in new[] { false, true })
        {
            (EventEngine engine, GameSession session) = Create(jump, known);
            NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(600).Display(250, 10);
            session.SetExamTimer(displayed ? timer : timer.KillDisplay());
            engine.Step();
            Require(engine.LastFault is null && session.State.Scene == SceneKind.WorldMap && session.ExamTimer!.ClockCalls == 2 && session.ExamTimer.DisplayEnabled == displayed, "Tracked displayed or killed timer blocked a prepared world departure or lost its final field callback.");
            JsonElement frozen = engine.Capture();
            engine.Step();
            Require(JsonElement.DeepEquals(frozen, engine.Capture()), "Frozen departing field VM incorrectly supplied further normal field timer clocks.");
        }
        foreach (bool disabledSource in new[] { false, true })
        {
            NativeWorldControlState controls = known with { SourceDisabled = disabledSource, Menu209 = disabledSource ? (byte)0xA7 : known.Menu209 };
            (EventEngine engine, GameSession session) = Create(jump, controls);
            int prepares = 0;
            Func<NativeWorldTransitionRequest, Action?> original = engine.PrepareWorldTransition!;
            engine.PrepareWorldTransition = request =>
            {
                prepares++;
                Require(request.Origin == NativeWorldTransitionOrigin.Script && request.ControlsAtDeparture == controls && request.ControlsAtIssue == controls.WithWorldEntryMenuFlag() && request.ControlsAtLoader == request.ControlsAtIssue && request.Transition == new WorldTransition(0, 0, 0x2345), "Scripted world preparation changed low-WORD packet or issue/loader flag stages.");
                return original(request);
            };
            engine.Step();
            NativeWorldJumpWait wait = engine.Scene.WorldJumpWaits.Values.Single();
            Require(prepares == 1 && !wait.Suppressed && wait.Operation == 4 && wait.Operands.SequenceEqual(new[] { 65536, -65536, 0x12345 }) && session.State.WorldArrival == new WorldTransition(0, 0, 0x2345) && session.WorldControls == controls.WithWorldEntryMenuFlag() && engine.NextTrigger.Kind == PlayerTriggerKind.WorldMap && !session.State.Variables.ContainsKey(512), "Enabled world jump did not commit once and hold its original issuing PC.");
            JsonElement saved = engine.Capture();
            (EventEngine restored, _) = Create(jump, known);
            restored.Restore(saved);
            restored.Step();
            Require(JsonElement.DeepEquals(saved, restored.Capture()), "Issued world packet/flags/PC wait changed after independent VM restore.");
        }

        foreach (int failure in new[] { 0, 1, 2, 3 })
        {
            (EventEngine engine, GameSession session) = Create(jump, failure == 3 ? null : known);
            int commits = 0;
            engine.PrepareWorldTransition = failure switch
            {
                0 => _ => null,
                1 => _ => throw new NotSupportedException("Synthetic resource preflight refusal."),
                2 => _ => () => throw new InvalidOperationException("Synthetic atomic commit refusal before publication."),
                _ => _ => () => commits++
            };
            GameState before = session.State;
            try
            {
                engine.Step();
                throw new InvalidOperationException("Unsupported scripted world entry consumed native operands.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement fiber = engine.Capture().GetProperty("Fibers")[0];
            Require(commits == 0 && ReferenceEquals(before, session.State) && engine.Scene.WorldTransitionRequest is null && engine.Scene.WorldJumpWaits.Count == 0 && engine.LastFault is { Opcode: "WORLDMAPJUMP", Operation: 4 } && fiber.GetProperty("Position").GetInt32() == 4 && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 0x12345, -65536, 65536 }), "Failed world preparation/commit changed packet, controls, issuing PC or three operands.");
        }
        (EventEngine aliases, GameSession aliasSession) = Create(jump, known);
        aliasSession.State.Variables[104] = 0x200;
        int aliasPrepares = 0;
        aliases.PrepareWorldTransition = _ =>
        {
            aliasPrepares++;
            return () => { };
        };
        try
        {
            aliases.Step();
        }
        catch (EventScriptException)
        {
        }
        Require(aliasPrepares == 0 && aliases.LastFault?.Operation == 4 && aliases.Scene.WorldJumpWaits.Count == 0, "Unknown legacy L104 alias entered world resource preparation.");

        byte[] sticky = SyntheticObjects([0, (ushort)(1 << 7)],
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.MAPJUMPOFF, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WORLDMAPJUMP, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.MAPJUMPON, 0), (Jsm.Opcode.RET, 0)]
        ]);
        (EventEngine suppressed, GameSession suppressedSession) = Create(sticky);
        int suppressedPrepares = 0;
        suppressed.PrepareWorldTransition = _ =>
        {
            suppressedPrepares++;
            return () => { };
        };
        suppressed.Step();
        JsonElement blocked = suppressed.Capture();
        Require(suppressed.Scene.MapJumpDisabled && suppressed.Scene.WorldJumpWaits.Values.Single().Suppressed && suppressed.Scene.WorldTransitionRequest is null && suppressedSession.WorldControls is null, "Disabled world jump wrote packet/flags or lost its first-entry wait.");
        (EventEngine resumed, _) = Create(sticky);
        resumed.PrepareWorldTransition = suppressed.PrepareWorldTransition;
        resumed.Restore(blocked);
        for (int tick = 0; tick < 5; tick++)
        {
            suppressed.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(suppressed.Capture(), resumed.Capture()), "Disabled sticky wait changed when an independent script enabled gateways after restore.");
        }
        Require(!suppressed.Scene.MapJumpDisabled && suppressedPrepares == 0 && !suppressedSession.State.Variables.ContainsKey(512) && suppressed.Scene.WorldJumpWaits.Values.Single().Operands.SequenceEqual(new[] { 3, 4, 5 }), "MAPJUMPON retried a previously suppressed world instruction or repopped its empty stack.");

        JsonElement valid = suppressed.Capture();
        JsonNode invalid = JsonNode.Parse(valid.GetRawText())!;
        string key = suppressed.Scene.WorldJumpWaits.Keys.Single().ToString();
        invalid["Scene"]!["WorldJumpWaits"]![key]!["Operation"] = 6;
        try
        {
            suppressed.Restore(JsonSerializer.SerializeToElement(invalid));
            throw new InvalidOperationException("Corrupted world-jump issuing PC passed restore validation.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(valid, suppressed.Capture()), "Invalid world wait restore partially mutated the VM.");

        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        byte[] gateProgram = SyntheticObjects([(16 << 7) | 2, 0], [player, [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)]]);
        gateProgram[0] = 1;
        gateProgram[3] = 1;
        Vector3 start = new(0, -20, 0);
        Vector3 end = new(0, 20, 0);
        byte[] raw = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(2), -20);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(8), 20);
        for (int index = 20; index < 28; index++)
        {
            raw[index] = (byte)(0xA0 + index);
        }
        InfGateway gateway = new(start, end, new FieldTransition(0, 0, 0, 0, 0), 2, raw);
        InfAreaTrigger trigger = new(start, end, 0, 0);
        foreach (bool refuse in new[] { false, true })
        {
            (EventEngine gate, GameSession session) = Create(gateProgram, known);
            gate.Step();
            gate.InfGateways = [gateway];
            gate.InfAreaTriggers = [trigger];
            int prepares = 0;
            Func<NativeWorldTransitionRequest, Action?> original = gate.PrepareWorldTransition!;
            gate.PrepareWorldTransition = request =>
            {
                prepares++;
                Require(request.Origin == NativeWorldTransitionOrigin.Gateway && request.ControlsAtIssue == known && request.ControlsAtLoader == known.WithWorldEntryMenuFlag() && request.Gateway?.NativeIndex == 2 && request.Gateway.NativeRecord!.SequenceEqual(raw), "INF world preparation changed raw gateway index/record or issue-stage flags.");
                return refuse ? null : original(request);
            };
            JsonElement before = gate.Capture();
            try
            {
                CommitComponentPlayer(gate, 1, 0, 0, 0);
                Require(!refuse, "Rejected gateway preparation committed movement.");
            }
            catch (NotSupportedException) when (refuse)
            {
            }
            if (refuse)
            {
                Require(JsonElement.DeepEquals(before, gate.Capture()) && session.State.WorldArrival is null && session.WorldControls == known, "World gateway refusal mutated field contacts, XYZ or flags.");
            }
            else
            {
                Require(prepares == 1 && session.State.WorldArrival == new WorldTransition(0, 0, 0) && gate.Actors[1].DoorLineEntryPending && gate.Scene.WorldTransitionRequest?.Gateway?.NativeRecord is not null && gate.NextTrigger.Kind == PlayerTriggerKind.WorldMap, "World gateway failed to commit original packet and deferred area contact.");
                (EventEngine restored, _) = Create(gateProgram, known);
                restored.InfGateways = [gateway];
                restored.Restore(gate.Capture());
                Require(JsonElement.DeepEquals(gate.Capture(), restored.Capture()), "INF request/full original record did not survive independent checkpoint restore.");
            }
        }
        byte[] negativeMode = raw.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(negativeMode.AsSpan(16), -1);
        (EventEngine negativeGate, _) = Create(gateProgram, known);
        negativeGate.Step();
        negativeGate.InfGateways = [gateway with { Destination = gateway.Destination with { Triangle = -1 }, NativeRecord = negativeMode }];
        int negativeModePrepares = 0;
        negativeGate.PrepareWorldTransition = request =>
        {
            negativeModePrepares++;
            Require(request.Transition.Mode == -1, "Negative raw world mode was unsigned or treated as a field sentinel.");
            return null;
        };
        JsonElement negativeBefore = negativeGate.Capture();
        try
        {
            CommitComponentPlayer(negativeGate, 1, 0, 0, 0);
            throw new InvalidOperationException("Negative world mode skipped required backend preflight refusal.");
        }
        catch (NotSupportedException)
        {
        }
        Require(negativeModePrepares == 1 && JsonElement.DeepEquals(negativeBefore, negativeGate.Capture()), "Negative world mode failed to preserve original state after reaching preparation.");
        (Jsm.Opcode, int)[] disabledPlayer = player.Take(player.Length - 1).Concat(new[] { (Jsm.Opcode.MAPJUMPOFF, 0), (Jsm.Opcode.RET, 0) }).ToArray();
        byte[] disabledGateProgram = SyntheticObjects([(16 << 7) | 2, 0], [disabledPlayer, [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 0)]]);
        disabledGateProgram[0] = 1;
        disabledGateProgram[3] = 1;
        (EventEngine disabledGate, GameSession disabledGateSession) = Create(disabledGateProgram);
        disabledGate.Step();
        disabledGate.InfGateways = [gateway with { NativeRecord = null, NativeIndex = -1 }, gateway with { Destination = new FieldTransition(143, 0, 0, 0, 0) }];
        disabledGate.InfAreaTriggers = [trigger];
        CommitComponentPlayer(disabledGate, 1, 0, 0, 0);
        Require(disabledGate.Scene.MapJumpDisabled && disabledGateSession.State.WorldArrival is null && disabledGateSession.State.FieldArrival is null && disabledGate.Actors[1].DoorLineEntryPending, "MAPJUMPOFF failed to suppress the entire world/field gateway scan while preserving areas.");
        Console.WriteLine("Native world transitions: enabled low-WORD script packet with atomic preflight and two flag stages; disabled sticky issuing-PC wait survives later enable/restore; full indexed INF record, gateway refusal and disabled scan preserve area/field transaction boundaries.");
    }
}
