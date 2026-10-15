using System.Runtime.InteropServices;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleAudioPresentationTests
{
    [Fact]
    public void NewDamageAndOutcomePlayOnceAndReturnStopsBattleOutput()
    {
        List<string> cues = [];
        BattleAudioPresentation output = Output(cues);
        BattleSession battle = Battle(enemyHp: 10);
        output.Update(battle);
        battle.QueueAttack("party", "enemy");
        output.Update(battle);
        TickAttack(battle);
        string state = JsonSerializer.Serialize(battle.Capture());
        output.Update(battle);
        output.Update(battle);
        Assert.Equal(state, JsonSerializer.Serialize(battle.Capture()));
        Assert.True(output.Update(null));
        Assert.False(output.Update(null));
        Assert.Equal(new[] { "stop", "music:5", "impact:0", "music:1", "stop" }, cues);
    }

    [Fact]
    public void RestoredBattleStartsCurrentMusicWithoutRepeatingHistoricalEffects()
    {
        BattleSession battle = Battle();
        battle.QueueAttack("party", "enemy");
        TickAttack(battle);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        List<string> cues = [];
        BattleAudioPresentation output = Output(cues);
        output.Update(restored);
        output.Update(restored);
        Assert.Equal(new[] { "stop", "music:5" }, cues);
        BattleSession replacement = Battle(enemyHp: 10);
        replacement.InstantVictory();
        output.Update(replacement);
        Assert.Equal(new[] { "stop", "music:5", "stop", "music:1" }, cues);
    }

    [Fact]
    public void EnemyDamageUsesNativeActorIdentityAndDefeatChangesMusic()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 1, CurrentHp = 1 }, new() { Id = "enemy", Name = "Enemy", ModelId = 1, InitialAtb = BattleSession.AtbMaximum }] }, BattleAtbMode.Normal);
        List<string> cues = [];
        BattleAudioPresentation output = Output(cues);
        output.Update(battle);
        TickAttack(battle);
        output.Update(battle);
        Assert.Equal(BattleOutcome.Defeat, battle.Outcome);
        Assert.Equal(new[] { "stop", "music:5", "impact:17", "music:0" }, cues);
    }

    [Fact]
    public void InstalledActorSoundsAndMusicResolveThroughOriginalTables()
    {
        string directory = InstalledGame();
        NativeMusicCatalog catalog = new(directory);
        Assert.Equal("005s-battle.sgt", Path.GetFileName(catalog.MusicFile(5, -1)), ignoreCase: true);
        Assert.Equal("001s-win.sgt", Path.GetFileName(catalog.MusicFile(1, -1)), ignoreCase: true);
        Assert.Equal("000s-lose.sgt", Path.GetFileName(catalog.MusicFile(0, -1)), ignoreCase: true);
        Assert.Equal(210009u, catalog.BattleActorSound(0, 1));
        Assert.Equal(159, EventEngine.MapGlobalSoundSample(catalog.BattleActorSound(0, 1)));
        Assert.Equal(0u, catalog.BattleActorSound(16, 0));
        Assert.Equal(220037u, catalog.BattleActorSound(17, 0));
        Assert.Equal(220025u, catalog.BattleActorSound(38, 0));
        foreach (int actor in Enumerable.Range(0, 11).Concat([17, 38]))
        {
            uint effect = catalog.BattleActorSound(actor, actor == 0 ? 1 : 0);
            PcmAudio pcm = PcmAudioDecoder.ReadSample(directory, EventEngine.MapGlobalSoundSample(effect));
            Assert.NotEmpty(pcm.Bytes);
            Assert.Contains(pcm.Bytes, value => value != 0);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.BattleActorSound(160, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.BattleActorSound(0, 7));
    }

    [Fact]
    public void SilentDeviceBridgePlaysOriginalBattleMusicAndBothSidesImpact()
    {
        string directory = InstalledGame();
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                string tools = new MovieClock(GameSession.CreateColdProcess(), directory).ToolsDirectory;
                using FieldAudioPresentation presentation = new(GetDesktopWindow(), directory, tools);
                presentation.SetOutputPreferences(0, 0);
                presentation.PlayBattleMusic(5);
                Assert.Null(presentation.Error);
                presentation.PlayBattleImpact(0);
                Assert.Null(presentation.Error);
                presentation.PlayBattleImpact(16);
                Assert.Null(presentation.Error);
                presentation.PlayBattleImpact(17);
                Assert.Null(presentation.Error);
                presentation.PlayBattleImpact(38);
                Assert.Null(presentation.Error);
                presentation.PlayBattleMusic(1);
                Assert.Null(presentation.Error);
                presentation.StopAll();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Silent battle audio smoke did not finish.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static BattleAudioPresentation Output(List<string> cues) => new(() => cues.Add("stop"), track => cues.Add($"music:{track}"), actor => cues.Add($"impact:{actor}"));

    private static BattleSession Battle(int enemyHp = 1000) => new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Attack = 30 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = enemyHp, CurrentHp = enemyHp, Speed = 1 }] }, BattleAtbMode.Normal);

    private static void TickAttack(BattleSession battle)
    {
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
    }

    private static string InstalledGame()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original audio corpus is required.");
        return directory;
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
