using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleHeroResourcesTests
{
    [Fact]
    public void Original_resources_admit_both_distinct_native_addresses_and_the_complete_typed_timelines()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the Hero modules.");
        using GameAssets game = new(directory!);
        NativeBattleHeroResources hero = game.ReadNativeHeroResources(309);
        NativeBattleHeroResources holy = game.ReadNativeHeroResources(310);
        Assert.Equal(0x10244E4u, hero.Main.Address);
        Assert.Equal(0x10231D8u, holy.Main.Address);
        Assert.Equal(0x1024D6Cu, hero.Orbit.Address);
        Assert.Equal(0x1023A60u, holy.Orbit.Address);
        Assert.Equal(0x888, hero.Main.Data.Length);
        Assert.Equal(0x7BC, hero.Orbit.Data.Length);
        Assert.Equal(0x2B8, hero.Fan.Length);
        Assert.Equal(hero.Main.Data.ToArray(), holy.Main.Data.ToArray());
        Assert.Equal(hero.Orbit.Data.ToArray(), holy.Orbit.Data.ToArray());
        Assert.Equal(hero.Fan.ToArray(), holy.Fan.ToArray());
        Assert.Equal(300188u, hero.Sound);
        Assert.Equal(hero.Sound, holy.Sound);
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(hero.Main.Data.AsSpan().Slice(8));
        int firstFace = geometry + BinaryPrimitives.ReadInt32LittleEndian(hero.Main.Data.AsSpan().Slice(geometry)) + 16;
        ushort[] encoded = Enumerable.Range(0, 16).SelectMany(face => Enumerable.Range(0, 4).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(hero.Main.Data.AsSpan().Slice(firstFace + face * 24 + 4 + corner * 2)))).ToArray();
        Assert.Contains((ushort)2, encoded);
        Assert.Contains((ushort)48, encoded);
        Assert.All(encoded, value =>
        {
            Assert.Equal(0, value & 1);
            Assert.InRange(value * 4, 0, 25 * 8 - 8);
        });
        Assert.Same(hero, game.ReadNativeHeroResources(309));
        Assert.Same(holy, game.ReadNativeHeroResources(310));
        Assert.Throws<NotSupportedException>(() => game.ReadNativeHeroResources(308));
    }
}
