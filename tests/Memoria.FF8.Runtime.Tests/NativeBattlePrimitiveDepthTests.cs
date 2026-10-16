using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePrimitiveDepthTests
{
    [Fact]
    public void Disabled_descriptor_or_zero_first_proxy_depth_uses_the_original_bucket_plane_without_RHW()
    {
        NativeBattlePrimitiveDepthState state = NativeBattlePrimitiveDepthState.FromColdProcess();
        float expected = (float)(300d * (float)(0.9985 / 8772) + 0.0015f);
        foreach (ImmutableArray<NativeBattlePrimitiveDepthVertex> vertices in new[] { state.Project461220(300, false, 0, [400, 800, 1600, 0]), state.Project461220(300, true, 0, [0, 800, 1600, 0]) })
        {
            Assert.All(vertices, vertex => Assert.Equal(new NativeBattlePrimitiveDepthVertex(expected, 1), vertex));
        }
    }

    [Fact]
    public void Cold_depth_uses_static_remaining_weight_until_the_real_460B60_producer_replaces_it()
    {
        NativeBattlePrimitiveDepthState cold = NativeBattlePrimitiveDepthState.FromColdProcess();
        ImmutableArray<NativeBattlePrimitiveDepthVertex> vertices = cold.Project461220(300, true, 0, [1024, 2048, 4096, 8192]);
        float scale = (float)(0.99f / 65536d);
        Assert.Equal((float)((double)scale * 1024 + 0.0015f), vertices[0].Depth);
        Assert.Equal(1f / 1024, vertices[0].ReciprocalW);
        NativeBattlePrimitiveDepthState set = cold.SetBucketMix460B60(0);
        Assert.Equal(1, set.RemainingWeight);
        Assert.NotEqual(vertices[0].Depth, set.Project461220(300, true, 0, [1024, 2048, 4096, 8192])[0].Depth);
        NativeBattlePrimitiveDepthState restored = JsonSerializer.Deserialize<NativeBattlePrimitiveDepthState>(JsonSerializer.Serialize(cold))!;
        Assert.Equal(vertices, restored.Project461220(300, true, 0, [1024, 2048, 4096, 8192]));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(2, 1)]
    public void Bucket_mix_clamps_at_its_native_producer_and_flag4_selects_direct_vertex_depth(float input, float expected)
    {
        NativeBattlePrimitiveDepthState state = NativeBattlePrimitiveDepthState.FromColdProcess().SetBucketMix460B60(input);
        Assert.Equal(expected, state.BucketMix);
        Assert.Equal(1 - expected, state.RemainingWeight);
        ImmutableArray<NativeBattlePrimitiveDepthVertex> vertices = state.Project461220(300, true, 4, [1024, 2048, 4096, 8192]);
        Assert.Equal((float)(1024d * NativeBattlePrimitiveDepthState.VertexDepthScale + 0.0015f), vertices[0].Depth);
        Assert.Equal(1f / 8192, vertices[3].ReciprocalW);
    }
}
