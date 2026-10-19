using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShot194ResourcesTests
{
    [Fact]
    public void Original_Flame_Shot_retains_its_actor_override_four_sprites_five_meshes_and_five_echo_colors()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([194])[193];
        Assert.Equal(0x5B4C60u, binding.Entry);
        Assert.Equal(0x5B4C40u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag193.tim", 0x22764CC), binding.InitializerResource);
        NativeBattleShot194Resources resources = Assert.IsType<NativeBattleShot194Resources>(binding.Shot194Resources);
        Assert.Equal(7104, resources.ActorOverride.Data.Length);
        Assert.Equal(new[] { 0x14, 6408, 6444, 7104 }, Enumerable.Range(0, 4).Select(index => NativeBattleDatDirectory.Section(resources.ActorOverride.Data.AsSpan(), index)));
        Assert.Equal(new[] { 16, 7, 4, 1 }, resources.Sprites.Select(sprite => BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))).Select(value => (int)value));
        Assert.Equal(new uint[] { 0x2505050, 0x2404040, 0x2303030, 0x2202020, 0x2101010 }, resources.EchoColors);
        Assert.Equal(new uint[] { 0xD0DCE4, 0xD0FB0C, 0xD106DC, 0xD11B54, 0xD1283C }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.Equal(new NativeBattleVector(0, 0, -4096), Assert.Single(resources.ReferenceAxes));
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleShot194Resources>(JsonSerializer.Serialize(resources))));
    }
}
