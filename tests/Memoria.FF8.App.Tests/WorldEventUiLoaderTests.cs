using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldEventUiLoaderTests
{
    [Theory]
    [InlineData(0, 0, 17, 0)]
    [InlineData(63, 33, 40, 128)]
    public void FreshOrdinaryEntryPublishesItsOriginalClosedModeZeroUi(int entry, int entity, int story, int eventFlags)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameAssets assets = new(directory);
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        // Declared component inputs exercise the genuine fresh loader, without reconstructing a gameplay route.
        session.SetWorldEntryMemory(session.WorldEntryMemory!.WriteBytes(256, 2, story).WriteBytes(265, 1, eventFlags));
        GameState before = session.State;
        using PreparedWorldEntry prepared = WorldEntryPreflight.Prepare(before, directory, new WorldTransition((short)entry, (short)entity, -1), validatePresentation: false);
        Assert.Same(before, session.State);
        Assert.NotNull(prepared.InitialState.EventUi);
        Assert.Equal((byte)0, prepared.InitialState.EventUi!.ScriptMode);
        Assert.False(prepared.InitialState.EventUi.CloseSuppressedThisCallback);
        Assert.Empty(prepared.InitialState.EventUi.Windows);
        Assert.Equal(0, prepared.InitialState.Frame);
        if (entity == 33)
        {
            Assert.NotNull(prepared.InitialState.NativeCarRuntime);
            Assert.Null(prepared.InitialState.NativeFootRuntime);
        }
        else
        {
            Assert.NotNull(prepared.InitialState.NativeFootRuntime);
            Assert.Null(prepared.InitialState.NativeCarRuntime);
        }
    }

    [Fact]
    public void RestoringAnOmittedHistoricalWorldUiDoesNotCreateAnOwner()
    {
        string directory = TestResources.GameDirectory;
        GameState supplied = GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1"));
        GameSession session = new(supplied with { World = supplied.World! with { EventUi = null } });
        GameState before = session.State;
        using WorldMapScene scene = new(session, directory);
        Assert.Same(before, session.State);
        Assert.Null(scene.State.EventUi);
        Assert.Throws<NotSupportedException>(() => NativeWorldSalaryState.Begin(session.State).WithWorldScriptMode(0));
        Assert.Same(before, session.State);
    }
}
