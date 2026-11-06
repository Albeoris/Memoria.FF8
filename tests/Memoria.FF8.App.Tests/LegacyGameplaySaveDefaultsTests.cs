using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LegacyGameplaySaveDefaultsTests
{
    [Fact]
    public void Missing_presentation_uses_safe_defaults_without_changing_saved_progress_or_scripts()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument current = GameplaySaveSessionLoaderTests.Document(assets, false);
        GameplaySaveComponentDocument legacy = current with { Progression = current.Progression! with { RuntimeOwnershipVersion = null, MusicPlayback = null, SalaryPresentation = null } };
        string before = JsonSerializer.Serialize(legacy);
        List<string> warnings = [];
        GameState loaded = GameplaySaveSessionLoader.Restore(legacy, directory, sharedAssets: assets, reportWarning: warnings.Add);
        Assert.Single(warnings);
        Assert.Equal(current.Economy.Gil, loaded.Economy!.Gil);
        Assert.Equal(current.Progression!.Clocks.Playtime, loaded.Playtime);
        Assert.Equal(current.Progression.Clocks.CrtRandom, loaded.CrtRandom);
        Assert.Equal(current.Progression.Clocks.GlobalRandom, loaded.GlobalRandom);
        Assert.True(loaded.FieldShake!.IsColdProcess);
        Assert.True(loaded.SalaryPresentation!.IsColdProcess);
        Assert.Equal(legacy.Progression!.SceneFacts!.Music!.LoadedMusic, loaded.GlobalSoundProcess!.MusicPlayback!.CurrentMusicId0);
        Assert.Equal(NativeMusicPlaybackMode.Midi, loaded.GlobalSoundProcess.MusicPlayback.Mode0);
        Assert.Equal(before, JsonSerializer.Serialize(legacy));
    }

    [Fact]
    public void Default_hidden_bar_records_accept_later_setbar_without_a_missing_owner()
    {
        GameplaySaveFieldPresentation defaults = GameplaySaveFieldPresentation.CreateDefaults();
        NativeFieldBarsState bars = defaults.RestoreBars(NativeFieldBarsState.CreateHidden()).SetValue(0, 75).SetValue(1, 42);
        Assert.False(bars.First!.Display.Visible);
        Assert.False(bars.Second!.Display.Visible);
        Assert.Equal(75, bars.First.Mirror.Value);
        Assert.Equal(42, bars.Second.Mirror.Value);
    }

    [Fact]
    public void Named_legacy_quick_save_loads_and_continues_without_rewriting_the_source()
    {
        string path = TestResources.FixturePath("App.Tests.LegacyGameplaySaveDefaultsTests.Latest");
        byte[] bytes = File.ReadAllBytes(path);
        GameplaySaveComponentDocument saved;
        using (MemoryStream stream = new(bytes))
        {
            saved = GameplaySaveCodec.Read(stream);
        }
        Assert.Null(saved.Progression!.RuntimeOwnershipVersion);
        string directory = TestResources.GameDirectory;
        using GameResourceHost selected = new(directory, watch: false);
        using GameAssets assets = new(directory, resources: selected.Resources, modelOverrides: selected);
        List<string> warnings = [];
        GameState loaded = GameplaySaveSessionLoader.Load(path, directory, sharedAssets: assets, resources: selected.Resources, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, selected), reportWarning: warnings.Add);
        Assert.Single(warnings);
        Assert.Equal(saved.Resume.FieldId, loaded.FieldId);
        Assert.Equal(saved.Economy.Gil, loaded.Economy!.Gil);
        Assert.Equal(saved.Economy.Items, loaded.Economy.Items);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(saved.Characters), JsonSerializer.SerializeToElement(GameplaySaveCharacters.Capture(loaded.CharacterVitals!))));
        Assert.Equal(saved.Progression.Transactions.AppliedBattleResults, loaded.AppliedBattleResultIds);
        Assert.Equal(saved.Progression.Clocks.Playtime, loaded.Playtime);
        Assert.Equal(saved.Progression.Clocks.CrtRandom, loaded.CrtRandom);
        Assert.Equal(saved.Progression.Clocks.GlobalRandom, loaded.GlobalRandom);
        Assert.Equal(JsonSerializer.Serialize(saved.Progression.Clocks.EventRandom), JsonSerializer.Serialize(loaded.EventRandom));
        Assert.Equal(saved.Progression.Clocks.MenuRandom, loaded.MenuRandom);
        Assert.NotNull(loaded.NativeBattleMemory!.EffectMemory!.ExecutionStack);
        GameSession session = new(loaded);
        using GameWorld world = new(session, directory, sharedAssets: assets, resources: selected.Resources, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy);
        world.EnsureField();
        Assert.Equal(saved.Progression.FieldContinuation!.Scripts.Frame, world.Events.Frame);
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_LEGACY_SAVE_RECOVERY_OUTPUT") is { Length: > 0 } output)
        {
            Assert.False(File.Exists(output));
            session.Save(output, compact: true);
            _ = GameSession.Load(output);
        }
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(File.ReadAllBytes(path)));
    }
}
