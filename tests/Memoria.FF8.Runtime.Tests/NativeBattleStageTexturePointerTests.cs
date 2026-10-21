using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageTexturePointerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initialize500EA0_publishes_null_for_empty_animation_section_and_retains_nonempty_bytes(bool populated)
    {
        ImmutableArray<byte> animation = populated ? [1, 2, 3, 4] : [];
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess().Initialize500EA0(Source(animation));
        if (populated)
        {
            Assert.Equal(animation.ToArray(), stage.TextureAnimations!.Value.ToArray());
        }
        else
        {
            Assert.Null(stage.TextureAnimations);
        }
        NativeBattleStageState restored = JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(stage))!;
        restored.Validate();
        Assert.Equal(stage.TextureAnimations is null, restored.TextureAnimations is null);
        if (populated)
        {
            Assert.Equal(animation.ToArray(), restored.TextureAnimations!.Value.ToArray());
        }
    }

    [Fact]
    public void Specialized_callback_with_null_generic_texture_policy_requires_exact_callback_and_retained_owner()
    {
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess().Initialize500EA0(Source([]));
        stage = stage with { Task = new(new(NativeBattleFinalStage142.CallbackAddress, null), 0) };
        stage.Validate();
        NativeBattleStageState restored = JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(stage))!;
        restored.Validate();
        Assert.Null(restored.TextureAnimations);
        Assert.Null(restored.Task!.Callback.UpdateTexture0);
        Assert.NotNull(restored.FinalStage142);
        Assert.Throws<InvalidDataException>(() => (stage with { Task = new(new(0x512981, null), 0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (stage with { FinalStage142 = null }).Validate());
    }

    private static NativeBattleStageSource Source(ImmutableArray<byte> textureAnimations)
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] animations = new byte[17];
        BinaryPrimitives.WriteInt32LittleEndian(animations, 1);
        BinaryPrimitives.WriteInt32LittleEndian(animations.AsSpan(4), 8);
        animations[8] = 3;
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), animations.ToImmutableArray());
        return new(8, [source, source, source, source], textureAnimations, new("stage:TIM", [])) { ModelColors = NativeBattleFinalStage142.LoadedColors };
    }
}
