using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class GlobalSoundCheckpointTests
{
    [Fact]
    public void LoopTimerRetainsNativeUnsignedArithmeticAndIgnoresChannels()
    {
        GlobalAudioWaveFormat wave = new(2, 2, 22050, 22310, 1024, 4, 0, []);
        GlobalAudioSampleDescriptor sample = new(69, 0, 0, 0, 1, 47844, 130624, wave);
        Assert.Equal(1087, GameWorld.CreateMutedGlobalSoundResource(sample)!.LoopPolicy.LoopTransitionDelayMilliseconds);
        Assert.Null(GameWorld.CreateMutedGlobalSoundResource(sample with { WaveFormat = wave with { SampleRate = 1 } }));
        Assert.Null(GameWorld.CreateMutedGlobalSoundResource(sample with { WaveFormat = wave with { Tag = 1, BitsPerSample = 8 } }));
        Assert.Null(GameWorld.CreateMutedGlobalSoundResource(sample with { LoopEndPcmByte = 130622 }));
        GlobalSoundResource wrapped = GameWorld.CreateMutedGlobalSoundResource(sample with { LoopStartPcmByte = 0x20000000, LoopEndPcmByte = 0x20000004 })!;
        Assert.Equal(0, wrapped.LoopPolicy.LoopTransitionDelayMilliseconds);
    }

    [Fact]
    public void GenuineNewGameFieldSaveLoadsStringEnumProcessOwnersAndReproducesNextCallback()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed New Game field assets are required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld first = new(session, directory);
        first.EnsureField();
        session.SetEventState(first.Events.Capture());
        Assert.Equal(74, first.Events.FieldId);
        Assert.NotNull(session.State.CharacterVitals);
        Assert.True(session.State.ReplayJournal!.IsComplete);
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-newgame-presentation-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("Bufferless", saved.RootElement.GetProperty("GlobalSoundProcess").GetProperty("Backend").GetString());
            Assert.Equal("Bufferless", saved.RootElement.GetProperty("EventState").GetProperty("GlobalSoundProcess").GetProperty("Backend").GetString());
            GameSession resumedSession = new(GameSession.Load(path));
            using GameWorld resumed = new(resumedSession, directory);
            resumed.EnsureField();
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(resumedSession.State)));
            Assert.True(JsonElement.DeepEquals(first.Events.Capture(), resumed.Events.Capture()));
            PlayerAction input = new(PlayerActionKind.Wait);
            Assert.Equal(first.Apply(input), resumed.Apply(input));
            Assert.Null(first.Events.LastFault);
            Assert.Null(resumed.Events.LastFault);
            Assert.True(JsonElement.DeepEquals(first.Events.Capture(), resumed.Events.Capture()));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(resumedSession.State)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReachedAmbientRequestUsesOriginalGlobalDescriptorAndRestoresItsMutedLoopPolicy()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field and global audio corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var first = new GameWorld(session, directory);
        first.EnsureField();
        Assert.Empty(first.Events.Scene.GlobalSoundRequests);
        Assert.True(first.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(first.Events.LastFault);
        GlobalSoundRequest request = Assert.Single(first.Events.Scene.GlobalSoundRequests);
        Assert.Equal(69u, request.OriginalEffectId);
        Assert.Equal(1u, request.ChannelMask);
        Assert.Equal(1, request.Voice);
        Assert.Equal(127u, request.RawPan);
        Assert.Equal(63u, request.EffectivePan);
        Assert.Equal(127u, request.Volume);
        Assert.Equal(100, request.PitchPercent);
        Assert.Equal(69, request.Resource.SampleIndex);
        Assert.Equal(GlobalSoundBackendStatus.Muted, request.Resource.BackendStatus);
        Assert.Equal(new GlobalSoundLoopPolicy(1, 47844, 130622, 22050, 16, 1, 1087), request.Resource.LoopPolicy);
        Assert.Equal(request, first.Events.Scene.GlobalSoundLatestAttempts[1]);
        session.SetEventState(first.Events.Capture());
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-global-sound-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            var resumedSession = new GameSession(GameSession.Load(path));
            using var resumed = new GameWorld(resumedSession, directory);
            resumed.EnsureField();
            Assert.True(JsonElement.DeepEquals(first.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(request, Assert.Single(resumed.Events.Scene.GlobalSoundRequests));
            Assert.Equal(request, resumed.Events.Scene.GlobalSoundLatestAttempts[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
