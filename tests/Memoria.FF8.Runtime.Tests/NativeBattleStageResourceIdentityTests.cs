using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageResourceIdentityTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(null)]
    public void Initialization_publishes_only_decoded_resource_identity(int? resourceStageId)
    {
        NativeBattleStageState prior = NativeBattleStageState.FromColdProcess() with { ResourceStageId = 142 };
        NativeBattleStageState stage = prior.Initialize500EA0(Source(resourceStageId));
        Assert.Equal(resourceStageId, stage.ResourceStageId);
        NativeBattleStageState restored = JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(stage))!;
        Assert.Equal(resourceStageId, restored.ResourceStageId);
    }

    [Theory]
    [InlineData(0, 37)]
    [InlineData(1, 144)]
    [InlineData(0, null)]
    public void Reload_uses_decoded_identity_instead_of_initial_stage_or_file_selector(int index, int? resourceStageId)
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess().Initialize500EA0(Source(142)) with { SourceAddress1D9A820 = 0x193A998 };
        NativeBattleStageState reloaded = stage.Reload512AC0(index, Source(resourceStageId));
        Assert.Equal(resourceStageId, reloaded.ResourceStageId);
        Assert.Equal(142, stage.ResourceStageId);
    }

    [Theory]
    [InlineData(137)]
    [InlineData(null)]
    public void Variant_rebuild_replaces_resource_identity_and_preserves_unknown_as_unknown(int? resourceStageId)
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess().Initialize500EA0(Source(142));
        NativeBattleStageState rebuilt = stage.Rebuild50E3C0(Source(resourceStageId), new(1, 2), 4, () => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        Assert.Equal(resourceStageId, rebuilt.ResourceStageId);
    }

    [Fact]
    public void Source_changes_restart_animation_at_the_supplied_callback_and_reject_negative_epochs()
    {
        NativeBattleStageState initial = NativeBattleStageState.FromColdProcess().Initialize500EA0(Source(142), 120) with { SourceAddress1D9A820 = 0x193A998 };
        Assert.Equal(120L, initial.TextureAnimationStartCallback);
        NativeBattleStageState reloaded = initial.Reload512AC0(0, Source(37), 180);
        Assert.Equal(180L, reloaded.TextureAnimationStartCallback);
        NativeBattleStageState rebuilt = reloaded.Rebuild50E3C0(Source(137), new(1, 2), 4, () => throw new InvalidOperationException(), () => throw new InvalidOperationException(), 240);
        Assert.Equal(240L, rebuilt.TextureAnimationStartCallback);
        Assert.Null(rebuilt.Initialize500EA0(Source(null)).TextureAnimationStartCallback);
        Assert.Throws<InvalidDataException>(() => initial.Initialize500EA0(Source(142), -1));
        Assert.Throws<InvalidDataException>(() => initial.Reload512AC0(0, Source(37), -1));
        NativeBattleStageState restored = JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(rebuilt))!;
        Assert.Equal(137, restored.ResourceStageId);
        Assert.Equal(240L, restored.TextureAnimationStartCallback);
    }

    private static NativeBattleStageSource Source(int? resourceStageId)
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] animations = new byte[17];
        BinaryPrimitives.WriteInt32LittleEndian(animations, 1);
        BinaryPrimitives.WriteInt32LittleEndian(animations.AsSpan(4), 8);
        animations[8] = 3;
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), animations.ToImmutableArray());
        return new(8, [pose, pose, pose, pose], [], new("stage:TIM", [])) { ResourceStageId = resourceStageId, Geometry = [], ModelColors = new(1, 2) };
    }
}
