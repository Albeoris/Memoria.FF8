using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGuestTextureMotionTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Original_guest_body_texture_motion_directory_reaches_its_graphics_resource(int modelId)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleModelDatSource dat = assets.ReadNativeModelDatSource(checked((byte)modelId), false, 0);
        byte[] expected = dat.Bytes.Slice(dat.Section(3), dat.Section(4) - dat.Section(3)).ToArray();
        BattleModel body = assets.LoadPlayer(modelId);
        Assert.NotNull(body.NativeTextureAnimations);
        Assert.Equal(expected, body.NativeTextureAnimations.Value.ToArray());
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(new() { Id = "source", IsPlayer = true, ModelId = modelId });
        Assert.NotNull(resource.TextureAnimations);
        Assert.Equal(expected, resource.TextureAnimations.Value.ToArray());
        resource.Validate();
    }
}
