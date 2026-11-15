using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldModelExportCoverageTests
{
    [Theory]
    [InlineData(false, "MASK")]
    [InlineData(true, "BLEND")]
    public void TextureCutoutsDoNotEnableNativePrimitiveTransparency(bool semiTransparent, string alphaMode)
    {
        FieldModel original = Model(semiTransparent);
        byte[] bytes = ModelExchange.ExportGlb(ModelExchange.FromField(original));
        using JsonDocument json = JsonDocument.Parse(bytes.AsMemory(20, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12))));
        Assert.Equal(alphaMode, json.RootElement.GetProperty("materials")[0].GetProperty("alphaMode").GetString());
        EditableModel edited = ModelExchange.ImportGlb(bytes).Model;
        Assert.Equal(semiTransparent, Assert.Single(edited.Meshes).Transparent);
        if (!semiTransparent)
        {
            Assert.Equal(1f / 255, edited.Meshes[0].AlphaCutoff);
        }
        FieldModel imported = ModelExchange.ToField(edited, original);
        Assert.All(imported.Faces, face => Assert.Equal(semiTransparent, face.SemiTransparent));
        Assert.Equal(original.Textures[0].Pixels, imported.Textures[0].Pixels);
        byte[] baseline = Render(original);
        Assert.Contains(baseline, value => value != 0 && value != 255);
        // Floating-point round trips can change an interpolated byte by one per overlapping draw.
        byte[] actual = Render(imported);
        Assert.All(baseline.Zip(actual), pair => Assert.InRange(Math.Abs(pair.First - pair.Second), 0, semiTransparent ? 2 : 1));
    }

    private static byte[] Render(FieldModel model)
    {
        FieldAssets field = new(0, "coverage", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), [model]);
        ActorVisual[] actors = [new(0, 0, 0, 0, 100, 192, 0, 0, true, PolygonColor: new Vector3(128))];
        return new FieldRenderer(field).Render(new Dictionary<int, int>(), actors, TimeSpan.Zero);
    }

    private static FieldModel Model(bool semiTransparent)
    {
        ModelFace face = new([0, 2, 1], [Vector2.One, Vector2.One, Vector2.One], 0, false) { SemiTransparent = semiTransparent };
        PixelImage texture = new(2, 2, [new(0, 0, 0, 0), new(63, 91, 127, 128), new(85, 105, 137, 255), new(79, 101, 131, 255)]);
        return new("coverage", [new(-1, 0)], [new(new(0, -10, -10), 0), new(new(0, 10, -10), 0), new(new(0, 0, 10), 0)], [face, face], [texture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
    }
}
