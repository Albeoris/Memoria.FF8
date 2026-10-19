using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeRoughDivide163ResourcesTests
{
    [Fact]
    public void OriginalRoughDivideResourcesDecodeActualDirectories()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRoughDivide163Resources resources = game.ReadNativeRoughDivide163Resources();
        resources.Validate();
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(resources.StaticData.Data.AsSpan()));
        Assert.Equal(2, resources.Sprites.Length);
        Assert.Equal(10, resources.Meshes.Length);
        Assert.Equal(0xD9FE54u, resources.Meshes[^1].Address);
        Assert.Equal(596, resources.Meshes[^1].Bytes.Length);
        Assert.All(resources.ColdGlobals, region => Assert.All(region.Data, value => Assert.Equal(0, value)));
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.StaticData, 0xE3C);
        textures.Validate();
    }
}
