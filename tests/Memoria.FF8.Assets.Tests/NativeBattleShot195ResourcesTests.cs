using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShot195ResourcesTests
{
    [Fact]
    public void Original_Canister_Shot_retains_its_actor_override_five_sprites_five_meshes_and_five_echo_colors()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([195])[194];
        Assert.Equal(0x5B2A40u, binding.Entry);
        Assert.Equal(0x5B2A20u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag194.tim", 0x22711C4), binding.InitializerResource);
        NativeBattleShot195Resources resources = Assert.IsType<NativeBattleShot195Resources>(binding.Shot195Resources);
        Assert.Equal(9164, resources.ActorOverride.Data.Length);
        Assert.Equal(new[] { 0x14, 8232, 8268, 9164 }, Enumerable.Range(0, 4).Select(index => NativeBattleDatDirectory.Section(resources.ActorOverride.Data.AsSpan(), index)));
        Assert.Equal(new[] { 16, 7, 16, 16, 16 }, resources.Sprites.Select(sprite => BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))).Select(value => (int)value));
        Assert.Equal(new uint[] { 0x2505050, 0x2404040, 0x2303030, 0x2202020, 0x2101010 }, resources.EchoColors);
        Assert.Equal(new uint[] { 0xD09FB8, 0xD0A978, 0xD0AD68, 0xD0AFE0, 0xD0B488 }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.Equal(new NativeBattleVector(0, 0, -4096), resources.ReferenceAxes[0]);
        Assert.Equal(new NativeBattleVector(-4096, 0, 0), resources.ReferenceAxes[1]);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleShot195Resources>(JsonSerializer.Serialize(resources))));
    }
}
