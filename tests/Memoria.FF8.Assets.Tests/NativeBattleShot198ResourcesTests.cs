using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShot198ResourcesTests
{
    [Fact]
    public void Original_Hyper_Shot_retains_its_actor_override_three_sprites_five_meshes_and_five_echo_colors()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([198])[197];
        Assert.Equal(0x5AA3E0u, binding.Entry);
        Assert.Equal(0x5AA3C0u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag197.tim", 0x22601BC), binding.InitializerResource);
        NativeBattleShot198Resources resources = Assert.IsType<NativeBattleShot198Resources>(binding.Shot198Resources);
        Assert.Equal(9164, resources.ActorOverride.Data.Length);
        Assert.Equal(new[] { 0x14, 8232, 8268, 9164 }, Enumerable.Range(0, 4).Select(index => NativeBattleDatDirectory.Section(resources.ActorOverride.Data.AsSpan(), index)));
        Assert.Equal(new[] { 16, 16, 15 }, resources.Sprites.Select(sprite => BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))).Select(value => (int)value));
        Assert.Equal(new uint[] { 0x2505050, 0x2404040, 0x2303030, 0x2202020, 0x2101010 }, resources.EchoColors);
        Assert.Equal(new uint[] { 0xCF6394, 0xCF8D2C, 0xCF9834, 0xCFA52C, 0xCFAE64 }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.Equal(new NativeBattleVector(0, 0, -4096), resources.ReferenceAxes[0]);
        Assert.Equal(new NativeBattleVector(0, 0, 4096), resources.ReferenceAxes[1]);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleShot198Resources>(JsonSerializer.Serialize(resources))));
    }
}
