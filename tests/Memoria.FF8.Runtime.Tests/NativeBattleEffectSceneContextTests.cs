namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectSceneContextTests
{
    [Theory]
    [InlineData(0, NativeBattleSummonFloorMode.BelowTargetPlane, NativeBattleTargetBoundsMode.AllContacts)]
    [InlineData(42, NativeBattleSummonFloorMode.BelowTargetPlane, NativeBattleTargetBoundsMode.AllContacts)]
    [InlineData(63, NativeBattleSummonFloorMode.TargetPlane, NativeBattleTargetBoundsMode.AllContacts)]
    [InlineData(368, NativeBattleSummonFloorMode.BelowTargetPlane, NativeBattleTargetBoundsMode.AllContacts)]
    [InlineData(796, NativeBattleSummonFloorMode.BelowTargetPlane, NativeBattleTargetBoundsMode.FirstContact)]
    public void Native_scene_producer_preserves_independent_original_spatial_branches(ushort encounter, NativeBattleSummonFloorMode floor, NativeBattleTargetBoundsMode targets)
    {
        NativeBattleEffectSceneContext context = NativeBattleEffectSceneContext.FromNativeEncounter(encounter);
        context.Validate();
        Assert.Equal(floor, context.FloorMode);
        Assert.Equal(targets, context.TargetBoundsMode);
    }

    [Fact]
    public void Host_mirror_policy_needs_no_native_stage_and_overrides_an_unrelated_native_selector()
    {
        NativeBattleEffectSceneContext host = new(NativeBattleSummonFloorMode.BelowTargetPlane, NativeBattleTargetBoundsMode.AllContacts)
        {
            MirroredSurfaceMode = NativeBattleMirroredSurfaceMode.InitialCapture
        };
        Assert.Equal(NativeBattleMirroredSurfaceMode.InitialCapture, host.RequireMirroredSurfaceMode(null));
        Assert.Equal(NativeBattleMirroredSurfaceMode.InitialCapture, host.RequireMirroredSurfaceMode(0x2C));
    }

    [Theory]
    [InlineData(0x2B, NativeBattleMirroredSurfaceMode.InitialCapture)]
    [InlineData(0x2C, NativeBattleMirroredSurfaceMode.ContinuousCapture)]
    [InlineData(0x2D, NativeBattleMirroredSurfaceMode.ContinuousCapture)]
    [InlineData(0x2E, NativeBattleMirroredSurfaceMode.ContinuousCapture)]
    [InlineData(0x2F, NativeBattleMirroredSurfaceMode.InitialCapture)]
    public void Native_stage_preserves_original_surface_refresh_and_absent_optional_policy_recovers_from_stage(byte stage, NativeBattleMirroredSurfaceMode expected)
    {
        Assert.Equal(expected, NativeBattleEffectSceneContext.FromNativeEncounter(42, stage).RequireMirroredSurfaceMode(null));
        Assert.Equal(expected, NativeBattleEffectSceneContext.FromNativeEncounter(42).RequireMirroredSurfaceMode(stage));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectSceneContext.FromNativeEncounter(42).RequireMirroredSurfaceMode(null));
    }

    [Fact]
    public void Host_spatial_traits_reject_unsupported_policies()
    {
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectSceneContext((NativeBattleSummonFloorMode)2, NativeBattleTargetBoundsMode.AllContacts).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectSceneContext(NativeBattleSummonFloorMode.TargetPlane, (NativeBattleTargetBoundsMode)2).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectSceneContext(NativeBattleSummonFloorMode.TargetPlane, NativeBattleTargetBoundsMode.AllContacts) { MirroredSurfaceMode = (NativeBattleMirroredSurfaceMode)2 }.Validate());
    }
}
