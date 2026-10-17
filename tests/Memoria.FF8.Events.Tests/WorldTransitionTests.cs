using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeWorldTransitionTransaction(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        NativeEconomy initialEconomy = assets.ReadNewGameEconomy();
        NativeInputConfiguration inputConfiguration = assets.ReadNewGameInputConfiguration();
        FieldAssets animationAssets = assets.LoadField(159);
        NativeWorldControlState known = NativeWorldControlState.CreateNewGame() with { Menu209 = 0xA5 };
        (EventEngine Engine, GameSession Session) Create(byte[] program, NativeWorldControlState? controls = null, bool bind = true, bool contacts = false)
        {
            GameSession cold = CreateDirectedLinearTurnSession(initial, initialEconomy);
            cold.InitializeNewGameInputConfiguration(inputConfiguration);
            if (contacts)
            {
                cold.RequestField(new FieldTransition(100, -2, 0, 0, 0));
            }
            else
            {
                cold.RequestField(100, 0);
            }
            GameSession session = new(cold.State with { WorldControls = controls });
            EventEngine engine = new(session) { AnimationFrameCount = animationAssets.GetAnimationFrameCount };
            if (contacts)
            {
                // The component contact fixture is a single flat triangle, independent of installed field travel.
                engine.ResolveNativeTriangleHeight = (_, _, _) => 0;
                engine.ResolvePartyTraceTriangle = (_, _, _) => 0;
            }
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
            engine.LoadField(100, program, [], session.State.FieldArrival, nativeRuntimeProfile: assets.NativeRuntimeProfile, fieldLoadReason: session.State.FieldLoadContext!.Reason);
            engine.Step();
            return (engine, session);
        }
        byte[] jump = SyntheticObjects([1],
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.PSHN_L, -65536), (Jsm.Opcode.PSHN_L, 0x12345), (Jsm.Opcode.WORLDMAPJUMP, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)]
        ]);
        const int jumpOperation = 6;
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
            NativeWorldControlState controls = known.WithPartyPower(disabledSource ? 0 : 1) with { Menu209 = disabledSource ? (byte)0xA7 : known.Menu209 };
            (EventEngine engine, GameSession session) = Create(jump, controls);
            NativeWorldControlState departureControls = controls.WithNormalFieldLoad();
            Require(session.WorldControls == departureControls, "The authentic field allocation did not apply its normal field-load controls before the request.");
            int prepares = 0;
            Func<NativeWorldTransitionRequest, Action?> original = engine.PrepareWorldTransition!;
            engine.PrepareWorldTransition = request =>
            {
                prepares++;
                Require(request.Origin == NativeWorldTransitionOrigin.Script && request.ControlsAtDeparture == departureControls && request.ControlsAtIssue == departureControls.WithWorldEntryMenuFlag() && request.ControlsAtLoader == request.ControlsAtIssue && request.Transition == new WorldTransition(0, 0, 0x2345), "Scripted world preparation changed low-WORD packet or issue/loader flag stages.");
                Require(request.SourceObjectId == 0 && request.SourceScriptId == 1 && request.SourceOperation == jumpOperation && request.SourceFrame == engine.Frame, "Scripted world preparation lost its actual default fiber and issuing operation provenance.");
                return original(request);
            };
            engine.Step();
            NativeWorldJumpWait wait = engine.Scene.WorldJumpWaits.Values.Single();
            Require(prepares == 1 && !wait.Suppressed && wait.Operation == jumpOperation && wait.Operands.SequenceEqual(new[] { 65536, -65536, 0x12345 }) && session.State.WorldArrival == new WorldTransition(0, 0, 0x2345) && session.WorldControls == departureControls.WithWorldEntryMenuFlag() && engine.NextTrigger.Kind == PlayerTriggerKind.WorldMap && !session.State.Variables.ContainsKey(512), "Enabled world jump did not commit once and hold its original issuing PC.");
            JsonElement saved = engine.Capture();
            (EventEngine restored, _) = Create(jump, known);
            restored.Restore(saved);
            restored.Step();
            Require(JsonElement.DeepEquals(saved, restored.Capture()), "Issued world packet/flags/PC wait changed after independent VM restore.");
            JsonNode corruptedSource = JsonNode.Parse(saved.GetRawText())!;
            corruptedSource["Scene"]!["WorldTransitionRequest"]!["SourceOperation"] = jumpOperation + 1;
            try
            {
                restored.Restore(JsonSerializer.SerializeToElement(corruptedSource));
                throw new InvalidOperationException("Corrupted world-request source operation passed restore validation.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(saved, restored.Capture()), "Invalid world-request source provenance partially mutated the VM.");
        }

        foreach (int failure in new[] { 0, 1, 2 })
        {
            (EventEngine engine, GameSession session) = Create(jump, known);
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
            JsonElement fiber = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1 && !item.GetProperty("Complete").GetBoolean());
            // The native callback advances its process owners before the instruction's atomic preflight.
            Require(commits == 0 && session.State.Scene == before.Scene && session.State.FieldId == before.FieldId && session.State.WorldArrival == before.WorldArrival && session.State.World == before.World && session.State.SavedWorld == before.SavedWorld && session.WorldControls == before.WorldControls && engine.Scene.WorldTransitionRequest is null && engine.Scene.WorldJumpWaits.Count == 0 && engine.LastFault is { Opcode: "WORLDMAPJUMP" } fault && fault.Operation == jumpOperation && fiber.GetProperty("Position").GetInt32() == jumpOperation && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 0x12345, -65536, 65536 }), "Failed world preparation/commit changed packet, controls, issuing PC or three operands.");
        }
        (EventEngine unknownControls, GameSession unknownSession) = Create(jump);
        JsonElement unknownBefore = unknownControls.Capture();
        GameState unknownState = unknownSession.State;
        try
        {
            unknownControls.Step();
            throw new InvalidOperationException("A verified native callback ran without its canonical control owner.");
        }
        catch (NotSupportedException)
        {
        }
        Require(ReferenceEquals(unknownState, unknownSession.State) && JsonElement.DeepEquals(unknownBefore, unknownControls.Capture()), "Missing canonical callback controls partially mutated the field or packet.");
        (EventEngine aliases, GameSession aliasSession) = Create(jump, known);
        ExpectEventMemoryPublicationFault(aliasSession, aliasSession.State.EventMemory.Write(104, 4, NativeScalarValue.Exact(0x200)));

        byte[] sticky = SyntheticObjects([1, (16 << 7) | 1],
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.MAPJUMPOFF, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WORLDMAPJUMP, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.MAPJUMPON, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]
        ]);
        (EventEngine suppressed, GameSession suppressedSession) = Create(sticky, known);
        int suppressedPrepares = 0;
        suppressed.PrepareWorldTransition = _ =>
        {
            suppressedPrepares++;
            return () => { };
        };
        suppressed.Step();
        JsonElement blocked = suppressed.Capture();
        Require(suppressed.Scene.MapJumpDisabled && suppressed.Scene.WorldJumpWaits.Values.Single().Suppressed && suppressed.Scene.WorldTransitionRequest is null && suppressedSession.WorldControls == known.WithNormalFieldLoad(), "Disabled world jump wrote packet/flags or lost its first-entry wait.");
        (EventEngine resumed, _) = Create(sticky, known);
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
        invalid["Scene"]!["WorldJumpWaits"]![key]!["Operation"] = suppressed.Scene.WorldJumpWaits.Values.Single().Operation + 1;
        try
        {
            suppressed.Restore(JsonSerializer.SerializeToElement(invalid));
            throw new InvalidOperationException("Corrupted world-jump issuing PC passed restore validation.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(valid, suppressed.Capture()), "Invalid world wait restore partially mutated the VM.");

        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.FADENONE, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        byte[] gateProgram = SyntheticObjects([(16 << 7) | 2, 0], [player, [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)]]);
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
            (EventEngine gate, GameSession session) = Create(gateProgram, known, contacts: true);
            gate.Step();
            Require(gate.PlayerActor is { ObjectId: 0, RenderModelId: 0 }, "The gateway fixture lost its actually allocated zero-model player body.");
            Require(session.FieldFade?.Physical.Mode == 0, "The gateway fixture did not clear its fresh allocation's native black fade through FADENONE.");
            NativeWorldControlState gateControls = known.WithNormalFieldLoad();
            Require(session.WorldControls == gateControls, "Gateway preparation did not retain its authentic field-load control stage.");
            gate.InfGateways = [gateway];
            gate.InfAreaTriggers = [trigger];
            int prepares = 0;
            JsonElement? refusedBefore = null;
            Func<NativeWorldTransitionRequest, Action?> original = gate.PrepareWorldTransition!;
            gate.PrepareWorldTransition = request =>
            {
                prepares++;
                Require(request.Origin == NativeWorldTransitionOrigin.Gateway && request.ControlsAtIssue == gateControls && request.ControlsAtLoader == gateControls.WithWorldEntryMenuFlag() && request.Gateway?.NativeIndex == 2 && request.Gateway.NativeRecord!.SequenceEqual(raw), "INF world preparation changed raw gateway index/record or issue-stage flags.");
                Require(request.SourceObjectId is null && request.SourceScriptId is null && request.SourceOperation is null, "An INF gateway acquired synthetic script-source provenance.");
                refusedBefore = gate.Capture();
                return refuse ? null : original(request);
            };
            string playerBefore = JsonSerializer.Serialize(new { gate.PlayerActor!.X, gate.PlayerActor.Y, gate.PlayerActor.PushRadius, gate.Scene.MapJumpDisabled });
            FieldTeleportContactResult transfer = default;
            try
            {
                transfer = gate.TeleportPlayerAndDispatchContacts(new(1, 0, 0, 0, 64));
                Require(!refuse, "Rejected gateway preparation committed movement.");
            }
            catch (NotSupportedException) when (refuse)
            {
            }
            if (refuse)
            {
                Require(refusedBefore is JsonElement before && JsonElement.DeepEquals(before, gate.Capture()) && session.State.WorldArrival is null && session.WorldControls == gateControls, "World gateway refusal mutated field contacts, XYZ or flags.");
            }
            else
            {
                Require(prepares == 1 && session.State.WorldArrival == new WorldTransition(0, 0, 0) && gate.Actors[1].DoorLineEntryPending && gate.Scene.WorldTransitionRequest?.Gateway?.NativeRecord is not null && gate.NextTrigger.Kind == PlayerTriggerKind.WorldMap, "World gateway failed to commit original packet and deferred area contact: " + JsonSerializer.Serialize(new { prepares, transfer, playerBefore, gate.PlayerActor!.X, gate.PlayerActor.Y, gate.Scene.MapJumpDisabled, session.State.WorldArrival, Player = gate.PlayerActor.ObjectId, gate.IsUserControlEnabled, AreaPending = gate.Actors[1].DoorLineEntryPending, gate.NextTrigger }));
                (EventEngine restored, _) = Create(gateProgram, known, contacts: true);
                restored.InfGateways = [gateway];
                restored.Restore(gate.Capture());
                Require(JsonElement.DeepEquals(gate.Capture(), restored.Capture()), "INF request/full original record did not survive independent checkpoint restore.");
            }
        }
        byte[] negativeMode = raw.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(negativeMode.AsSpan(16), -1);
        (EventEngine negativeGate, _) = Create(gateProgram, known, contacts: true);
        negativeGate.Step();
        negativeGate.InfGateways = [gateway with { Destination = gateway.Destination with { Triangle = -1 }, NativeRecord = negativeMode }];
        int negativeModePrepares = 0;
        JsonElement? negativeBefore = null;
        negativeGate.PrepareWorldTransition = request =>
        {
            negativeModePrepares++;
            Require(request.Transition.Mode == -1, "Negative raw world mode was unsigned or treated as a field sentinel.");
            negativeBefore = negativeGate.Capture();
            return null;
        };
        try
        {
            negativeGate.TeleportPlayerAndDispatchContacts(new(1, 0, 0, 0, 64));
            throw new InvalidOperationException("Negative world mode skipped required backend preflight refusal.");
        }
        catch (NotSupportedException)
        {
        }
        Require(negativeModePrepares == 1 && negativeBefore is JsonElement negativeSnapshot && JsonElement.DeepEquals(negativeSnapshot, negativeGate.Capture()), "Negative world mode failed to preserve original state after reaching preparation.");
        (Jsm.Opcode, int)[] disabledPlayer = player.Take(player.Length - 1).Concat(new[] { (Jsm.Opcode.MAPJUMPOFF, 0), (Jsm.Opcode.RET, 0) }).ToArray();
        byte[] disabledGateProgram = SyntheticObjects([(16 << 7) | 2, 0], [disabledPlayer, [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 0)]]);
        disabledGateProgram[0] = 1;
        disabledGateProgram[3] = 1;
        (EventEngine disabledGate, GameSession disabledGateSession) = Create(disabledGateProgram, known, contacts: true);
        disabledGate.Step();
        disabledGate.InfGateways = [gateway with { NativeRecord = null, NativeIndex = -1 }, gateway with { Destination = new FieldTransition(143, 0, 0, 0, 0) }];
        disabledGate.InfAreaTriggers = [trigger];
        FieldTransition? originalArrival = disabledGateSession.State.FieldArrival;
        FieldTeleportContactResult disabledTransfer = disabledGate.TeleportPlayerAndDispatchContacts(new(1, 0, 0, 0, 64));
        Require(disabledTransfer is { PlacementApplied: true, ContactTriggered: true, NativeInterrupted: false } && disabledGate.Scene.MapJumpDisabled && disabledGateSession.State.WorldArrival is null && disabledGateSession.State.FieldArrival == originalArrival && disabledGate.Actors[1].DoorLineEntryPending, "MAPJUMPOFF failed to suppress the entire world/field gateway scan while preserving the incoming packet and areas.");
        Console.WriteLine("Native world transitions: enabled low-WORD script packet with atomic preflight and two flag stages; disabled sticky issuing-PC wait survives later enable/restore; full indexed INF record, gateway refusal and disabled scan preserve area/field transaction boundaries.");
    }
}
