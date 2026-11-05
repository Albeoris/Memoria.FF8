using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveNativeMovementTests
{
    [Theory]
    [InlineData("point")]
    [InlineData("async")]
    [InlineData("held")]
    [InlineData("tracking")]
    [InlineData("backup")]
    [InlineData("replace")]
    [InlineData("flush")]
    [InlineData("async-tracking")]
    public void Current_geometry_movement_reconstructs_worker_issue_and_backup_without_reissuing_move(string kind)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        int triangle;
        using (GameAssets original = new(directory))
        {
            FieldAssets field = original.LoadField(165);
            fieldName = field.Name;
            triangle = FieldPartyTraceGeometry.Resolve(field.Walkmesh, -700, -8593, 567)!.Value;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(kind, triangle)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        for (int tick = 0; tick < (kind is "backup" or "replace" or "flush" ? 4 : 2); tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(source.Events.IsUserControlEnabled);
        if (kind == "point")
        {
            ActorState npc = source.Events.Actors[1];
            NativeScriptMovementState current = npc.NativeScriptMovement!;
            source.Events.Scene.LockedTriangles.Add(npc.WalkmeshTriangle);
            Assert.True(NativeScriptMovementAdapter.CanRestorePlacement(source.Field!, source.Events.Scene, npc, new(current.FixedX, current.FixedY, current.FixedZ + 4096)));
            source.Events.Scene.LockedTriangles.Remove(npc.WalkmeshTriangle);
        }
        if (kind == "replace")
        {
            Assert.NotEqual(source.Events.Actors[1].NativeMovementRequest!.SavedMovement.FixedX, source.Events.Actors[1].NativeScriptMovement!.FixedX);
        }
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveActorMovement movement = continuation.Actors.Actors[1].Movement!;
        Assert.NotNull(movement.Worker);
        Assert.Equal(kind != "replace", movement.Worker.Active);
        Assert.Equal(kind is "backup" or "replace" or "flush", movement.Backup is not null);
        if (kind is "tracking" or "async-tracking")
        {
            Assert.Equal(2, movement.Worker.TrackingObjectId);
            if (kind == "tracking")
            {
                Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.ObjectId == 1 && fiber.MovementIssue?.TrackingObjectId == 2);
            }
        }
        if (kind is "async" or "async-tracking")
        {
            Assert.True(movement.Worker.Asynchronous);
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
        GameplaySaveFieldActor[] actors = valid.Actors.Actors.ToArray();
        actors[1] = actors[1] with { Pose = actors[1].Pose with { Triangle = ushort.MaxValue } };
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Actors = valid.Actors with { Actors = actors } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        GameState restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredState.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredState.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredState.ExamTimer!.ClockCalls);
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        Assert.Equal(continuation.Scripts.Frame, restored.Events.Frame);
        for (int tick = 0; tick < 10; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            ActorState expected = source.Events.Actors[1];
            ActorState actual = restored.Events.Actors[1];
            Assert.Equal((expected.X, expected.Y, expected.Z, expected.WalkmeshTriangle, expected.Direction), (actual.X, actual.Y, actual.Z, actual.WalkmeshTriangle, actual.Direction));
            Assert.Equal(expected.NativeMovementCompletion542, actual.NativeMovementCompletion542);
            Assert.Equal(expected.NativeScriptMovement, actual.NativeScriptMovement);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            GameState exactState = restoredSession.PrepareSnapshot();
            using GameWorld exact = new(new GameSession(exactState), directory, sharedAssets: assets);
            exact.EnsureField();
            Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), exact.Events.Capture()));
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
        }
        Assert.NotEqual(-700, restored.Events.Actors[1].X);
    }

    private static byte[] Program(string kind, int triangle)
    {
        List<(Jsm.Opcode, int)> player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> controller = [(Jsm.Opcode.LBL, 1)];
        if (kind is "backup" or "replace" or "flush")
        {
            controller.AddRange([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 18), (Jsm.Opcode.REQ, 1)]);
        }
        controller.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        List<(Jsm.Opcode, int)> actor = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, -700), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, triangle), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> move = [(Jsm.Opcode.LBL, 17)];
        int[] operands = kind is "tracking" or "async-tracking" ? [2, 0] : [-650, -8593, 567, 0];
        move.AddRange(operands.Select(value => (Jsm.Opcode.PSHN_L, value)));
        move.Add((kind switch { "async" => Jsm.Opcode.RMOVE, "async-tracking" => Jsm.Opcode.RMOVEA, "held" => Jsm.Opcode.CMOVE, "tracking" => Jsm.Opcode.MOVEA, _ => Jsm.Opcode.MOVE }, 0));
        if (kind is "async" or "async-tracking")
        {
            move.AddRange([(Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0)]);
        }
        move.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        List<(Jsm.Opcode, int)> request = [(Jsm.Opcode.LBL, 18)];
        if (kind == "flush")
        {
            request.Add((Jsm.Opcode.MOVEFLUSH, 0));
        }
        if (kind == "replace")
        {
            request.AddRange([(Jsm.Opcode.PSHN_L, -690), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.FMOVE, 0)]);
        }
        request.AddRange([(Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
        (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, -650), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, triangle), (Jsm.Opcode.RET, 8)];
        return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 2, (32 << 7) | 1], [player.ToArray(), controller.ToArray(), actor.ToArray(), move.ToArray(), request.ToArray(), target, [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
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
