using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDrawStock15ResourcesTests
{
    [Fact]
    public void Original_DrawStock_aggregate_has_its_camera_independent_node_bank_and_both_particle_sources()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the Draw Stock module.");
        using GameAssets game = new(directory!);
        NativeBattleDrawStock15Resources resources = game.ReadNativeDrawStock15Resources();
        resources.Validate();
        Assert.Equal(0x7CC, resources.Animation.Data.Length);
        Assert.Equal(0x17FC68Cu, resources.Executable.ResourceAddress);
        Assert.Equal(0x5944, resources.Executable.Data.Length);
        Assert.Equal((ushort)15, resources.GenericNodes().Effect);
        Assert.Same(resources.Executable, resources.GenericNodes().Executable);
        Assert.Equal(528, resources.Particles.Trajectory.Length);
        Assert.Equal(288, resources.Particles.Sparkle.Length);
        Assert.Same(resources, game.ReadNativeDrawStock15Resources());
    }
}
