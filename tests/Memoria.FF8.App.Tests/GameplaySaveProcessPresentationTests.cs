using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveProcessPresentationTests
{
    [Fact]
    public void Logical_playback_keeps_the_new_process_first_load_owner_and_validates_concert_selection()
    {
        NativeMusicPlaybackState source = Playback(2) with { BattleFirstLoadB8B7D4 = false };
        GameplaySaveMusicPlayback logical = GameplaySaveMusicPlayback.Capture(source)!;
        NativeMusicPlaybackState restored = logical.Restore(NativeMusicPlaybackState.FromColdProcess());
        Assert.True(restored.BattleFirstLoadB8B7D4);
        Assert.Equal(source with { BattleFirstLoadB8B7D4 = true }, restored);
        Assert.Throws<InvalidDataException>(() => (logical with { CurrentMusicId0 = 26 }).Validate());
        Assert.DoesNotContain("BattleFirstLoad", JsonSerializer.Serialize(logical));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Field_and_world_semantic_documents_restore_salary_without_repaying_or_changing_rank(bool world)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, GameplaySaveFieldScriptsTests.Encode([1], [initialize, ordinary])));
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, world);
        GameState initialized = GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets);
        NativeSalaryPresentation salary = initialized.SalaryPresentation!.StartPayment(4, 5, 4000, 5000).Advance(21);
        NativeMusicPlaybackState playback = Playback(2) with { BattleFirstLoadB8B7D4 = false };
        GameState source = initialized with { SalaryPresentation = salary, SeeD = initialized.SeeD! with { SalaryDisplayCountdown184 = 130 }, GlobalSoundProcess = initialized.GlobalSoundProcess! with { MusicPlayback = playback } };
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(source, initial.Resume);
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-process-presentation-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (FileStream destination = File.Create(path))
            {
                GameplaySaveCodec.Write(destination, document);
            }
            GameState restored = GameplaySaveSessionLoader.Load(path, directory, sharedAssets: assets);
            Assert.Equal(JsonSerializer.Serialize(salary), JsonSerializer.Serialize(restored.SalaryPresentation));
            Assert.Equal(source.Economy!.Gil, restored.Economy!.Gil);
            Assert.Equal(source.SeeD!.RankPoints, restored.SeeD!.RankPoints);
            Assert.Equal(source.SeeD.SalaryDisplayCountdown184, restored.SeeD.SalaryDisplayCountdown184);
            Assert.Equal(playback with { BattleFirstLoadB8B7D4 = true }, restored.GlobalSoundProcess!.MusicPlayback);
            Assert.Equal(JsonSerializer.Serialize(salary.Advance(2)), JsonSerializer.Serialize(restored.SalaryPresentation!.Advance(2)));
            Assert.Equal(1, document.Progression!.RuntimeOwnershipVersion);
            GameplaySaveComponentDocument cold = initial with { Progression = initial.Progression! with { RuntimeOwnershipVersion = 1, MusicPlayback = null, SalaryPresentation = null } };
            GameState coldRestored = GameplaySaveSessionLoader.Restore(cold, directory, sharedAssets: assets);
            Assert.True(coldRestored.SalaryPresentation!.IsColdProcess);
            Assert.True(coldRestored.GlobalSoundProcess!.MusicPlayback!.IsColdProcess);
            GameplaySaveComponentDocument old = document with { Progression = document.Progression with { RuntimeOwnershipVersion = null } };
            using MemoryStream legacyBytes = new();
            GameplaySaveCodec.Write(legacyBytes, old);
            legacyBytes.Position = 0;
            GameplaySaveComponentDocument inspected = GameplaySaveCodec.Read(legacyBytes);
            Assert.Null(inspected.Progression!.RuntimeOwnershipVersion);
            string before = JsonSerializer.Serialize(inspected);
            NotSupportedException refusal = Assert.Throws<NotSupportedException>(() => GameplaySaveSessionLoader.Restore(inspected, "missing-selected-installation"));
            Assert.Contains("authoritative standalone snapshot", refusal.Message);
            Assert.Equal(before, JsonSerializer.Serialize(inspected));
            Assert.Throws<InvalidDataException>(() => (document.Progression with { RuntimeOwnershipVersion = 2 }).Validate());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Playing_stopped_secondary_and_concert_selections_survive_until_the_next_musicchange(int selection)
    {
        string directory = TestResources.GameDirectory;
        string fieldName;
        using (GameAssets original = new(directory))
        {
            fieldName = original.LoadField(165).Name;
        }
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] ordinary = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 26), (Jsm.Opcode.MUSICLOAD, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.MUSICCHANGE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        using GameAssets assets = new(directory, overrides: new ScriptOverride(fieldName, GameplaySaveFieldScriptsTests.Encode([1], [initialize, ordinary])));
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, false), directory, sharedAssets: assets));
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        session.SetGlobalSoundProcess(session.GlobalSoundProcess! with { MusicPlayback = Playback(selection) });
        for (int tick = 0; source.Events.Scene.MusicProcess?.Pending != true && tick < 10; tick++)
        {
            source.Events.Step();
        }
        Assert.True(source.Events.Scene.MusicProcess!.Pending);
        NativeMusicPlaybackState playback = session.GlobalSoundProcess!.MusicPlayback!;
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-playing-music-" + Guid.NewGuid().ToString("N"));
        try
        {
            source.WriteGameplaySave(path);
            GameSession restored = new(PlayerStateLoader.Load(path, directory, sharedAssets: assets));
            using GameWorld resumed = new(restored, directory, sharedAssets: assets);
            resumed.EnsureField();
            Assert.Equal(playback, restored.GlobalSoundProcess!.MusicPlayback);
            List<(int Slot, int Music, int Gain)> tracks = [];
            uint? concert = null;
            FieldAudioPresentation.DispatchNativeMusicResume(restored.GlobalSoundProcess.MusicPlayback, (slot, music, gain) => tracks.Add((slot, music, gain)), (mask, gain) => concert = mask);
            Assert.Equal(selection == 2 ? playback.ConcertMask : null, concert);
            if (selection != 0)
            {
                Assert.Contains((1, 3, 51), tracks);
            }
            if (selection == 0)
            {
                Assert.Contains((0, 26, 83), tracks);
            }
            for (int tick = 0; tick < 10; tick++)
            {
                source.Events.Step();
                resumed.Events.Step();
                Assert.Equal(session.GlobalSoundProcess!.MusicPlayback, restored.GlobalSoundProcess.MusicPlayback);
                Assert.Equal(source.Events.Scene.MusicPlayRequest, resumed.Events.Scene.MusicPlayRequest);
            }
            Assert.Equal(26, restored.GlobalSoundProcess.MusicPlayback!.CurrentMusicId0);
            Assert.Null(restored.GlobalSoundProcess.MusicPlayback.ConcertMask);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static NativeMusicPlaybackState Playback(int selection)
    {
        return selection switch
        {
            0 => new(83, 0, 26, 0) { Mode0 = NativeMusicPlaybackMode.Midi, Mode1 = NativeMusicPlaybackMode.Stopped, BattleFirstLoadB8B7D4 = true },
            1 => new(83, 51, 0, 3) { Mode0 = NativeMusicPlaybackMode.Stopped, Mode1 = NativeMusicPlaybackMode.Wave, BattleFirstLoadB8B7D4 = true },
            _ => new(83, 51, 43, 3) { Mode0 = NativeMusicPlaybackMode.Midi, Mode1 = NativeMusicPlaybackMode.Wave, ConcertMask = 0x1234, BattleFirstLoadB8B7D4 = true }
        };
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
