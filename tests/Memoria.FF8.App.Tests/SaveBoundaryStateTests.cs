using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SaveBoundaryStateTests
{
    private static SaveBoundaryState Field => new() { Scene = SceneKind.Field, WorldLoaded = true, FieldReady = true };

    [Fact]
    public void Field_control_does_not_override_pending_gameplay_or_transactions()
    {
        Assert.Equal(SaveBoundaryStatus.Ready, Field.Assess());
        Assert.Equal(SaveBoundaryStatus.BattleOrCardPending, (Field with { BattleOrCardPending = true }).Assess());
        Assert.Equal(SaveBoundaryStatus.InteractionPending, (Field with { InteractionPending = true }).Assess());
        Assert.Equal(SaveBoundaryStatus.ScriptedMovementActive, (Field with { ScriptedMovementActive = true }).Assess());
        Assert.Equal(SaveBoundaryStatus.FieldNotReady, (Field with { FieldReady = false }).Assess());
    }

    [Fact]
    public void World_requires_its_own_readiness_instead_of_dormant_field_control()
    {
        SaveBoundaryState world = Field with { Scene = SceneKind.WorldMap };
        Assert.Equal(SaveBoundaryStatus.WorldNotReady, world.Assess());
        Assert.Equal(SaveBoundaryStatus.Ready, (world with { WorldReady = true, FieldReady = false, ScriptedMovementActive = true }).Assess());
        Assert.Equal(SaveBoundaryStatus.BattleOrCardPending, (world with { WorldReady = true, BattleOrCardPending = true }).Assess());
    }

    [Fact]
    public void Fault_and_startup_precede_scene_specific_readiness()
    {
        Assert.Equal(SaveBoundaryStatus.Faulted, (Field with { Faulted = true, StartupActive = true }).Assess());
        Assert.Equal(SaveBoundaryStatus.NotPlaying, (Field with { StartupActive = true }).Assess());
        Assert.Equal(SaveBoundaryStatus.NotPlaying, (Field with { WorldLoaded = false }).Assess());
        Assert.Equal(SaveBoundaryStatus.NotPlaying, (Field with { Scene = SceneKind.Movie }).Assess());
        Assert.Equal(SaveBoundaryStatus.NotPlaying, (Field with { Scene = SceneKind.Title }).Assess());
        Assert.Equal(SaveBoundaryStatus.InterfaceOpen, (Field with { InterfaceOpen = true, AutomaticPlayActive = true }).Assess());
        Assert.Equal(SaveBoundaryStatus.AutomaticPlayActive, (Field with { AutomaticPlayActive = true }).Assess());
    }
}
