using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleModelColorOwnerTests
{
    [Fact]
    public void Authenticated_image_preserves_the_two_distinct_cold_override_actor_bytes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe"));
        byte[] packed = NativeBattleEffectStaticResourcesReader.Read(executable, 0xB8BA00, 8).ToArray();
        Assert.Equal(0xFF000000u, BinaryPrimitives.ReadUInt32LittleEndian(packed));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(packed.AsSpan(4)));
        NativeBattleModelColorOverrides actual = Assert.IsType<NativeBattleModelColorOverrides>(game.ReadColdProcessNativeBattleMemory().EffectMemory!.ModelColorOverrides);
        Assert.Equal(NativeBattleModelColorOverrides.FromColdProcess().Slots.ToArray(), actual.Slots.ToArray());
        Assert.Equal(new byte?[] { 255, 0 }, actual.Slots.Select(slot => slot.Actor).ToArray());
        Assert.Equal(new uint?[] { 0, 0 }, actual.Slots.Select(slot => slot.Color).ToArray());
    }
}
