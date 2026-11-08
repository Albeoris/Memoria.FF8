using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveAnimationContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Completed_single_pose_range_preserves_its_first_word_in_current_and_backup_animation(bool backup)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, Program(backup)));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameSession session = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        for (int tick = 0; tick < 4; tick++)
        {
            Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Null(source.Events.LastFault);
        NativeActorAnimationHeader header = source.Events.Actors[1].NativeAnimationHeader!;
        NativeActorAnimationBackup savedBackup = source.Events.Actors[1].NativeAnimationBackup!;
        Assert.Equal((ushort)16, backup ? savedBackup.First : header.First);
        Assert.Equal((ushort?)0, backup ? savedBackup.Terminal : header.Terminal);
        Assert.Equal((ushort?)0, backup ? savedBackup.Frame : header.Frame);
        Assert.Equal((ushort?)0x1800, backup ? savedBackup.Flags : header.Flags);
        GameplaySaveFieldContinuation before = source.Events.CaptureGameplaySaveFieldContinuation();
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-single-pose-" + Guid.NewGuid().ToString("N") + ".sav");
        try
        {
            source.WriteGameplaySave(path);
            GameSession resumedSession = new(GameplaySaveSessionLoader.Load(path, directory, sharedAssets: assets));
            using GameWorld resumed = new(resumedSession, directory, sharedAssets: assets);
            resumed.EnsureField();
            Assert.Equal(header, resumed.Events.Actors[1].NativeAnimationHeader);
            Assert.Equal(savedBackup, resumed.Events.Actors[1].NativeAnimationBackup);
            Assert.Equal(session.State.CrtRandom, resumedSession.State.CrtRandom);
            Assert.Equal(session.State.Playtime, resumedSession.State.Playtime);
            Assert.Equal(session.State.Economy!.Gil, resumedSession.State.Economy!.Gil);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.Scripts), JsonSerializer.SerializeToElement(resumed.Events.CaptureGameplaySaveScripts())));
            for (int tick = 0; tick < 5; tick++)
            {
                Assert.Equal(source.Apply(new(PlayerActionKind.Wait)), resumed.Apply(new(PlayerActionKind.Wait)));
                Assert.Equal(source.Events.Actors[1].NativeAnimationHeader, resumed.Events.Actors[1].NativeAnimationHeader);
                Assert.Equal(source.Events.Actors[1].NativeAnimationBackup, resumed.Events.Actors[1].NativeAnimationBackup);
                Assert.Equal(session.State.Economy!.Gil, resumedSession.State.Economy!.Gil);
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Events.CaptureGameplaySaveScripts()), JsonSerializer.SerializeToElement(resumed.Events.CaptureGameplaySaveScripts())));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] Program(bool backup)
    {
        (Jsm.Opcode, int)[] leaderInit = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.ADDGIL, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] leader = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] actorInit = [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> actor = [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.RCANIMEKEEP, 1)];
        if (backup)
        {
            actor.AddRange([(Jsm.Opcode.PUSHANIME, 0), (Jsm.Opcode.RANIMELOOP, 1)]);
        }
        actor.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        return GameplaySaveFieldScriptsTests.Encode([1, (8 << 7) | 1], [leaderInit, leader, actorInit, actor.ToArray()]);
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
