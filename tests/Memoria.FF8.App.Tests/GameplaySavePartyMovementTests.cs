using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySavePartyMovementTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Consumed_party_targets_and_held_instruction_resume_without_reissuing_or_replaying_rewards(bool split, bool detachFollowers)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        (int X, int Y, int Triangle) origin;
        (int X, int Y, int Triangle) destination;
        using (GameAssets original = new(directory))
        {
            FieldAssets field = original.LoadField(165);
            fieldName = field.Name;
            origin = (-700, -8593, FieldPartyTraceGeometry.Resolve(field.Walkmesh, -700, -8593, 567)!.Value);
            destination = FindDestination(field, origin);
        }
        byte[] program = Program(split, detachFollowers, origin, destination);
        byte[] originalProgram = program.ToArray();
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, program));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        string owner = split ? "PartySplit" : "PartyJoin";
        for (int tick = 0; tick < 12 && source.Events.Capture().GetProperty(owner).ValueKind == JsonValueKind.Null; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Equal(JsonValueKind.Object, source.Events.Capture().GetProperty(owner).ValueKind);
        Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        JsonElement nativeContinuation = source.Events.Capture();
        source.Events.Restore(nativeContinuation);
        Assert.True(JsonElement.DeepEquals(nativeContinuation, source.Events.Capture()));
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySavePartyMovement movement = Assert.IsType<GameplaySavePartyMovement>(continuation.PartyMovement);
        Assert.Equal(split ? GameplaySavePartyMovementKind.Split : GameplaySavePartyMovementKind.Join, movement.Kind);
        Assert.Equal(split ? new[] { 0, 1, 2 } : new[] { 1, 2 }, movement.Targets.Select(target => target.ObjectId));
        GameplaySaveScriptFiber caller = Assert.Single(continuation.Scripts.Fibers, fiber => fiber.Sequence == movement.FiberSequence);
        Assert.Equal(split ? GameplaySaveScriptWait.PartySplit : GameplaySaveScriptWait.PartyJoin, caller.Wait);
        Assert.Empty(caller.Stack);
        Assert.Equal(0, source.Events.EventMemory.Read(600, 1).Values[0]);
        if (detachFollowers)
        {
            Assert.Equal(new[] { 0, 255, 255 }, source.Events.Scene.FieldPartyNativeActorIds);
            Assert.Equal(new NativePartyBindingMemory(1, 0, 1, 2), source.Events.Scene.PartyBindingMemory);
            Assert.Equal(new[] { 0, 1, 2 }, Enumerable.Range(610, 3).Select(address => source.Events.EventMemory.Read(address, 1).Values[0]));
        }
        GameState prepared = sourceSession.PrepareSnapshot();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(prepared, initial.Resume with { Field = continuation.GetLeaderPosition() });
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        GameplaySaveComponentDocument decoded = GameplaySaveCodec.Read(encoded);
        string before = JsonSerializer.Serialize(decoded);
        GameplaySaveFieldContinuation valid = decoded.Progression!.FieldContinuation!;
        GameplaySaveFieldContinuation invalidBindings = valid with { PartyMovement = null, Scene = valid.Scene with { SavedPartyBindings = [valid.Actors.RegisteredActors.Length, 255, 255] }, Scripts = valid.Scripts with { Fibers = valid.Scripts.Fibers.Select(fiber => fiber with { Wait = GameplaySaveScriptWait.None }).ToArray() } };
        Assert.Throws<InvalidDataException>(invalidBindings.Validate);
        GameplaySaveComponentDocument missingOwner = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { PartyMovement = null } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(missingOwner, directory, sharedAssets: assets));
        GameplaySaveComponentDocument wrongCaller = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { PartyMovement = valid.PartyMovement! with { CallerObjectId = 2 } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(wrongCaller, directory, sharedAssets: assets));
        GameSession restoredSession = new(GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets));
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        JsonElement reconstructedContinuation = restored.Events.Capture();
        restored.Events.Restore(reconstructedContinuation);
        Assert.True(JsonElement.DeepEquals(reconstructedContinuation, restored.Events.Capture()));
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(originalProgram, program);
        Assert.Equal(prepared.CrtRandom, restoredSession.State.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredSession.State.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredSession.State.ExamTimer!.ClockCalls);
        Assert.Equal(source.Events.Scene.PartyBindingMemory, restored.Events.Scene.PartyBindingMemory);
        Assert.Equal(source.Events.Scene.FieldPartyNativeActorIds, restored.Events.Scene.FieldPartyNativeActorIds);
        AssertLogicalState(source, restored);
        int callbacks = 0;
        while (source.Events.CaptureGameplaySaveFieldContinuation().PartyMovement is not null && callbacks < 240)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            AssertLogicalState(source, restored);
            Assert.Equal(sourceSession.State.CrtRandom, restoredSession.State.CrtRandom);
            Assert.Equal(sourceSession.State.Playtime, restoredSession.State.Playtime);
            Assert.Equal(sourceSession.State.ExamTimer!.ClockCalls, restoredSession.State.ExamTimer!.ClockCalls);
            if (restored.Events.CaptureGameplaySaveFieldContinuation().PartyMovement is not null)
            {
                GameplaySaveScriptFiber held = restored.Events.CaptureGameplaySaveScripts().Fibers.Single(fiber => fiber.Sequence == caller.Sequence);
                Assert.Equal(caller.Position, held.Position);
                Assert.Equal(caller.Wait, held.Wait);
                Assert.Empty(held.Stack);
                Assert.Equal(0, restored.Events.EventMemory.Read(600, 1).Values[0]);
                Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
            }
            callbacks++;
        }
        Assert.True(callbacks is >= 1 and < 240, string.Join("; ", movement.Targets.Select(target => $"Actor {target.ObjectId}: {continuation.Actors.Actors.Single(actor => actor.ObjectId == target.ObjectId).Pose.Position} -> ({restored.Events.Actors[target.ObjectId].X},{restored.Events.Actors[target.ObjectId].Y},{restored.Events.Actors[target.ObjectId].Z}), completion {restored.Events.Actors[target.ObjectId].NativeMovementCompletion542}, destination {target.Destination}")));
        // Completion's return2 runs the immediate successor in the same callback.
        Assert.Equal(73, restored.Events.EventMemory.Read(600, 1).Values[0]);
        Assert.Equal(prepared.Economy!.Gil + 13, restoredSession.State.Economy!.Gil);
        Assert.Null(restored.Events.CaptureGameplaySaveFieldContinuation().PartyMovement);
        Assert.All(movement.Targets, target => Assert.Equal((ushort?)2, restored.Events.Actors[target.ObjectId].NativeMovementCompletion542));
        Assert.Equal(source.Events.Frame, restored.Events.Frame);
        Assert.Equal(sourceSession.State.Economy!.Gil, restoredSession.State.Economy!.Gil);
        Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(prepared.Economy.Gil + 13, restoredSession.State.Economy.Gil);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(originalProgram, program);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Join_takeover_preserves_lower_move_and_request_backup_through_active_and_completed_cold_loads(bool asynchronous)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        (int X, int Y, int Triangle) origin;
        (int X, int Y, int Triangle) destination;
        using (GameAssets original = new(directory))
        {
            FieldAssets field = original.LoadField(165);
            fieldName = field.Name;
            origin = (-700, -8593, FieldPartyTraceGeometry.Resolve(field.Walkmesh, -700, -8593, 567)!.Value);
            destination = FindDestination(field, origin);
        }
        int targetX = (origin.X + destination.X) / 2;
        int targetY = (origin.Y + destination.Y) / 2;
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, TakeoverProgram(asynchronous, origin, destination)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        for (int tick = 0; tick < 12 && source.Events.Actors[1].NativeScriptMovement is not { SupersededByPartyJoin: true }; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        ActorState companion = source.Events.Actors[1];
        Assert.NotNull(source.Events.CaptureGameplaySaveFieldContinuation().PartyMovement);
        Assert.True(companion.NativeScriptMovement is { Active: false, Cancelled: true, Suspended: false, SupersededByPartyJoin: true });
        NativeMovementRequestState originalBackup = Assert.IsType<NativeMovementRequestState>(companion.NativeMovementRequest);
        Assert.True(originalBackup.SavedMovement.Active);
        Assert.False(originalBackup.SavedMovement.SupersededByPartyJoin);
        Assert.Equal(asynchronous, originalBackup.SavedMovement.Asynchronous);
        Assert.Equal((targetX << 12, targetY << 12, (short)1000), (originalBackup.SavedMovement.TargetX, originalBackup.SavedMovement.TargetY, originalBackup.SavedMovement.Speed));
        GameplaySaveScriptFiber lower = source.Events.CaptureGameplaySaveScripts().Fibers.Single(fiber => fiber.Sequence == originalBackup.SavedMovement.FiberSequence);
        Assert.Equal(!asynchronous, lower.MovementIssue is not null);
        Assert.NotEqual(originalBackup.RequestSequence, lower.Sequence);
        AssertExactRoundTrip(source);
        companion = source.Events.Actors[1];
        GameplaySaveComponentDocument activeDocument = EncodeContinuation(source, sourceSession, initial);
        string activeBefore = JsonSerializer.Serialize(activeDocument);
        GameplaySaveFieldContinuation activeContinuation = activeDocument.Progression!.FieldContinuation!;
        GameplaySaveFieldActor[] unrelatedActors = activeContinuation.Actors.Actors.ToArray();
        GameplaySaveActorMovement displacedMovement = unrelatedActors[1].Movement!;
        unrelatedActors[1] = unrelatedActors[1] with { Movement = displacedMovement with { Worker = displacedMovement.Worker! with { SupersededByPartyJoin = false } } };
        GameplaySaveComponentDocument unrelatedWorker = activeDocument with { Progression = activeDocument.Progression with { FieldContinuation = activeContinuation with { Actors = activeContinuation.Actors with { Actors = unrelatedActors } } } };
        JsonElement beforeRejectedComponent = source.Events.Capture();
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(unrelatedWorker, directory, sharedAssets: assets));
        Assert.True(JsonElement.DeepEquals(beforeRejectedComponent, source.Events.Capture()));
        Assert.Equal(activeBefore, JsonSerializer.Serialize(activeDocument));
        GameSession activeSession = new(GameplaySaveSessionLoader.Restore(activeDocument, directory, sharedAssets: assets));
        using GameWorld active = new(activeSession, directory, sharedAssets: assets);
        active.EnsureField();
        AssertExactRoundTrip(active);
        AssertTakeoverState(source, active);
        int callbacks = 0;
        while (source.Events.CaptureGameplaySaveFieldContinuation().PartyMovement is not null && callbacks < 16)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), active.Apply(new(PlayerActionKind.Wait)));
            AssertTakeoverState(source, active);
            callbacks++;
        }
        Assert.InRange(callbacks, 1, 15);
        Assert.NotNull(companion.NativeJoinedPosition);
        Assert.NotNull(companion.NativeMovementRequest);
        Assert.True(companion.NativeScriptMovement!.SupersededByPartyJoin);
        Assert.Equal((ushort?)2, companion.NativeMovementCompletion542);
        Assert.Equal(73, source.Events.EventMemory.Read(600, 1).Values[0]);
        Assert.Equal(originalBackup, companion.NativeMovementRequest);
        AssertExactRoundTrip(source);
        companion = source.Events.Actors[1];
        GameplaySaveComponentDocument completedDocument = EncodeContinuation(source, sourceSession, initial);
        string completedBefore = JsonSerializer.Serialize(completedDocument);
        GameSession completedSession = new(GameplaySaveSessionLoader.Restore(completedDocument, directory, sharedAssets: assets));
        using GameWorld completed = new(completedSession, directory, sharedAssets: assets);
        completed.EnsureField();
        AssertExactRoundTrip(completed);
        AssertTakeoverState(source, completed);
        Assert.Null(completed.Events.Actors[1].NativeJoinedPosition);
        JsonElement completedSnapshot = completed.Events.Capture();
        JsonNode missingPlacementOwner = JsonNode.Parse(completedSnapshot.GetRawText())!;
        missingPlacementOwner["Actors"]![1]!["GameplayReconstruction"] = null;
        Assert.Throws<InvalidDataException>(() => completed.Events.Restore(JsonSerializer.SerializeToElement(missingPlacementOwner)));
        Assert.True(JsonElement.DeepEquals(completedSnapshot, completed.Events.Capture()));
        bool returned = false;
        for (int tick = 0; tick < 40; tick++)
        {
            PlayerActionResult expected = source.Apply(new(PlayerActionKind.Wait));
            Assert.Equal(expected, active.Apply(new(PlayerActionKind.Wait)));
            Assert.Equal(expected, completed.Apply(new(PlayerActionKind.Wait)));
            AssertTakeoverState(source, active);
            AssertTakeoverState(source, completed);
            if (!returned && companion.NativeMovementRequest is null)
            {
                returned = true;
                NativeScriptMovementState resumed = Assert.IsType<NativeScriptMovementState>(companion.NativeScriptMovement);
                Assert.True(resumed.Active);
                Assert.False(resumed.SupersededByPartyJoin);
                Assert.Null(companion.NativeJoinedPosition);
                Assert.Equal((originalBackup.SavedMovement.FiberSequence, targetX << 12, targetY << 12, (short)1000), (resumed.FiberSequence, resumed.TargetX, resumed.TargetY, resumed.Speed));
                Assert.Equal(resumed, completed.Events.Actors[1].NativeScriptMovement);
            }
        }
        Assert.True(returned);
        Assert.Equal((ushort?)2, completed.Events.Actors[1].NativeMovementCompletion542);
        Assert.Equal(sourceSession.State.Economy!.Gil, completedSession.State.Economy!.Gil);
        Assert.Equal(activeSession.State.Economy!.Gil, completedSession.State.Economy.Gil);
        Assert.Equal(activeBefore, JsonSerializer.Serialize(activeDocument));
        Assert.Equal(completedBefore, JsonSerializer.Serialize(completedDocument));
    }

    private static GameplaySaveComponentDocument EncodeContinuation(GameWorld world, GameSession session, GameplaySaveComponentDocument initial)
    {
        GameplaySaveFieldContinuation continuation = world.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(session.PrepareSnapshot(), initial.Resume with { Field = continuation.GetLeaderPosition() });
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation) };
        using MemoryStream encoded = new();
        GameplaySaveCodec.Write(encoded, document);
        encoded.Position = 0;
        return GameplaySaveCodec.Read(encoded);
    }

    private static void AssertExactRoundTrip(GameWorld world)
    {
        JsonElement snapshot = world.Events.Capture();
        world.Events.Restore(snapshot);
        Assert.True(JsonElement.DeepEquals(snapshot, world.Events.Capture()));
    }

    private static void AssertTakeoverState(GameWorld expected, GameWorld actual)
    {
        AssertLogicalState(expected, actual);
        GameplaySaveActorMovement? expectedMovement = expected.Events.CaptureGameplaySaveFieldContinuation().Actors.Actors.Single(actor => actor.ObjectId == 1).Movement;
        GameplaySaveActorMovement? actualMovement = actual.Events.CaptureGameplaySaveFieldContinuation().Actors.Actors.Single(actor => actor.ObjectId == 1).Movement;
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expectedMovement), JsonSerializer.SerializeToElement(actualMovement)));
    }

    private static byte[] TakeoverProgram(bool asynchronous, (int X, int Y, int Triangle) origin, (int X, int Y, int Triangle) destination)
    {
        List<(Jsm.Opcode, int)[]> scripts = [];
        for (int slot = 0; slot < 3; slot++)
        {
            List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, slot * 16)];
            if (slot == 0)
            {
                init.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0)]);
            }
            init.AddRange([(Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, origin.X), (Jsm.Opcode.PSHN_L, origin.Y), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, origin.Triangle), (Jsm.Opcode.RET, 8)]);
            scripts.Add(init.ToArray());
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, slot * 16 + 1)];
            if (slot == 0)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.INITTRACE, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 18), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.JOIN, 0), (Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 600)]);
            }
            else if (slot == 1)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, destination.X), (Jsm.Opcode.PSHN_L, destination.Y), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, destination.Triangle), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, (origin.X + destination.X) / 2), (Jsm.Opcode.PSHN_L, (origin.Y + destination.Y) / 2), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.PSHN_L, 0), (asynchronous ? Jsm.Opcode.RMOVE : Jsm.Opcode.MOVE, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDGIL, 0)]);
            }
            else
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, origin.X), (Jsm.Opcode.PSHN_L, origin.Y), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, origin.Triangle), (Jsm.Opcode.FOLLOWOFF, 0)]);
            }
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            scripts.Add(main.ToArray());
            if (slot == 1)
            {
                scripts.Add([(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 24), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
            }
        }
        return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 2, (32 << 7) | 1], scripts);
    }

    private static void AssertLogicalState(GameWorld expected, GameWorld actual)
    {
        for (int objectId = 0; objectId < 3; objectId++)
        {
            ActorState source = expected.Events.Actors[objectId];
            ActorState target = actual.Events.Actors[objectId];
            Assert.Equal((source.X, source.Y, source.Z, source.WalkmeshTriangle, source.Direction, source.NativeMovementCompletion542, source.NativeTurnAccumulator474, source.NativeMotionAuthority), (target.X, target.Y, target.Z, target.WalkmeshTriangle, target.Direction, target.NativeMovementCompletion542, target.NativeTurnAccumulator474, target.NativeMotionAuthority));
        }
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.Events.Scene.PartyTrace), JsonSerializer.SerializeToElement(actual.Events.Scene.PartyTrace)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(actual.Events.CaptureGameplaySaveScripts())));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.Events.CaptureGameplaySaveFieldContinuation().PartyMovement), JsonSerializer.SerializeToElement(actual.Events.CaptureGameplaySaveFieldContinuation().PartyMovement)));
    }

    private static (int X, int Y, int Triangle) FindDestination(FieldAssets field, (int X, int Y, int Triangle) origin)
    {
        foreach ((int dx, int dy) in new[] { (128, 0), (-128, 0), (0, 128), (0, -128), (96, 96), (-96, 96), (96, -96), (-96, -96) })
        {
            if (Enumerable.Range(0, 65).All(step => FieldPartyTraceGeometry.Resolve(field.Walkmesh, origin.X + dx * step / 64, origin.Y + dy * step / 64, 567) is not null))
            {
                int x = origin.X + dx;
                int y = origin.Y + dy;
                return (x, y, FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, 567)!.Value);
            }
        }
        throw new InvalidDataException("The configured field resource has no bounded straight party-movement fixture route.");
    }

    private static byte[] Program(bool split, bool detachFollowers, (int X, int Y, int Triangle) origin, (int X, int Y, int Triangle) destination)
    {
        List<(Jsm.Opcode, int)[]> scripts = [];
        for (int slot = 0; slot < 3; slot++)
        {
            (int X, int Y, int Triangle) start = split || slot == 0 ? origin : destination;
            List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, slot * 16)];
            if (slot == 0)
            {
                init.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0)]);
            }
            init.AddRange([(Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, start.X), (Jsm.Opcode.PSHN_L, start.Y), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, start.Triangle), (Jsm.Opcode.RET, 8)]);
            scripts.Add(init.ToArray());
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, slot * 16 + 1)];
            if (slot == 0)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.UCON, 0), (Jsm.Opcode.INITTRACE, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0)]);
                for (int binding = 0; binding < 3; binding++)
                {
                    main.AddRange([(Jsm.Opcode.PSHM_B, 194 + binding), (Jsm.Opcode.POPM_B, 610 + binding)]);
                }
                if (split)
                {
                    for (int target = 0; target < 3; target++)
                    {
                        main.AddRange([(Jsm.Opcode.PSHN_L, destination.X), (Jsm.Opcode.PSHN_L, destination.Y), (Jsm.Opcode.PSHN_L, 567)]);
                    }
                }
                main.AddRange([(split ? Jsm.Opcode.SPLIT : Jsm.Opcode.JOIN, 0), (Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 600)]);
            }
            else
            {
                // INITTRACE establishes the trail and also places followers; restore the fixture's walkmesh source afterward.
                main.AddRange([(Jsm.Opcode.PSHN_L, start.X), (Jsm.Opcode.PSHN_L, start.Y), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, start.Triangle)]);
                if (detachFollowers)
                {
                    main.Add((Jsm.Opcode.FOLLOWOFF, 0));
                }
            }
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            scripts.Add(main.ToArray());
        }
        return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 1, (32 << 7) | 1], scripts);
    }

    private sealed class ScriptOverride(string fieldName, byte[] script) : IAssetOverrideSource
    {
        public bool TryRead(string logicalPath, out byte[] content)
        {
            if (Path.GetFileName(logicalPath).Equals(fieldName + ".jsm", StringComparison.OrdinalIgnoreCase))
            {
                content = script.ToArray();
                return true;
            }
            content = [];
            return false;
        }
    }
}
