namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematic30CaptureTests
{
    [Theory]
    [InlineData((ushort)248)]
    [InlineData((ushort)250)]
    [InlineData((ushort)253)]
    [InlineData((ushort)254)]
    [InlineData((ushort)255)]
    [InlineData((ushort)261)]
    [InlineData((ushort)262)]
    [InlineData((ushort)263)]
    [InlineData((ushort)264)]
    public void Selected_model_uses_actual_geometry_resource_with_detached_native_parity(ushort effect)
    {
        new NativeBattleCinematicModelTests().Selected_model_resource_capture_preserves_actual_geometry_pointer_and_all_native_output(effect);
    }

    [Theory]
    [InlineData((ushort)248)]
    [InlineData((ushort)250)]
    [InlineData((ushort)253)]
    [InlineData((ushort)254)]
    [InlineData((ushort)255)]
    [InlineData((ushort)261)]
    [InlineData((ushort)262)]
    [InlineData((ushort)263)]
    [InlineData((ushort)264)]
    public void Selected_particle_resource_replacement_keeps_actual_birth_and_detached_native_output(ushort effect)
    {
        new NativeBattleWaterParticleRectangleTests().Selected_resource_changes_at_one_particle_birth_preserve_detached_native_state_and_previous_geometry(effect);
    }

    [Theory]
    [InlineData((ushort)248)]
    [InlineData((ushort)250)]
    [InlineData((ushort)253)]
    [InlineData((ushort)254)]
    [InlineData((ushort)255)]
    [InlineData((ushort)261)]
    [InlineData((ushort)262)]
    [InlineData((ushort)263)]
    [InlineData((ushort)264)]
    public void Selected_resource_gate_changes_only_metadata_from_the_already_selected_pointer(ushort effect)
    {
        NativeBattlePilotVisualIdentity identity = new(effect, 17, 2, 3, 4) { ResourceGeneration = 55 };
        NativeBattlePilotVisualIdentity selected = NativeBattleCinematicVisualIdentity.SelectedResource(identity, 0x1234)!.Value;
        Assert.True(NativeBattleCinematic30Presentation.Owns(effect));
        Assert.Equal(identity with { ResourceGeneration = unchecked(55L * 397 ^ 0x1234u) }, selected);
        Assert.Null(NativeBattleCinematicVisualIdentity.SelectedResource(null, 0x1234));
    }

    [Theory]
    [InlineData((ushort)248)]
    [InlineData((ushort)250)]
    [InlineData((ushort)253)]
    [InlineData((ushort)254)]
    [InlineData((ushort)255)]
    [InlineData((ushort)261)]
    [InlineData((ushort)262)]
    [InlineData((ushort)263)]
    [InlineData((ushort)264)]
    public void Authored_fixed_overlay_keeps_discrete_screen_policy_native_stores_and_zero_depth(ushort effect)
    {
        new NativeBattleCinematicOverlayTests().Selected_fixed_overlay_keeps_native_discrete_screen_operation_and_zero_depth(effect);
    }

    [Theory]
    [InlineData((ushort)18)]
    [InlineData((ushort)77)]
    [InlineData((ushort)208)]
    [InlineData((ushort)211)]
    [InlineData((ushort)249)]
    [InlineData((ushort)259)]
    public void Other_resource_policies_and_ordinary_Renz_defaults_remain_unchanged(ushort effect)
    {
        NativeBattlePilotVisualIdentity identity = new(effect, 17, 2, 3, 4) { ResourceGeneration = 55 };
        Assert.False(NativeBattleCinematic30Presentation.Owns(effect));
        Assert.Equal(identity, NativeBattleCinematicVisualIdentity.SelectedResource(identity, 0x1234));
    }
}
