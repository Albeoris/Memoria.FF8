using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldTerrainSelectionTests
{
    [Fact]
    public void NativePersistentStoryMasksSelectAllSevenOriginalRectangles()
    {
        Assert.Equal((ushort)285, WorldTerrainSelection.ReplacementMask(0, 0));
        Assert.Equal((ushort)269, WorldTerrainSelection.ReplacementMask(0, 1));
        Assert.Equal((ushort)257, WorldTerrainSelection.ReplacementMask(0, 2));
        Assert.Equal((ushort)256, WorldTerrainSelection.ReplacementMask(0, 3));
        Assert.Equal((ushort)384, WorldTerrainSelection.ReplacementMask(0, 4));
        Assert.Equal((ushort)349, WorldTerrainSelection.ReplacementMask(8, 0xE0));
        Assert.Throws<InvalidDataException>(() => WorldTerrainSelection.ReplacementMask(0, 5));
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(127);
        Assert.Equal(67, replacements.Count);
        Assert.Equal(Enumerable.Range(768, 67), replacements.Values.Order());
        Assert.Equal(768, replacements[373]);
        Assert.Equal(775, replacements[380]);
        Assert.Equal(776, replacements[405]);
        Assert.Equal(823, replacements[572]);
        Assert.False(replacements.ContainsKey(381));
        Assert.False(replacements.ContainsKey(404));
        Assert.Equal(824, replacements[149]);
        Assert.Equal(825, replacements[150]);
        Assert.Equal(826, replacements[267]);
        Assert.Equal(827, replacements[274]);
        Assert.Equal(828, replacements[275]);
        Assert.Equal(829, replacements[327]);
        Assert.Equal(830, replacements[214]);
        Assert.Equal(831, replacements[215]);
        Assert.Equal(832, replacements[246]);
        Assert.Equal(833, replacements[247]);
        Assert.Equal(834, replacements[361]);
        Assert.False(WorldTerrainSelection.Replacements(WorldTerrainSelection.ReplacementMask(0, 2)).ContainsKey(275));
    }

    [Fact]
    public void NativeSkySelectionUsesFirstRegionStoryExclusionAndIntegerBlend()
    {
        WorldSky fallback = Sky(0, 0, 0, 10);
        WorldSky special = Sky(0, 0, 320, 210);
        WorldSky region = Sky(0, 0, 320, 110);
        WorldSky overlapping = Sky(0, 0, 640, 250);
        WorldSky[] skies = [fallback, special, region, overlapping];
        Assert.Equal(new Rgba32(110, 110, 110), WorldTerrainSelection.SelectSky(skies, 0, 0, 0).Top);
        Assert.Equal(new Rgba32(210, 210, 210), WorldTerrainSelection.SelectSky(skies, 0, 0, 4).Top);
        // distance5, radius10: native blend=5*(4096/10)=2045; truncating negative color delta gives61.
        Assert.Equal(new Rgba32(61, 61, 61), WorldTerrainSelection.SelectSky(skies, 160, 0, 0).Top);
        Assert.Equal(fallback, WorldTerrainSelection.SelectSky(skies, 640, 0, 0));
        Assert.Equal(fallback, WorldTerrainSelection.SelectSky([fallback], 0, 0, 0));
        Assert.Throws<InvalidDataException>(() => WorldTerrainSelection.SelectSky([], 0, 0, 0));
    }

    [Fact]
    public void SelectedOriginalSkyRegionAndGardenReplacementRetainInstalledMetadata()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(WorldTerrainSelection.ReplacementMask(0, 0));
        WorldSegment garden = assets.GetSegment(275, replacements);
        Assert.Equal(828, garden.SourceId);
        Assert.Equal(1047, garden.Triangles.Count);
        Assert.Equal(1, assets.LoadedSegmentCount);
        Assert.True(assets.Skies.Count > 2);
        WorldSky region = assets.Skies[2];
        Assert.True(region.Radius >= 32);
        WorldSky selected = WorldTerrainSelection.SelectSky(assets.Skies, (int)region.Position.X, (int)region.Position.Z, 0);
        Assert.Equal(region.Top, selected.Top);
        Assert.Equal(region.Center, selected.Center);
        Assert.Equal(region.Bottom, selected.Bottom);
    }

    private static WorldSky Sky(int x, int z, int radius, byte intensity)
    {
        Rgba32 color = new(intensity, intensity, intensity);
        return new WorldSky(new Vector3(x, 0, z), color, color, color, radius);
    }
}
