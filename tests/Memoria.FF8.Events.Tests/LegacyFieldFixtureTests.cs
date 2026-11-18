using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeAreaRequestAndGuard()
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.REQ, 3), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] requested = [(Jsm.Opcode.LBL, 5), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 8)];
        byte[] program = SyntheticObjects([2 << 7, (3 << 7) | 2, 6 << 7, 1], [init, caller, init, init, init, requested, init]);
        program[0] = 1;
        program[1] = 1;
        program[2] = 1;
        program[3] = 1;
        JsmProgram parsed = JsmProgram.Read(program);
        Require(parsed.NativeObjects.SequenceEqual(new[] { 0, 1, 3, 2 }) && parsed.Objects.Single(obj => obj.IsArea).Label == 3, "Native objects/doors/modules/areas permutation disagrees with the original doors-before-areas raw group consumption.");
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
        engine.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        var pending = engine.Capture();
        engine.Restore(pending);
        engine.Step();
        Require(session.State.Variables[512] == 7 && engine.LastFault is null, "REQ did not resolve its global script label on the native area object.");
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.REQ, Jsm.Opcode.REQSW, Jsm.Opcode.REQEW })
        {
            engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 99), (opcode, 0), (Jsm.Opcode.RET, 0)), []);
            try
            {
                engine.Step();
                throw new InvalidOperationException("Invalid REQ target script consumed its operands before failing.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == opcode.ToString())
            {
            }
            var frozen = engine.Capture();
            var fiber = frozen.GetProperty("Fibers")[0];
            Require(fiber.GetProperty("Position").GetInt32() == 3 && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 99, 3 }), "Invalid REQ target script did not preserve its original PC/operands.");
            ExpectInterruptedRestoreRefusal(engine, frozen);
            try
            {
                engine.Step();
                throw new InvalidOperationException("Invalid REQ target script resumed after restore.");
            }
            catch (EventScriptException) when (engine.LastFault?.Opcode == opcode.ToString())
            {
            }
            Require(frozen.GetRawText() == engine.Capture().GetRawText(), "Invalid REQ target script did not remain exactly frozen.");
        }
    }


    private static void RefreshPartyBindingsAndGuard()
    {
        (Jsm.Opcode, int)[] first = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.USE, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] door = [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] duplicate = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] callerInit = [(Jsm.Opcode.LBL, 48), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 49), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] program = SyntheticObjects([8 << 7, 16 << 7, 0, 32 << 7, (48 << 7) | 1], [first, door, duplicate, unused, callerInit, caller]);
        program[1] = 1;
        program[3] = 4;
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
        engine.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        JsonElement actors = default;
        bool inspectedRefresh = false;
        engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.NOP))
            {
                actors = engine.Capture().GetProperty("Actors");
            }
            if (command.Name == nameof(Jsm.Opcode.REFRESHPARTY))
            {
                inspectedRefresh = true;
                JsonElement current = engine.Capture();
                JsonElement currentCaller = current.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 49);
                Require(JsonElement.DeepEquals(actors, current.GetProperty("Actors")) && currentCaller.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([123]), "REFRESHPARTY changed physical actor operands or consumed its unrelated logical DWORD.");
            }
        };
        engine.Step();
        Require(engine.Scene.Party.SequenceEqual(new[] { 5, 255, 255 }) && engine.Scene.NativeActorObjectIds.SequenceEqual(new[] { 0, 3, 4 }) && engine.Scene.FieldPartyNativeActorIds.SequenceEqual(new[] { 0, 255, 255 }) && engine.Variables[194] == 0 && engine.Variables[195] == 255 && engine.Variables[196] == 255, "REFRESHPARTY used raw/door identity, retained an init-disabled duplicate, or matched defaultFE to emptyFF.");
        Require(inspectedRefresh && session.State.Variables[512] == 123, "REFRESHPARTY consumed operands or changed actor flags, movement or geometry.");
        var snapshot = engine.Capture();
        engine.Restore(snapshot);
        Require(snapshot.GetRawText() == engine.Capture().GetRawText(), "REFRESHPARTY's typed and script-visible binding bytes failed exact restore.");
        foreach ((int address, int value) in new[] { (104, 0x800), (105, 8) })
        {
            GameSession guarded = new(new GameState());
            guarded.SetEventMemory(guarded.State.EventMemory.Write(address, 4, NativeScalarValue.Exact(value)));
            EventEngine blocked = new(guarded);
            blocked.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)), []);
            try
            {
                blocked.Step();
                throw new InvalidOperationException("REFRESHPARTY silently entered its unproved auxiliary-party branch.");
            }
            catch (EventScriptException) when (blocked.LastFault?.Opcode == nameof(Jsm.Opcode.REFRESHPARTY))
            {
            }
            var frozen = blocked.Capture();
            Require(frozen.GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 2 && frozen.GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 123 && blocked.Scene.FieldPartyNativeActorIds.Count == 0, "Unsupported REFRESHPARTY advanced PC, consumed stack or rebuilt its bindings.");
            ExpectInterruptedRestoreRefusal(blocked, frozen);
            try
            {
                blocked.Step();
                throw new InvalidOperationException("Unsupported auxiliary-party branch continued after restore.");
            }
            catch (EventScriptException)
            {
            }
            Require(frozen.GetRawText() == blocked.Capture().GetRawText(), "Unsupported auxiliary-party refresh failed to remain exactly frozen.");
        }
    }


    private static void NativeActorCompactionAndNullRequests()
    {
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.REQ, Jsm.Opcode.REQSW, Jsm.Opcode.REQEW })
        {
            (Jsm.Opcode, int)[] removed = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] callerInit = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 999), (opcode, 0), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] program = SyntheticObjects([16 << 7, 1], [callerInit, caller, removed]);
            GameSession session = CreateInitializedSession();
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
            engine.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            Require(engine.Scene.NativeActorLayoutVersion == 1 && engine.Scene.NativeActorObjectIds.SequenceEqual(new[] { 0 }) && engine.Actors.All(actor => actor.CharacterId == -2 && actor.PushRadius == 48 && actor.TalkRadius == 128), "Native compaction lost its raw null slot or native FE/push48/talk128 defaults.");
            var initialized = engine.Capture();
            engine.Step();
            JsonElement completedCaller = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean());
            Require(engine.LastFault is null && session.State.Variables[512] == 123 && completedCaller.GetProperty("ScriptId").GetInt32() == 1 && completedCaller.GetProperty("Stack").GetArrayLength() == 0 && engine.Capture().GetProperty("Fibers").GetArrayLength() == initialized.GetProperty("Fibers").GetArrayLength(), "Null native request did not consume exactly two operands and return without allocating another request.");
            string completed = engine.Capture().GetRawText();
            engine.Restore(initialized);
            engine.Step();
            Require(completed == engine.Capture().GetRawText(), "Raw null request changed after retained-layout restoration.");
        }
        List<(Jsm.Opcode, int)> delayed = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.UNUSE, 0)];
        delayed.AddRange(Enumerable.Repeat((Jsm.Opcode.NOP, 0), 20));
        delayed.Add((Jsm.Opcode.USE, 0));
        delayed.Add((Jsm.Opcode.RET, 0));
        GameSession delayedSession = CreateInitializedSession();
        EventEngine delayedEngine = new(delayedSession) { AnimationFrameCount = (_, _) => 1 };
        delayedEngine.LoadField(100, SyntheticProgram(delayed.ToArray()), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        int executedNops = 0;
        bool useBeforeCompaction = false;
        delayedEngine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.NOP))
            {
                executedNops++;
            }
            if (command.Name == nameof(Jsm.Opcode.USE))
            {
                useBeforeCompaction = delayedEngine.Scene.NativeActorLayoutVersion == 0;
            }
        };
        delayedEngine.Step();
        Require(executedNops == 20 && useBeforeCompaction && delayedEngine.Actors[0].Active && delayedEngine.Scene.NativeActorLayoutVersion == 1 && delayedEngine.Frame == 1, "Synchronous initialization split its instruction budget or compacted before the later original USE/RET.");
        var completedInit = delayedEngine.Capture();
        delayedEngine.Restore(completedInit);
        delayedEngine.Step();
        Require(delayedEngine.Actors[0].Active && delayedEngine.Scene.NativeActorObjectIds.SequenceEqual(new[] { 0 }) && delayedEngine.Scene.NativeActorLayoutVersion == 1, "Completed synchronous initialization restore lost the original USE or retained compaction.");
    }


    private static void InfMovementAndContactPersistence()
    {
        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] areaInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] areaDefault = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] areaEntry = [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([(16 << 7) | 2, 0], [player, areaInit, areaDefault, areaEntry]);
        program[0] = 1;
        program[3] = 1;
        GameSession session = CreateInitializedSession();
        EventEngine Create()
        {
            session = CreateInitializedSession();
            EventEngine result = new(session) { AnimationFrameCount = (_, _) => 1 };
            result.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return result;
        }
        InfAreaTrigger trigger = new(new System.Numerics.Vector3(0, -20, 0), new System.Numerics.Vector3(0, 20, 0), 0, 0);
        EventEngine engine = Create();
        engine.Step();
        engine.InfAreaTriggers = [trigger];
        CommitComponentPlayer(engine, -1, 0, 0, 0);
        Require(engine.Actors[1].DoorLineContact && engine.Actors[1].DoorLineEntryPending && !session.State.Variables.ContainsKey(512), "INF entry failed to use current proximity/facing and defer its original script.");
        var pending = engine.Capture();
        EventEngine restored = Create();
        restored.InfAreaTriggers = [trigger];
        restored.Restore(pending);
        Require(pending.GetRawText() == restored.Capture().GetRawText(), "INF per-area contact/pending entry did not survive exact restore.");
        engine.Step();
        restored.Step();
        Require(session.State.Variables[512] == 7 && !engine.Actors[1].DoorLineEntryPending && engine.Capture().GetRawText() == restored.Capture().GetRawText(), "INF entry failed to execute original local2 at native priority6 after restore.");
        long sequence = restored.Capture().GetProperty("Sequence").GetInt64();
        CommitComponentPlayer(restored, 0, 0, 0, 0);
        restored.Step();
        Require(restored.Capture().GetProperty("Sequence").GetInt64() == sequence, "INF side0 contact incorrectly rearmed while already contacted.");

        foreach (byte side in new byte[] { 0, 1, 2, 3, 4, 5 })
        {
            engine = Create();
            engine.Step();
            engine.InfAreaTriggers = [trigger];
            CommitComponentPlayer(engine, -1, 0, 0, 0);
            engine.Step();
            engine.InfAreaTriggers = [trigger with { Side = side }];
            CommitComponentPlayer(engine, 0, 0, 0, 0);
            Require(engine.Actors[1].DoorLineContact == (side % 2 == 0), $"INF near mode{side} failed native even-entry/odd-clear contact semantics.");
        }
        foreach (byte side in new byte[] { 0, 1, 2, 3, 4, 5 })
        {
            engine = Create();
            engine.Step();
            engine.InfAreaTriggers = [trigger with { Start = new System.Numerics.Vector3(-200, -20, 0), End = new System.Numerics.Vector3(-200, 20, 0), Side = side }];
            CommitComponentPlayer(engine, -1, 0, 0, 0);
            Require(engine.Actors[1].DoorLineContact == (side is 3 or 5), $"INF far mode{side} failed native inverse/contact-side behavior.");
        }
        engine = Create();
        engine.Step();
        engine.InfAreaTriggers = [trigger with { Start = new System.Numerics.Vector3(200, -20, 0), End = new System.Numerics.Vector3(200, 20, 0), Side = 5 }];
        CommitComponentPlayer(engine, -1, 0, 0, 0);
        Require(!engine.Actors[1].DoorLineContact, "INF far mode5 ignored its signed-side condition.");

        FieldTransition packet = new(143, -2466, -5997, 37, 185);
        InfGateway gateway = new(new System.Numerics.Vector3(0, -20, 0), new System.Numerics.Vector3(0, 20, 0), packet);
        engine = Create();
        engine.Step();
        engine.InfGateways = [gateway];
        CommitComponentPlayer(engine, 1, 0, 0, 0);
        Require(session.State.FieldArrival == packet && engine.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "INF gateway failed to preserve its native field/XY/triangle/direction packet.");
        long departureFrame = engine.Frame;
        engine.Step();
        Require(engine.Frame == departureFrame, "INF gateway executed the departing VM after its packet request.");

        engine = Create();
        engine.Step();
        engine.InfGateways = [gateway];
        CommitComponentPlayer(engine, 1, 0, 200, 0);
        Require(engine.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "INF gateway used proposed walkmesh Z instead of native current actor Z.");

        engine = Create();
        engine.Step();
        engine.InfGateways = [gateway with { Start = new System.Numerics.Vector3(0, -20, 100), End = new System.Numerics.Vector3(0, 20, 100) }];
        CommitComponentPlayer(engine, 1, 0, 0, 0);
        Require(engine.NextTrigger.Kind != PlayerTriggerKind.FieldTransition, "INF strict 3D distance accepted a gateway exactly one push radius away.");
        engine = Create();
        engine.Step();
        engine.InfGateways = [gateway with { Start = new System.Numerics.Vector3(0, 10, 0), End = new System.Numerics.Vector3(0, 20, 0) }];
        CommitComponentPlayer(engine, 1, 0, 0, 0);
        Require(engine.NextTrigger.Kind != PlayerTriggerKind.FieldTransition, "INF line distance clamped an out-of-segment projection into a crossing.");

        foreach (int fieldId in new[] { 0, 71 })
        {
            engine = Create();
            engine.Step();
            engine.InfGateways = [gateway with { Destination = packet with { FieldId = fieldId } }];
            var before = engine.Capture();
            FieldTransition? priorPacket = session.State.FieldArrival;
            try
            {
                CommitComponentPlayer(engine, 1, 0, 0, 0);
                throw new InvalidOperationException("INF world gateway used the field arrival service.");
            }
            catch (NotSupportedException)
            {
            }
            Require(before.GetRawText() == engine.Capture().GetRawText() && session.State.FieldArrival == priorPacket, "Unsupported world INF gateway changed movement, direction, contact or packet before rejecting.");
        }
        Console.WriteLine("INF routing: finite quantized3D gateway crossing/currentZ/typed packet and safe world rejection; all six native modes, original local2/priority6, exact pending/contact restore.");
    }


    private static void DoorLineGateAndGuard()
    {
        byte[] area = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.DOORLINEOFF, 0), (Jsm.Opcode.RET, 0));
        area[0] = 1;
        area[3] = 0;
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
        engine.LoadField(100, area, []);
        Require(engine.Actors[0].DoorLineEnabled, "Native area gate failed to initialize enabled before its script.");
        engine.Step();
        var disabled = engine.Capture();
        Require(!engine.Actors[0].DoorLineEnabled && engine.Actors[0].LineEnabled && !engine.Actors[0].LineContact && engine.IsUserControlEnabled, "DOORLINEOFF changed the independent SETLINE contact/gate or player control.");
        engine.Restore(disabled);
        Require(disabled.GetRawText() == engine.Capture().GetRawText(), "Disabled area gate did not survive exact snapshot restoration.");
        engine.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.DOORLINEOFF, 0), (Jsm.Opcode.RET, 0)), []);
        try
        {
            engine.Step();
            throw new InvalidOperationException("DOORLINEOFF accepted an unproved non-area entity layout.");
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.DOORLINEOFF))
        {
        }
        var frozen = engine.Capture();
        Require(frozen.GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 1 && engine.Actors[0].DoorLineEnabled, "Unproved DOORLINEOFF advanced its PC or changed its gate.");
        ExpectInterruptedRestoreRefusal(engine, frozen);
        try
        {
            engine.Step();
            throw new InvalidOperationException("Unproved DOORLINEOFF resumed after restore.");
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.DOORLINEOFF))
        {
        }
        Require(frozen.GetRawText() == engine.Capture().GetRawText(), "Unproved non-area DOORLINEOFF failed to remain frozen.");
    }


    private static void BackgroundModuleInitialSelection(string gameDirectory)
    {
        (Jsm.Opcode, int)[] idle = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] moduleInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] selection = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BGDRAW, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.BGOFF, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] program = SyntheticObjects([(16 << 7) | 1, 0], [idle, moduleInit, selection]);
        program[2] = 2;
        program[3] = 0;
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
        engine.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(engine.BackgroundStates[0] == -1 && engine.BackgroundStates[1] == -1, "Background modules did not start disabled in native raw-group order.");
        engine.Step();
        engine.Step();
        Require(engine.BackgroundStates[0] == -1 && engine.BackgroundStates[1] == -1, "An unselected module appeared during its preceding wait.");
        engine.Step();
        engine.Step();
        Require(engine.BackgroundStates[0] == 0 && engine.BackgroundStates[1] == -1, "BGDRAW0 did not select only its actual source module.");
        var selected = engine.Capture();
        EventEngine resumed = new(CreateInitializedSession());
        resumed.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        resumed.Restore(selected);
        engine.Step();
        resumed.Step();
        Require(engine.BackgroundStates[0] == 0 && engine.Capture().GetRawText() == resumed.Capture().GetRawText(), "Selected background frame changed across a saved wait.");
        engine.Step();
        resumed.Step();
        Require(engine.BackgroundStates[0] == -1 && engine.BackgroundStates[1] == -1 && engine.Capture().GetRawText() == resumed.Capture().GetRawText(), "BGOFF did not disable the previously selected module after restore.");

        using GameAssets assets = new(gameDirectory);
        FieldAssets classroom = assets.LoadField(232);
        EventObject deskModule = JsmProgram.Read(classroom.Jsm).Objects.Single(obj => obj.BackgroundModuleIndex == 6);
        Require(deskModule.Index == 35 && deskModule.Label == 328 && deskModule.Scripts[0].Start == 7480 && deskModule.Scripts[0].End == 7482, "Actual desk-sprite module no longer matches its original no-selection init script.");
        BackgroundTile[] deskTiles = classroom.Background.Where(tile => tile.AnimationId == 6).ToArray();
        Require(deskTiles.Length == 11 && deskTiles.All(tile => tile.AnimationState == 0) && deskTiles.Select(tile => tile.Z).Distinct().Order().SequenceEqual([391, 559]), "Actual two desk sprites lost their module6/frame0 MAP selection or depth groups.");
        GameState historical = GameSession.Load(TestResources.FixturePath("Events.Tests.Program.12"));
        GameSession historicalSession = new(historical);
        EventEngine restoredHistorical = new(historicalSession);
        restoredHistorical.LoadField(232, classroom.Jsm, classroom.Messages);
        Require(restoredHistorical.BackgroundStates[6] == -1, "Actual classroom LoadField failed to initialize never-selected module6 as disabled.");
        restoredHistorical.Restore(historical.EventState!.Value);
        Require(!restoredHistorical.BackgroundStates.ContainsKey(6) && restoredHistorical.Frame == 1724, "Restore migrated the historical missing-module state or changed its exact preceding frame.");
        Console.WriteLine("Background selection: initialized off -> BGDRAW0 -> saved wait -> BGOFF; actual classroom module6 has11 frame0 tiles in two desk-depth groups; historical missing state retained.");
    }


    private static void BackgroundLoopResume()
    {
        (Jsm.Opcode, int)[] idle = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] shade = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 25), (Jsm.Opcode.PSHN_L, 22), (Jsm.Opcode.PSHN_L, 126), (Jsm.Opcode.PSHN_L, 126), (Jsm.Opcode.PSHN_L, 126), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.RBGSHADELOOP, 0), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([16 << 7, 0], [idle, shade]);
        program[2] = 1;
        program[3] = 1;
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
        engine.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        Require(engine.GetBackgroundModuleShades()[0] == new System.Numerics.Vector3(126), "Shade module did not begin at its actual RGB endpoint.");
        for (int index = 0; index < 12; index++)
        {
            engine.Step();
        }
        var snapshot = engine.Capture();
        EventEngine restored = new(CreateInitializedSession()) { AnimationFrameCount = (_, _) => 1 };
        restored.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        restored.Restore(snapshot);
        for (int index = 12; index < 49; index++)
        {
            engine.Step();
            restored.Step();
            Require(engine.GetBackgroundModuleShades()[0] == restored.GetBackgroundModuleShades()[0], "Restoring a running module shade loop changed an observed frame color.");
            if (index == 24)
            {
                Require(engine.GetBackgroundModuleShades()[0] == System.Numerics.Vector3.Zero, "Shade loop did not finish its first25-frame fade at black.");
            }
        }
        Require(engine.GetBackgroundModuleShades()[0] == new System.Numerics.Vector3(126) && engine.Capture().GetRawText() == restored.Capture().GetRawText(), "Loop second endpoint/holds or exact phase snapshot diverged.");
    }


    private static void ArrivalInitializationOrder()
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -99), (Jsm.Opcode.PSHN_L, -99), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.AMESW, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([1], [init, main]);
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { ResolveFieldArrival = arrival => new WalkmeshPosition(new System.Numerics.Vector3(arrival.X, arrival.Y, -7), arrival.Triangle) };
        engine.LoadField(100, program, ["Arrival"], new FieldTransition(100, 10, 20, 50, 32));
        var pending = engine.Capture();
        engine.Restore(pending);
        Require(pending.GetRawText() == engine.Capture().GetRawText(), "Pending coordinate arrival was lost on restore.");
        engine.Step();
        Require(engine.Actors[0].X == 10 && engine.Actors[0].Y == 20 && engine.Actors[0].Z == -7 && engine.Actors[0].WalkmeshTriangle == 50 && engine.Actors[0].Direction == 32, "Arrival failed to override initialization SET3 before default scripts.");
        engine.Step();
        Require(engine.IsDialogueOpen, "Default script failed to start after coordinate arrival.");
        engine.ConfirmDialogue();
        engine.Step();
        Require(engine.Actors[0].X == 4 && engine.Actors[0].Y == 5 && engine.Actors[0].Z == 6, "Arrival incorrectly overrode later original SET3.");
    }


    private static void LineContactAndResume()
    {
        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 0)];
        List<(Jsm.Opcode, int)[]> scripts = [player, [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 0)]];
        for (int label = 17; label < 23; label++)
        {
            scripts.Add([(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 0)]);
        }
        scripts.Add([(Jsm.Opcode.LBL, 23), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.AMESW, 0), (Jsm.Opcode.RET, 0)]);
        byte[] program = SyntheticObjects([0, (16 << 7) | 7], scripts);
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 1 };
        engine.ResolveWalkmesh = (x, y) => new WalkmeshPosition(new System.Numerics.Vector3(x, y, 0), 0);
        engine.LoadField(100, program, ["Crossed"]);
        engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 10));
        engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Frames: 2));
        Require(engine.Actors[0].Direction == 64, "Native positive-X movement facing must be64.");
        Require(engine.LineCrossings.Count == 1 && engine.CurrentDialogue?.Text == "Crossed" && engine.Actors[1].LineContact, "Forward entry did not dispatch the original crossing script.");
        System.Text.Json.JsonElement snapshot = engine.Capture();
        EventEngine restored = new(session);
        restored.ResolveWalkmesh = engine.ResolveWalkmesh;
        restored.LoadField(100, program, ["Crossed"]);
        restored.Restore(snapshot);
        Require(snapshot.GetRawText() == restored.Capture().GetRawText(), "Saving at a line contact lost the pending original script/contact state.");
        restored.Apply(new PlayerAction(PlayerActionKind.Confirm));
        restored.Apply(new PlayerAction(PlayerActionKind.Move));
        restored.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        Require(restored.LineCrossings.Count == 0, "Stopping on or leaving a contacted line retriggered its script.");
        restored.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1));
        Require(restored.LineCrossings.Count == 1 && restored.IsDialogueOpen, "Reverse entry after leaving did not rearm the enabled line.");
    }

}
