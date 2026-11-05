using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySavePendingContactTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ordinary_move_preserves_pending_push_or_door_contact_until_one_next_callback_dispatch(bool door)
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
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(door, triangle)));
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        for (int callback = 0; callback < 2; callback++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Null(source.Events.LastFault);
        Assert.True(source.Events.CanAcceptMovementInput);
        uint gil = session.Economy!.Gil;
        Assert.True(source.Apply(new(PlayerActionKind.Move, Vertical: -1)).Accepted);
        Assert.Null(source.Events.LastFault);
        Assert.True(source.Events.CanAcceptMovementInput);
        byte pendingDoorEvents = 0;
        if (door)
        {
            Assert.Equal(2, source.Events.Scene.NativeDoorDriverVersion);
            pendingDoorEvents = source.Events.Scene.NativeDoorPendingEvents!.GetValueOrDefault(1);
            Assert.NotEqual((byte)0, pendingDoorEvents);
        }
        else
        {
            Assert.True(source.Events.Actors[1].NativePushContactPending);
        }
        Assert.Equal(gil, session.Economy.Gil);
        string sourceBefore = source.Events.Capture().GetRawText();
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-pending-contact-" + Guid.NewGuid().ToString("N"));
        try
        {
            source.WriteGameplaySave(path);
            byte[] originalBytes = File.ReadAllBytes(path);
            Assert.Equal(sourceBefore, source.Events.Capture().GetRawText());
            GameSession restoredSession = new(PlayerStateLoader.Load(path, directory, sharedAssets: assets));
            using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
            restored.EnsureField();
            Assert.Equal(gil, restoredSession.Economy!.Gil);
            if (door)
            {
                Assert.Equal(pendingDoorEvents, restored.Events.Scene.NativeDoorPendingEvents!.GetValueOrDefault(1));
            }
            else
            {
                Assert.True(restored.Events.Actors[1].NativePushContactPending);
            }
            List<int> sourceDispatches = [];
            List<int> restoredDispatches = [];
            source.Events.CommandExecuted += command => Record(command, sourceDispatches);
            restored.Events.CommandExecuted += command => Record(command, restoredDispatches);
            Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.NotEmpty(sourceDispatches);
            Assert.Equal(sourceDispatches, restoredDispatches);
            Assert.Equal(1, restoredDispatches.Count(script => script == (door ? 23 : 19)));
            Assert.Equal(gil + 11, restoredSession.Economy.Gil);
            if (door)
            {
                Assert.Empty(restored.Events.Scene.NativeDoorPendingEvents!);
            }
            else
            {
                Assert.False(restored.Events.Actors[1].NativePushContactPending);
            }
            for (int callback = 0; callback < 3; callback++)
            {
                Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
                Assert.Equal(session.Economy.Gil, restoredSession.Economy.Gil);
            }
            Assert.Equal(gil + 11, restoredSession.Economy.Gil);
            Assert.Equal(1, restoredDispatches.Count(script => script == (door ? 23 : 19)));
            Assert.Equal(originalBytes, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }

        static void Record(ScriptCommand command, List<int> dispatches)
        {
            if (command.ObjectId == 1 && command.Name == "LBL")
            {
                dispatches.Add(command.ScriptId);
            }
        }
    }

    private static byte[] Program(bool door, int triangle)
    {
        (Jsm.Opcode, int)[] Empty(int label) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] idle = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] reward = [(Jsm.Opcode.LBL, door ? 23 : 19), (Jsm.Opcode.PSHN_L, 11), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        if (!door)
        {
            (Jsm.Opcode, int)[] actor = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHON, 0), (Jsm.Opcode.PSHN_L, 40), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, -700), (Jsm.Opcode.PSHN_L, -8583), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SET3, triangle), (Jsm.Opcode.RET, 8)];
            return GameplaySaveFieldScriptsTests.Encode([1, (16 << 7) | 3], [player, idle, actor, [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Empty(18), reward]);
        }
        (Jsm.Opcode, int)[] line = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, -750), (Jsm.Opcode.PSHN_L, -8580), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.PSHN_L, -650), (Jsm.Opcode.PSHN_L, -8580), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 8)];
        byte[] program = GameplaySaveFieldScriptsTests.Encode([(16 << 7) | 7, 1], [player, idle, line, [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Empty(18), Empty(19), Empty(20), Empty(21), Empty(22), reward]);
        program[1] = 1;
        program[3] = 1;
        return program;
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
