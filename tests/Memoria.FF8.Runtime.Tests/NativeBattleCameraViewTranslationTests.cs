using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraViewTranslationTests
{
    [Theory]
    [InlineData(32760, 32, -32744)]
    [InlineData(-32760, -32, 32744)]
    [InlineData(-1, 32, 31)]
    public void Additive_Blizzard_producer_reads_only_Y_and_wraps_its_WORD_without_changing_the_prior_view(short priorY, short delta, short expectedY)
    {
        NativeBattleCameraState before = NativeBattleCameraState.FromColdProcess() with { ViewTranslationScratch = new(null, priorY, null), Scratch = [10, 20, 30, 40, 50, 60, 70, 80] };
        NativeBattleCameraState after = before.AddViewTranslationY61B8D5(delta);
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(null, expectedY, null), after.ViewTranslationScratch);
        Assert.Same(before.Projection, after.Projection);
        Assert.Same(before.CurrentPose, after.CurrentPose);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after with { ViewTranslationScratch = before.ViewTranslationScratch }));
        Assert.Equal(priorY, before.ViewTranslationScratch!.Y);
    }

    [Fact]
    public void Missing_prior_Y_is_strict_even_when_the_other_two_axes_are_known()
    {
        NativeBattleCameraState before = NativeBattleCameraState.FromColdProcess() with { ViewTranslationScratch = new(0, null, 0) };
        Assert.Throws<NotSupportedException>(() => before.AddViewTranslationY61B8D5(32));
        Assert.Null(before.ViewTranslationScratch!.Y);
        Assert.Throws<NotSupportedException>(() => (before with { ViewTranslationScratch = null }).AddViewTranslationY61B8D5(32));
    }

    [Fact]
    public void Tail_sign_extends_all_three_WORDs_and_wraps_DWORD_addition_without_Q12_or_clamping()
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], int.MaxValue, int.MinValue, -1, 4096);
        NativeBattleCameraViewTranslationScratch scratch = new(32767, -32768, -1);
        NativeBattleActionMatrix after = scratch.Apply5033E0(view);
        Assert.Equal(int.MinValue + 32766, after.X);
        Assert.Equal(int.MaxValue - 32767, after.Y);
        Assert.Equal(-2, after.Z);
        Assert.Equal(view.Rotation, after.Rotation);
        Assert.Equal(view.Scale, after.Scale);
        Assert.Equal(int.MaxValue, view.X);
    }

    [Theory]
    [InlineData((short)0, 0u)]
    [InlineData((short)0, 1u)]
    [InlineData((short)0, 0x201u)]
    [InlineData((short)0, 4u)]
    [InlineData((short)700, 0u)]
    [InlineData((short)700, 0x201u)]
    [InlineData((short)700, 4u)]
    public void Roll_and_nonroll_publication_consume_once_after_the_fresh_view_even_under_pause(short roll, uint flags)
    {
        NativeBattleCameraPose pose = new(new(1000, -2000, 4000), new(-200, -300, 100), 300, roll);
        NativeBattleCameraState before = NativeBattleCameraState.FromColdProcess().Initialize500400().WriteEffectGlobals(pose) with { ViewTranslationScratch = new(-9, 32, -32768) };
        NativeBattleCameraProjection prior = before.Projection!;
        NativeBattleActionMatrix baseline = NativeBattlePoseMatrices.CameraView(pose);
        NativeBattleCameraState published = before.PublishProjection(flags);
        NativeBattleActionMatrix view = published.Projection!.View!;
        Assert.Equal(unchecked(baseline.X - 9), view.X);
        Assert.Equal(unchecked(baseline.Y + 32), view.Y);
        Assert.Equal(unchecked(baseline.Z - 32768), view.Z);
        Assert.Equal(baseline.Rotation.ToArray(), view.Rotation.ToArray());
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 0, 0), published.ViewTranslationScratch);
        Assert.Same(pose, published.CurrentPose);
        Assert.Equal((flags & 4) != 0 ? (ushort)300 : prior.FocalLength, published.Projection.FocalLength);
        Assert.Equal((flags & 4) != 0 ? (short)160 : prior.OffsetX, published.Projection.OffsetX);
        Assert.Equal((flags & 4) != 0 ? (short)108 : prior.OffsetY, published.Projection.OffsetY);
        Assert.Equal((short?)32, before.ViewTranslationScratch!.Y);
        Assert.Same(prior, before.Projection);
        NativeBattleCameraState second = published.PublishProjection(flags);
        Assert.Equal(baseline.X, second.Projection!.View!.X);
        Assert.Equal(baseline.Y, second.Projection.View.Y);
        Assert.Equal(baseline.Z, second.Projection.View.Z);
    }

    [Fact]
    public void Effects_tasks_and_500400_preserve_pending_scratch_until_the_common_tail()
    {
        NativeBattleCameraState before = NativeBattleCameraState.FromColdProcess() with { ViewTranslationScratch = new(10, -20, 30) };
        NativeBattleCameraState initialized = before.Initialize500400();
        Assert.Same(before.ViewTranslationScratch, initialized.ViewTranslationScratch);
        NativeBattleCameraPose pose = new(new(0, -3000, -1000), new(0, -500, 0), 240, 0);
        NativeBattleCameraState effect = initialized.WriteEffectGlobals(pose);
        Assert.Same(before.ViewTranslationScratch, effect.ViewTranslationScratch);
        Assert.Same(before.ViewTranslationScratch, effect.Tick(0x101).ViewTranslationScratch);
        NativeBattleCameraState started = effect.Start(new([1, 0, 4, 0, 0, 0]), 0);
        Assert.Single(started.Order);
        Assert.Same(before.ViewTranslationScratch, started.ViewTranslationScratch);
    }

    [Fact]
    public void Actual_5041E0_reset_clears_known_or_unknown_scratch_but_keeps_the_previous_projection()
    {
        NativeBattleCameraState before = NativeBattleCameraState.FromColdProcess() with { ViewTranslationScratch = new(100, null, -100), Scratch = [1, 2, 3, 4, 5, 6, 7, 8] };
        NativeBattleCameraState reset = NativeBattleCameraState.Initialize5041E0(before);
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 0, 0), reset.ViewTranslationScratch);
        Assert.Same(before.Projection, reset.Projection);
        Assert.Equal(before.CurrentPose!.Eye, reset.CurrentPose!.Eye);
        Assert.Equal(before.CurrentPose.Target, reset.CurrentPose.Target);
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 0, 0), NativeBattleCameraState.Initialize5041E0(null).ViewTranslationScratch);
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 0, 0), NativeBattleCameraState.FromColdProcess().ViewTranslationScratch);
        Assert.Null(NativeBattleCameraState.Initialize().ViewTranslationScratch);
    }

    [Fact]
    public void Old_JSON_does_not_gain_scratch_from_known_pose_projection_or_executable_defaults()
    {
        NativeBattleCameraState old = NativeBattleCameraState.FromColdProcess() with { ViewTranslationScratch = null };
        string json = JsonSerializer.Serialize(old);
        Assert.DoesNotContain("ViewTranslationScratch", json);
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(json)!;
        Assert.Null(restored.ViewTranslationScratch);
        Assert.Throws<NotSupportedException>(() => restored.PublishProjection(0));
        Assert.Throws<NotSupportedException>(() => restored.AddViewTranslationY61B8D5(32));
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 0, 0), NativeBattleCameraState.Initialize5041E0(restored).ViewTranslationScratch);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("Y")]
    [InlineData("Z")]
    public void Publication_requires_each_axis_and_missing_JSON_members_do_not_become_zero(string axis)
    {
        NativeBattleCameraViewTranslationScratch scratch = axis switch
        {
            "X" => new(null, 0, 0),
            "Y" => new(0, null, 0),
            _ => new(0, 0, null)
        };
        NativeBattleCameraState before = NativeBattleCameraState.FromColdProcess() with { ViewTranslationScratch = scratch };
        Assert.Throws<NotSupportedException>(() => before.PublishProjection(1));
        Assert.Same(scratch, before.ViewTranslationScratch);
        JsonObject node = JsonSerializer.SerializeToNode(new NativeBattleCameraViewTranslationScratch(0, 0, 0))!.AsObject();
        Assert.True(node.Remove(axis));
        Assert.Throws<JsonException>(() => node.Deserialize<NativeBattleCameraViewTranslationScratch>());
    }

    [Fact]
    public void A_prior_published_view_is_not_a_fresh_base_for_nonzero_impulses()
    {
        NativeBattleCameraState before = NativeBattleCameraState.FromColdProcess() with { CurrentPose = null, ViewTranslationScratch = new(0, 32, 0) };
        Assert.Contains("fresh base", Assert.Throws<NotSupportedException>(() => before.PublishProjection(0)).Message);
        Assert.Equal((short?)32, before.ViewTranslationScratch!.Y);
        NativeBattleCameraState knownZero = before with { ViewTranslationScratch = new(0, 0, 0) };
        Assert.Equal(knownZero.Projection!.View, knownZero.PublishProjection(0).Projection!.View);
        NativeBattleCameraState unknownView = NativeBattleCameraState.Initialize5041E0(null).Initialize500400();
        NativeBattleCameraState result = unknownView.PublishProjection(0);
        Assert.Null(result.CurrentPose);
        Assert.Null(result.Projection!.View);
        Assert.Equal((ushort?)216, result.Projection.FocalLength);
    }

    [Fact]
    public void Mid_impulse_JSON_continuation_preserves_the_same_single_publication_and_clear()
    {
        NativeBattleCameraState before = NativeBattleCameraState.FromColdProcess().Initialize500400() with { ViewTranslationScratch = new(-123, 5, 321) };
        before = before.AddViewTranslationY61B8D5(32).AddViewTranslationY61B8D5(32);
        Assert.Equal((short?)69, before.ViewTranslationScratch!.Y);
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(before))!;
        NativeBattleCameraState originalNext = before.PublishProjection(0x201);
        NativeBattleCameraState restoredNext = restored.PublishProjection(0x201);
        Assert.Equal(JsonSerializer.Serialize(originalNext), JsonSerializer.Serialize(restoredNext));
        Assert.Equal(new NativeBattleCameraViewTranslationScratch(0, 0, 0), restoredNext.ViewTranslationScratch);
        Assert.Equal(JsonSerializer.Serialize(originalNext.PublishProjection(4)), JsonSerializer.Serialize(restoredNext.PublishProjection(4)));
    }
}
