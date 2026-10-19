using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShot196ResourcesTests
{
    [Fact]
    public void Original_Quick_Shot_retains_its_actor_override_two_sprites_two_meshes_and_five_echo_colors()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([196])[195];
        Assert.Equal(0x5B1900u, binding.Entry);
        Assert.Equal(0x5B18E0u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag195.tim", 0x226A6C4), binding.InitializerResource);
        NativeBattleShot196Resources resources = Assert.IsType<NativeBattleShot196Resources>(binding.Shot196Resources);
        Assert.Equal(0xB28, resources.ActorOverride.Data.Length);
        Assert.Equal(new[] { 0x14, 0x9EC, 0xA14, 0xB28 }, Enumerable.Range(0, 4).Select(index => NativeBattleDatDirectory.Section(resources.ActorOverride.Data.AsSpan(), index)));
        Assert.Equal(new[] { 16, 8 }, resources.Sprites.Select(sprite => BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))).Select(value => (int)value));
        Assert.Equal(new uint[] { 0x2505050, 0x2404040, 0x2303030, 0x2202020, 0x2101010 }, resources.EchoColors);
        Assert.Equal(new uint[] { 0xD0609C, 0xD06FD4 }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.Equal(new NativeBattleVector(0, 0, -4096), Assert.Single(resources.ReferenceAxes));
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleShot196Resources>(JsonSerializer.Serialize(resources))));
    }
}
