using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Tests;

public sealed class Ff7FixedEyeMaterialTests
{
    [Fact]
    public void OriginalCloudFixedClosedEyesChangeOnlyNativeMaterialSelection()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the fixed Cloud eye material check.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "ealin_2");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        using LgpArchive fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, field.Models.Models[0], executable, fields);
        Assert.NotNull(model.Eyes?.ClosedLeft);
        Assert.NotNull(model.Eyes?.ClosedRight);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = model }, new(50, 32000));
        Ff7ActorVisual actor = new(1, 0, -288 * 4096, 48 * 4096, 383 * 4096, 0, 0, 0) { AutomaticBlinkDisabled = true };
        Capture opened = new();
        Capture closed = new();
        Capture repeated = new();
        renderer.Render(opened, 320, 240, new(0, Vector2.Zero, [actor with { NativeEyeLeft = 1, NativeEyeRight = 1 }], new ushort[256], MovieActive: true));
        renderer.Render(closed, 320, 240, new(0, Vector2.Zero, [actor with { NativeEyeLeft = 2, NativeEyeRight = 2 }], new ushort[256], MovieActive: true));
        renderer.Render(repeated, 320, 240, new(0, Vector2.Zero, [actor with { NativeEyeLeft = 2, NativeEyeRight = 2 }], new ushort[256], MovieActive: true));
        Assert.NotEmpty(closed.Positions);
        Assert.Equal(opened.Positions, closed.Positions);
        Assert.Equal(opened.Uvs, closed.Uvs);
        Assert.Equal(closed.Positions, repeated.Positions);
        Assert.Equal(closed.Materials, repeated.Materials);
        var left = Ff7FieldRenderer.DecodeTexture(model.Eyes!.ClosedLeft!);
        var right = Ff7FieldRenderer.DecodeTexture(model.Eyes.ClosedRight!);
        Assert.Contains(closed.Materials, material => material.Texture is not null && material.Texture.Pixels.SequenceEqual(left.Pixels));
        Assert.Contains(closed.Materials, material => material.Texture is not null && material.Texture.Pixels.SequenceEqual(right.Pixels));
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<Vector2> Positions { get; } = [];
        public List<Vector2> Uvs { get; } = [];
        public List<TriangleMaterial> Materials { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Positions.AddRange([a.Position, b.Position, c.Position]);
            Uvs.AddRange([a.Uv, b.Uv, c.Uv]);
            Materials.Add(material);
        }

        public void End()
        {
        }
    }
}
