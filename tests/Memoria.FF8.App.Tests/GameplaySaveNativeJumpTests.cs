using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveNativeJumpTests
{
    [Theory]
    [InlineData("jump", 2)]
    [InlineData("jump3", 3)]
    [InlineData("party", 2)]
    [InlineData("jump", 6)]
    [InlineData("async", 2)]
    public void Cold_ballistic_resume_preserves_held_instruction_landing_and_native_completion(string kind, int ticks)
    {
        CheckResume(kind, ticks);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void Requested_jump_preserves_lower_movement_backup_and_returns_at_current_landing(int ticks)
    {
        CheckResume("request", ticks);
    }

    private static void CheckResume(string kind, int ticks)
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
        for (int tick = 0; tick < ticks; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(source.Events.IsUserControlEnabled);
        GameplaySaveFieldContinuation continuation = source.Events.CaptureGameplaySaveFieldContinuation();
        GameplaySaveActorJump jump = continuation.Actors.Actors[1].Jump!;
        Assert.NotNull(jump);
        Assert.Equal(kind != "request" || ticks < 9, jump.Active);
        if (jump.Active)
        {
            Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.ObjectId == 1 && fiber.Position == jump.Operation && fiber.Wait == GameplaySaveScriptWait.NativeJump);
        }
        if (kind is "async" or "request")
        {
            Assert.Equal(kind == "async", jump.AsyncFlag);
        }
        Assert.Equal(kind == "request", continuation.Actors.Actors[1].Movement?.Backup is not null);
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
        actors[1] = actors[1] with { Jump = jump with { Velocity = jump.Velocity + 1 } };
        GameplaySaveComponentDocument malformed = decoded with { Progression = decoded.Progression with { FieldContinuation = valid with { Actors = valid.Actors with { Actors = actors } } } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        actors[1] = valid.Actors.Actors[1] with { Jump = jump with { LandingPlane = ushort.MaxValue } };
        Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        if (kind == "request")
        {
            GameplaySaveActorMovement movement = valid.Actors.Actors[1].Movement!;
            GameplaySaveMovementBackup backup = movement.Backup!;
            actors[1] = valid.Actors.Actors[1] with { Movement = movement with { Backup = backup with { RequestScriptId = backup.RequestScriptId + 1 } } };
            Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
            actors[1] = valid.Actors.Actors[1] with { Movement = movement with { Backup = backup with { Worker = backup.Worker with { Target = backup.Worker.Target with { Z = backup.Worker.Target.Z + 1 } } } } };
            Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
            actors[1] = valid.Actors.Actors[1] with { Jump = jump with { AnimationFlag = null } };
            Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
            actors[1] = valid.Actors.Actors[1] with { Movement = movement with { Backup = backup with { Worker = backup.Worker with { AutomaticAnimation = false } } } };
            Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
        }
        GameState restoredState = GameplaySaveSessionLoader.Restore(decoded, directory, sharedAssets: assets);
        Assert.Equal(before, JsonSerializer.Serialize(decoded));
        Assert.Equal(prepared.CrtRandom, restoredState.CrtRandom);
        Assert.Equal(prepared.Playtime, restoredState.Playtime);
        Assert.Equal(prepared.ExamTimer!.ClockCalls, restoredState.ExamTimer!.ClockCalls);
        GameSession restoredSession = new(restoredState);
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        Assert.Equal(continuation.Scripts.Frame, restored.Events.Frame);
        if (kind == "request")
        {
            NativeMovementRequestState backup = source.Events.Actors[1].NativeMovementRequest!;
            NativeMovementRequestState restoredBackup = restored.Events.Actors[1].NativeMovementRequest!;
            Assert.Equal(backup.SavedMovement.Speed, restoredBackup.SavedMovement.Speed);
            Assert.Equal((backup.SavedMovement.TargetX, backup.SavedMovement.TargetY, backup.SavedMovement.TargetZ), (restoredBackup.SavedMovement.TargetX, restoredBackup.SavedMovement.TargetY, restoredBackup.SavedMovement.TargetZ));
            using GameWorld airborneExact = new(new GameSession(restoredSession.PrepareSnapshot()), directory, sharedAssets: assets);
            airborneExact.EnsureField();
            Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), airborneExact.Events.Capture()));
            if (ticks == 4)
            {
                Assert.NotEqual(0, restoredBackup.SavedMovement.FixedZ & 4095);
                JsonNode wrongOrigin = JsonNode.Parse(restored.Events.Capture().GetRawText())!;
                wrongOrigin["Actors"]![1]!["GameplayReconstruction"]!["Position"]!["Z"] = restoredBackup.SavedMovement.FixedZ + 1;
                InvalidDataException refused = Assert.Throws<InvalidDataException>(() => airborneExact.Events.Restore(JsonSerializer.SerializeToElement(wrongOrigin)));
                Assert.Contains("movement backup", refused.Message, StringComparison.Ordinal);
            }
        }
        for (int tick = 0; tick < 12; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            ActorState expected = source.Events.Actors[1];
            ActorState actual = restored.Events.Actors[1];
            Assert.Equal((expected.X, expected.Y, expected.Z, expected.WalkmeshTriangle, expected.Direction), (actual.X, actual.Y, actual.Z, actual.WalkmeshTriangle, actual.Direction));
            Assert.Equal(expected.NativeMovementCompletion542, actual.NativeMovementCompletion542);
            Assert.Equal(expected.NativeJump, actual.NativeJump);
            Assert.Equal(expected.NativeScriptMovement, actual.NativeScriptMovement);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(restored.Events.CaptureGameplaySaveScripts())));
            using GameWorld exact = new(new GameSession(restoredSession.PrepareSnapshot()), directory, sharedAssets: assets);
            exact.EnsureField();
            Assert.True(JsonElement.DeepEquals(restored.Events.Capture(), exact.Events.Capture()));
            Assert.Equal(prepared.Economy!.Gil, restoredSession.State.Economy!.Gil);
        }
        Assert.Equal(77, restored.Events.EventMemory.Read(600, 4).Values[0]);
        Assert.Equal((ushort?)2, restored.Events.Actors[1].NativeMovementCompletion542);
    }

    private static byte[] Program(string kind, int triangle)
    {
        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> controller = [(Jsm.Opcode.LBL, 1)];
        if (kind == "request")
        {
            controller.AddRange([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 18), (Jsm.Opcode.REQ, 1)]);
        }
        controller.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        (Jsm.Opcode, int)[] actor = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.TALKOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, -700), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, triangle), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> move = [(Jsm.Opcode.LBL, 17)];
        if (kind is "async" or "request")
        {
            move.AddRange([(Jsm.Opcode.PSHN_L, kind == "request" ? -679 : -650), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.PSHN_L, 0), (kind == "request" ? Jsm.Opcode.MOVE : Jsm.Opcode.RMOVE, 0)]);
        }
        if (kind != "request")
        {
            AddJump(move, kind, triangle);
        }
        move.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_L, 600), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        List<(Jsm.Opcode, int)> request = [(Jsm.Opcode.LBL, 18)];
        AddJump(request, "jump3", triangle);
        request.Add((Jsm.Opcode.RET, 8));
        return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 2], [player, controller.ToArray(), actor, move.ToArray(), request.ToArray()]);
    }

    private static void AddJump(List<(Jsm.Opcode, int)> script, string kind, int triangle)
    {
        if (kind == "party")
        {
            script.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PJUMPA, 0)]);
            return;
        }
        script.AddRange([(Jsm.Opcode.PSHN_L, -680), (Jsm.Opcode.PSHN_L, -8593)]);
        if (kind != "jump")
        {
            script.Add((Jsm.Opcode.PSHN_L, 900));
        }
        script.AddRange([(Jsm.Opcode.PSHN_L, 4), (kind == "jump" ? Jsm.Opcode.JUMP : Jsm.Opcode.JUMP3, triangle)]);
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
