using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFieldEncounterControlTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(255)]
    public void OrdinaryMovementUsesTheSharedNativeControlByteAndPreservesItAcrossSnapshotRestore(byte keep)
    {
        string source = TestResources.FixturePath("field-encounter-entry");
        string game = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game, validateWorldPresentation: false);
        world.EnsureField();
        Assert.True(session.State.FullBattlePlaythrough);
        Assert.True(session.State.RandomEncounters!.Enabled);
        Assert.Null(session.State.RandomEncounters.Pending);
        Assert.True(world.Field!.RandomEncounters!.Rate > 0);
        Assert.Equal(0, session.State.CharacterVitals!.GetEncounterAbilityFlags() & 8);
        NativeWorldControlState controls = session.State.WorldControls! with { MusicKeep207 = keep };
        session.SetWorldControls(controls);
        session.SetEventState(world.Events.Capture());
        string snapshot = TestResources.ArtifactFile("field-encounter-" + keep + ".json");
        session.Save(snapshot);
        GameSession restoredSession = new(GameSession.Load(snapshot));
        using GameWorld restored = new(restoredSession, game, validateWorldPresentation: false);
        restored.EnsureField();
        Assert.Equal(controls, restoredSession.State.WorldControls);
        NativeRandomEncounterState initial = session.State.RandomEncounters.Native;
        (float X, float Y) start = (world.Events.PlayerActor!.X, world.Events.PlayerActor.Y);
        bool moved = false;
        for (int index = 0; index < 32 && !world.HasPendingBattle; index++)
        {
            float direction = index / 8 % 2 == 0 ? 1 : -1;
            PlayerAction action = new(PlayerActionKind.Move, Vertical: direction);
            world.Apply(action);
            restored.Apply(action);
            Assert.Equal(session.State.RandomEncounters, restoredSession.State.RandomEncounters);
            Assert.Null(world.Events.LastFault);
            Assert.Equal(session.State.FieldId, restoredSession.State.FieldId);
            moved |= start != (world.Events.PlayerActor.X, world.Events.PlayerActor.Y);
        }
        Assert.True(moved, "The supplied fixture must admit ordinary player movement.");
        if (keep == 0)
        {
            Assert.NotEqual(initial, session.State.RandomEncounters.Native);
        }
        else
        {
            Assert.Equal(initial, session.State.RandomEncounters.Native);
            Assert.False(world.HasPendingBattle);
        }
    }
}
