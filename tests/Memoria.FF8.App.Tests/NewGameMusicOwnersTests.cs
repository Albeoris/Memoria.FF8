using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NewGameMusicOwnersTests
{
    [Fact]
    public void Fresh_New_Game_initializes_music_without_allocating_a_field_program()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine engine = new(session);

        NativeMusicProcessState music = engine.InitializeNewGameMusicOwners(NativeFieldRuntimeProfile.FF8En);

        Assert.Same(music, engine.Scene.MusicProcess);
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, music.Bootstrap!.ExecutableHash);
        Assert.Null(music.Buffer0);
        Assert.Null(music.Buffer1);
        Assert.Null(music.Pending);
        Assert.NotNull(engine.Scene.MusicChannel108);
        Assert.NotNull(engine.Scene.MusicChannel112);
        Assert.NotNull(engine.Scene.NativeBattleMusic203);
        Assert.Empty(engine.Actors);
        Assert.Null(engine.NativeRuntimeProfile);
        Assert.Null(session.State.EventState);
    }

    [Fact]
    public void Repeated_initialization_preserves_existing_music_owners()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine engine = new(session);
        NativeMusicProcessState music = engine.InitializeNewGameMusicOwners(NativeFieldRuntimeProfile.FF8En);
        NativeMusicChannelState primary = engine.Scene.MusicChannel108!;
        NativeMusicChannelState secondary = engine.Scene.MusicChannel112!;

        Assert.Throws<InvalidOperationException>(() => engine.InitializeNewGameMusicOwners(NativeFieldRuntimeProfile.FF8En));

        Assert.Same(music, engine.Scene.MusicProcess);
        Assert.Same(primary, engine.Scene.MusicChannel108);
        Assert.Same(secondary, engine.Scene.MusicChannel112);
    }

    [Fact]
    public void Cold_Title_state_cannot_initialize_New_Game_music()
    {
        EventEngine engine = new(GameSession.CreateColdProcess());

        Assert.Throws<InvalidOperationException>(() => engine.InitializeNewGameMusicOwners(NativeFieldRuntimeProfile.FF8En));

        Assert.Null(engine.Scene.MusicProcess);
        Assert.Null(engine.Scene.MusicChannel108);
    }

    [Fact]
    public void Unverified_profile_cannot_publish_music_owners()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine engine = new(session);
        NativeFieldRuntimeProfile unverified = NativeFieldRuntimeProfile.FF8En with { Version = 0 };

        Assert.Throws<InvalidOperationException>(() => engine.InitializeNewGameMusicOwners(unverified));

        Assert.Null(engine.Scene.MusicProcess);
        Assert.Null(engine.Scene.MusicChannel108);
    }
}
