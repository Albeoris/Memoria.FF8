using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMovementProgressWorldTests
{
    [Fact]
    public void InstalledFootMovementPublishesRecoveryAndTrainingThenColdContinues()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        using GameAssets assets = new(directory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        NativeLimitProgress limits = new(session.CharacterVitals!.InitOutSha256, 0, 0, 0, 3, ImmutableArray.CreateRange(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }));
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0), LimitProgress = limits, EventMemory = session.EventMemory.Write(12, 4, NativeScalarValue.Exact(7)).Write(244, 4, NativeScalarValue.Exact(591)), DepartingFieldState = JsonSerializer.SerializeToElement(new { Scene = new { Party = new[] { 4, 255, 255 } }, EventMemory = session.EventMemory }) });
        using WorldMapScene scene = new(session, directory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        session.SetWorldState(scene.State with { CameraYaw = 0, BodyYaw = 1024 });
        Assert.True(scene.Apply(new(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        Assert.Equal(3, session.EventMemory.Read(4, 4).RequireExact());
        Assert.Equal(0, session.EventMemory.Read(12, 4).RequireExact());
        Assert.Equal(0, session.EventMemory.Read(244, 4).RequireExact());
        Assert.Equal(0, session.State.LimitProgress!.TrainingCounters[3]);
        Assert.Equal(0, session.State.LimitProgress.AngeloFlags);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene resumed = new(restored, directory);
        PlayerAction next = new(PlayerActionKind.Move, Horizontal: 1, Frames: 3);
        Assert.Equal(scene.Apply(next), resumed.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restored.State));
        session.SetEventMemory(session.EventMemory.Write(244, 4, NativeScalarValue.Exact(592)));
        GameState beforeLearning = session.State;
        Assert.Throws<NotSupportedException>(() => scene.Apply(new(PlayerActionKind.Wait)));
        Assert.Same(beforeLearning, session.State);
        EventEngine output = new(session)
        {
            ResolveGlobalSoundResource = sample =>
            {
                GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(directory, sample);
                return new(sample, GlobalSoundBackendStatus.Muted, new(descriptor.PlaybackFlags, descriptor.LoopStartPcmByte, descriptor.LoopEndPcmByte, descriptor.WaveFormat.SampleRate, 16, descriptor.WaveFormat.Channels, null));
            }
        };
        scene.PrepareGlobalSound = output.PrepareGlobalSound;
        List<GlobalSoundRequest> requests = [];
        scene.PublishSalaryEffects = effects => requests.AddRange(effects.Requests);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(8, session.State.LimitProgress!.AngeloFlags);
        Assert.Equal(0, session.EventMemory.Read(244, 4).RequireExact());
        GlobalSoundRequest learned = Assert.Single(requests);
        Assert.Equal(131u, learned.OriginalEffectId);
        Assert.Equal(0u, learned.RawChannelMask);
        Assert.Equal(128u, learned.RawPan);
        Assert.Equal(127u, learned.Volume);
    }
}
