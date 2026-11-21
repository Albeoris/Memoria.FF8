using Memoria.FF7.Resources;

internal static class NativeSnowboardResourcesOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select an installed original resource profile."));
        Ff7SnowboardAssets assets = Ff7SnowboardResources.LoadStory(installation);
        var model = assets.Model;
        Ff7SnowboardNativeData native = Ff7PcExecutableData.LoadSnowboardData(installation);
        IReadOnlyList<Ff7SnowboardTextureBinding> textures = Ff7PcExecutableData.LoadSnowboardTextureBindings(installation);
        if (textures.Count != 16 || textures.Any(binding => !assets.Textures.ContainsKey(binding.Name) || binding.WidthWords <= 0 || binding.Height <= 0) || textures[0] != new Ff7SnowboardTextureBinding(320, 128, 14, 48, "eyes.tex", 4) || textures[10] != new Ff7SnowboardTextureBinding(320, 256, 32, 64, "ki0.tex", 4) || textures[12] != new Ff7SnowboardTextureBinding(384, 128, 80, 160, "fuusen1a.tex", 0))
        {
            throw new InvalidOperationException("Native snowboard texture rectangles or material types differ from their original bindings.");
        }
        if (assets.Textures.Count != 30 || model.Objects.Count != 273 || model.Objects.Sum(item => item.Vertices.Count) != 11795 || native.Courses.Count != 7 || native.TileGeometry.Count != 76 * 0x374 || native.Courses[0].Tiles.Count != 301 || native.Courses.Skip(1).Any(course => course.Tiles.Count != 50) || !native.Courses.Select(course => course.EndDistance).SequenceEqual(new short[] { 300, 350, 500, 400, 450, 550, 600 }) || !native.Results.SequenceEqual(new byte[] { 0, 0, 0, 2, 1, 4, 8 }))
        {
            throw new InvalidOperationException("The selected native snowboard model and course tables do not match their decoded structural contract.");
        }
        Console.WriteLine($"Native snowboard story resources: objects={model.Objects.Count}, vertices={model.Objects.Sum(item => item.Vertices.Count)}, primitives={model.Objects.Sum(item => item.Primitives.Count)}, tiles={native.TileGeometry.Count / 0x374}, courses={native.Courses.Count}.");
    }
}
