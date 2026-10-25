using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Rendering;
using System.Numerics;

namespace Memoria.FF8.Mods.Tests;

public sealed class WorldFootModelTextureTests
{
    [Fact]
    public void OriginalFootEntityMappingPublishesBothNativeTextureMembers()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(game), "Configure optional original game data.");
        using ClassicGameDataSource source = new(game!);
        ResourceService resources = new(source);
        using WorldAssets assets = new(game!, resources);
        foreach (byte entity in new byte[] { 0, 3, 4, 5, 7, 8, 9 })
        {
            Assert.Equal("World character shape 0", assets.GetFootModel(entity).Name);
        }
        Assert.Equal("World character shape 4", assets.GetFootModel(1).Name);
        Assert.Equal("World character shape 5", assets.GetFootModel(2).Name);
        FieldModel model = assets.GetFootModel(6);
        Assert.Equal("World character shape 6", model.Name);
        Assert.Equal(2, model.Textures.Count);
        Assert.Contains(model.Faces, face => face.Texture == 0);
        Assert.Contains(model.Faces, face => face.Texture == 1);
        Rgba32[] colors = [new(200, 10, 20, 255), new(10, 200, 20, 255)];
        ModFile[] files = model.Textures.Select((texture, index) => Sheet(index, texture.Width, texture.Height, colors[index])).ToArray();
        resources.Prepare(new FfnxTextureResolver(new(1, [], files))).Commit();
        for (int texture = 0; texture < 2; texture++)
        {
            Assert.True(((ResourceTextureList)model.Textures).GetTexture(texture).IsReplacement);
            Assert.Equal(colors[texture], model.Textures[texture][0, 0]);
        }
        WorldTerrainRenderer renderer = new(assets, 320, 240);
        Vector3 position = new(24576, 658, -29406);
        WorldCamera camera = WorldCamera.Foot(2048, 2048) with { TargetPosition = position + new Vector3(0, 256, 0) };
        Dictionary<int, int> replacements = new() { [274] = 827, [275] = 828 };
        byte[] both = renderer.Render(position, MathF.PI, 0, 0, camera, replacements, avatarModel: model).ToArray();
        for (int member = 0; member < 2; member++)
        {
            resources.Prepare(new FfnxTextureResolver(new(1, [], [files[1 - member]]))).Commit();
            byte[] single = renderer.Render(position, MathF.PI, 0, 0, camera, replacements, avatarModel: model).ToArray();
            Assert.NotEqual(both, single);
        }
        Assert.Throws<NotSupportedException>(() => assets.GetFootModel(10));
    }

    private static ModFile Sheet(int member, int width, int height, Rgba32 color)
    {
        using MagickImage image = new(new MagickColor(color.R, color.G, color.B, color.A), (uint)(width * 2), (uint)(height * 2));
        byte[] png = image.ToByteArray(MagickFormat.Png);
        return new("test", ModFormat.Junction, $"mods/textures/world/esk/chara_one/model6-{member}_00.png", 1, () => png);
    }
}
