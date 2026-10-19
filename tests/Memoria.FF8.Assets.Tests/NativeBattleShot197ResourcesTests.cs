using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShot197ResourcesTests
{
    [Fact]
    public void Original_Armor_Shot_retains_its_actor_override_three_sprites_nine_meshes_and_five_echo_colors()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([197])[196];
        Assert.Equal(0x5AED30u, binding.Entry);
        Assert.Equal(0x5AED10u, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag196.tim", 0x2265B04), binding.InitializerResource);
        NativeBattleShot197Resources resources = Assert.IsType<NativeBattleShot197Resources>(binding.Shot197Resources);
        Assert.Equal(9164, resources.ActorOverride.Data.Length);
        Assert.Equal(new[] { 0x14, 8232, 8268, 9164 }, Enumerable.Range(0, 4).Select(index => NativeBattleDatDirectory.Section(resources.ActorOverride.Data.AsSpan(), index)));
        Assert.Equal(new[] { 14, 16, 9 }, resources.Sprites.Select(sprite => BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))).Select(value => (int)value));
        Assert.Equal(new uint[] { 0x2505050, 0x2404040, 0x2303030, 0x2202020, 0x2101010 }, resources.EchoColors);
        Assert.Equal(new uint[] { 0xCFDD18, 0xCFEDE8, 0xCFF2F8, 0xD012B0, 0xD02180, 0xD02570, 0xD03298, 0xD03940, 0xD04B88 }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.Equal(new NativeBattleVector(0, 0, -4096), resources.ReferenceAxes[0]);
        Assert.Equal(new NativeBattleVector(0, 0, 4096), resources.ReferenceAxes[1]);
        Assert.Equal(new NativeBattleVector(0, -4096, 0), resources.ReferenceAxes[2]);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleShot197Resources>(JsonSerializer.Serialize(resources))));
    }
}
