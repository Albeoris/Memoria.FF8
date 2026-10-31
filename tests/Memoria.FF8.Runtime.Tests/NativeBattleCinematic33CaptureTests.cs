namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematic33CaptureTests
{
    [Fact]
    public void Native331_depths_preserve_world_identity_physical_payload_and_expose_uncaptured_geometry()
    {
        NativeBattlePilotVisualGeometry world = new(new(331, 1, 2, 3, 4) { ResourceGeneration = 99 }, new(10, 20, 30), new(40, 50, 60), new(70, 80, 90), default, 3);
        NativeBattleGeometryPrimitive primitive = new(17, [0x01000000, 0xE1000000], [101, 202, 303, 0]) { PilotGeometry = world };
        NativeBattleGfGeometryBatch batch = new([new(0, [0xA5], [NativeBattleGfPacketByteState.Unknown], primitive), new(8, [0x5A], [NativeBattleGfPacketByteState.Known], primitive with { PilotGeometry = null })], 16);
        string expected = System.Text.Json.JsonSerializer.Serialize(batch);
        var captured = NativeBattleCinematic33Presentation.Depths(batch);
        Assert.Equal(expected, System.Text.Json.JsonSerializer.Serialize(captured));
        var actual = captured.Attempts[0].Primitive!.PilotGeometry!;
        Assert.Equal(world.Identity, actual.Identity);
        Assert.Equal(world.A, actual.A);
        Assert.False(actual.ScreenSpace);
        Assert.Equal(new System.Numerics.Vector4(101, 202, 303, 0), actual.SourceDepths!.Value);
        Assert.True(actual.UseSourceDepths);
        Assert.Null(captured.Attempts[1].Primitive!.PilotGeometry);
    }

    [Fact]
    public void Actual331_model_resource_retains_detached_native_output_and_selected_source()
    {
        new NativeBattleCinematicModelTests().Selected_model_resource_capture_preserves_actual_geometry_pointer_and_all_native_output(331);
    }

    [Fact]
    public void Actual331_particle_resource_change_at_same_birth_retains_native_output()
    {
        new NativeBattleWaterParticleRectangleTests().Selected_resource_changes_at_one_particle_birth_preserve_detached_native_state_and_previous_geometry(331);
    }

    [Fact]
    public void Authored331_fixed_overlay_preserves_discrete_policy_and_native_stores()
    {
        new NativeBattleCinematicOverlayTests().Selected_fixed_overlay_keeps_native_discrete_screen_operation_and_zero_depth(331);
    }
}
