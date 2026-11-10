using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattleArenaResourcesTests
{
    [Theory]
    [InlineData(9, "op", 5, 7, 845)]
    [InlineData(10, "oq", 24, 5, 1321)]
    [InlineData(11, "or", 17, 5, 1070)]
    [InlineData(12, "os", 9, 5, 803)]
    [InlineData(13, "ot", 10, 7, 687)]
    [InlineData(25, "pf", 17, 6, 727)]
    [InlineData(26, "pg", 17, 7, 691)]
    [InlineData(29, "pj", 10, 6, 772)]
    [InlineData(41, "pv", 9, 5, 803)]
    [InlineData(49, "qd", 16, 8, 727)]
    [InlineData(85, "rn", 23, 5, 1235)]
    public void InstalledOpeningArenasDecodeOriginalMeshesAndTextureBindings(int locationId, string prefix, int partCount, int textureCount, int polygonCount)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the native battle arena geometry check.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        Ff7BattleArenaResources arena = Ff7BattleArenaResources.Load(archive, locationId);

        Assert.Equal(locationId, arena.LocationId);
        Assert.Equal(prefix, arena.Prefix);
        Assert.Equal(52, arena.Header.Length);
        Assert.Equal(8, arena.StageData.Length);
        Assert.Equal(partCount, arena.Geometry.Count);
        Assert.Equal(textureCount, arena.Textures.Count);
        Assert.Equal(polygonCount, arena.Geometry.Sum(part => part.Polygon.Polygons.Count));
        Assert.Equal(prefix + "am", arena.Geometry[0].ResourceName);
        Assert.Equal(prefix + "ac", arena.Textures[0].ResourceName);
        Assert.Contains(arena.Geometry.SelectMany(part => part.Polygon.Groups), group => group.TexturesUsed != 0);
        foreach (Ff7BattleArenaPart part in arena.Geometry)
        {
            Assert.NotEmpty(part.Polygon.Vertices);
            Assert.NotEmpty(part.Polygon.Groups);
            Assert.Equal(part.Polygon.Groups.Count, part.Polygon.RenderStates.Count);
            Assert.All(part.Polygon.RenderStates, state => PcRenderState.Read(state.Span));
        }
        foreach (Ff7BattleArenaTexture entry in arena.Textures)
        {
            PcTexture texture = entry.Texture;
            Assert.Equal(1, texture.BytesPerPixel);
            Assert.Equal(256, Assert.Single(texture.Palettes).Count);
            Assert.Equal(texture.Width * texture.Height, texture.Pixels.Length);
        }
    }

    [Theory]
    [InlineData("opac", 7, 256, 0xFF7B947Bu, 0x82)]
    [InlineData("rtac", 2, 16, 0xFF180808u, 0)]
    [InlineData("ruac", 2, 16, 0xFF8B7B6Au, 0)]
    [InlineData("sbac", 3, 16, 0xFE180808u, 0)]
    public void InstalledBattleTextureConsumesOnlyItsStoredPalette(string resourceName, int nominalPalettes, int storedColors, uint secondColor, byte firstPixel)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the native battle texture check.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        byte[] member = archive.ReadEntry(Assert.Single(archive.Find(resourceName)));
        PcTexture texture = PcTexture.Read(member);

        Assert.Equal(nominalPalettes, BinaryPrimitives.ReadInt32LittleEndian(texture.Header.Span[0x30..]));
        Assert.Equal(storedColors, BinaryPrimitives.ReadInt32LittleEndian(texture.Header.Span[0x58..]));
        IReadOnlyList<uint> colors = Assert.Single(texture.Palettes);
        Assert.Equal(storedColors, colors.Count);
        Assert.Equal(secondColor, colors[1]);
        Assert.Equal(firstPixel, texture.Pixels.Span[0]);
        Assert.True(texture.Pixels.Span.SequenceEqual(member.AsSpan(0xEC + 4 * storedColors)));
        Assert.True(texture.TrailingData.IsEmpty);
        Assert.Throws<InvalidDataException>(() => PcTexture.Read(member.AsSpan(0, member.Length - 1)));
    }
}
