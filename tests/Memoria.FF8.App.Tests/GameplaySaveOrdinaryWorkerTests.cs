using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveOrdinaryWorkerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Controllable_field_resumes_move_sync_or_shortest_turn_and_awards_once(bool turn, bool controlledLeader)
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
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(turn, controlledLeader, triangle)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession session = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        source.Apply(new(PlayerActionKind.Wait));
        source.Apply(new(PlayerActionKind.Wait));
        Assert.True(source.Events.CanAcceptMovementInput);
        int workerObject = controlledLeader ? 0 : 1;
        if (controlledLeader)
        {
            Assert.NotEqual(MathF.Truncate(source.Events.Actors[workerObject].Direction), source.Events.Actors[workerObject].Direction);
        }
        GameplaySaveComponentDocument document = source.CaptureGameplaySave();
        GameplaySaveFieldContinuation continuation = document.Progression!.FieldContinuation!;
        Assert.Contains(continuation.Scripts.Fibers, fiber => fiber.ObjectId == workerObject && fiber.Wait == (turn ? GameplaySaveScriptWait.Rotation : GameplaySaveScriptWait.Movement));
        if (turn)
        {
            Assert.Equal((byte)3, continuation.Actors.Actors[workerObject].Turn!.Kind);
        }
        using MemoryStream bytes = new();
        GameplaySaveCodec.Write(bytes, document);
        bytes.Position = 0;
        GameSession restoredSession = new(GameplaySaveSessionLoader.Restore(GameplaySaveCodec.Read(bytes), directory, sharedAssets: assets));
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        Assert.Equal(source.Events.Actors[workerObject].Direction, restored.Events.Actors[workerObject].Direction);
        uint gil = session.State.Economy!.Gil;
        for (int tick = 0; tick < 180; tick++)
        {
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.Equal(source.Events.Actors[workerObject].X, restored.Events.Actors[workerObject].X);
            Assert.Equal(source.Events.Actors[workerObject].Direction, restored.Events.Actors[workerObject].Direction);
            Assert.Equal(session.State.Economy!.Gil, restoredSession.State.Economy!.Gil);
        }
        Assert.Equal(gil + 11, restoredSession.State.Economy!.Gil);
        Assert.Null(restored.Events.Actors[workerObject].RotationTarget);
        if (turn)
        {
            Assert.Equal((byte?)3, restored.Events.Actors[workerObject].NativeTurnMode580);
        }
        else
        {
            Assert.Equal((ushort?)2, restored.Events.Actors[workerObject].NativeMovementCompletion542);
        }
    }

    private static byte[] Program(bool turn, bool controlledLeader, int triangle)
    {
        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] actor = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, -700), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, triangle), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> worker = [(Jsm.Opcode.LBL, controlledLeader ? 1 : 17)];
        if (turn)
        {
            worker.AddRange([(Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.LTURN, 0)]);
        }
        else
        {
            worker.AddRange([(Jsm.Opcode.PSHN_L, -690), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RMOVE, 0), (Jsm.Opcode.MOVESYNC, 0)]);
        }
        worker.AddRange([(Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, -650), (Jsm.Opcode.PSHN_L, -8593), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, triangle), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] controller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] idle = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 1, (32 << 7) | 1], [player, controlledLeader ? worker.ToArray() : controller, actor, controlledLeader ? idle : worker.ToArray(), target, [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
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
