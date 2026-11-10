using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7ColorOffsetPresentationTests
{
    [Fact]
    public void NativeOffsetsUseSharedColorOverridesWithoutChangingLightingOrTopology()
    {
        PcFieldCamera camera = new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero, 0, 0);
        Ff7NativeColorOffsetState pending = Ff7NativeColorOffset.Request(null, [10, 0, 236, 255, 255, 0]);
        var queued = Ff7Scene.CreateModelEffects(null, camera, null, offset: pending)!;
        Assert.Equal(0x73402010U, queued.VertexColor!(Vector3.Zero, Vector3.Zero, 0x73402010));
        var applied = Ff7Scene.CreateModelEffects(null, camera, null, offset: Ff7NativeColorOffset.Advance(pending, true))!;
        Assert.Equal(0x734A0CFFU, applied.VertexColor!(Vector3.One, Vector3.UnitX, 0x73402010));
        Assert.Equal(0x734A0CFFU, applied.VertexColor!(Vector3.Zero, Vector3.Zero, 0x73402010));
        Assert.False(applied.SuppressNativeLighting);
        Assert.False(applied.RequiresOriginalTopology);
        Assert.True(applied.PartVisible(100));
    }
}
