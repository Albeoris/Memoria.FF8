using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShot192ResourcesTests
{
    [Fact]
    public void Original_Scatter_Shot_retains_its_actor_override_five_sprites_four_meshes_and_five_echo_colors()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([192])[191];
        Assert.Equal(0x5B96F0u, binding.Entry);
        Assert.Equal(0x5B96D0u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag191.tim", 0x228186C), binding.InitializerResource);
        NativeBattleShot192Resources resources = Assert.IsType<NativeBattleShot192Resources>(binding.Shot192Resources);
        Assert.Equal(0x1570, resources.ActorOverride.Data.Length);
        Assert.Equal(new[] { 0x14, 0x1334, 0x1358, 0x1570 }, Enumerable.Range(0, 4).Select(index => NativeBattleDatDirectory.Section(resources.ActorOverride.Data.AsSpan(), index)));
        Assert.Equal(new[] { 7, 16, 4, 16, 16 }, resources.Sprites.Select(sprite => BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))).Select(value => (int)value));
        Assert.Equal(new uint[] { 0x2505050, 0x2404040, 0x2303030, 0x2202020, 0x2101010 }, resources.EchoColors);
        Assert.Equal(new uint[] { 0xD17220, 0xD17768, 0xD17B58, 0xD17DC0 }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.Equal(new NativeBattleVector(0, 0, -4096), Assert.Single(resources.ReferenceAxes));
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleShot192Resources>(JsonSerializer.Serialize(resources))));
    }
}
