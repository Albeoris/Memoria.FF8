using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class ModelBrowserDialogTests
{
    [Fact]
    public void Native_opaque_preview_keeps_stp_texels_solid_without_removing_cutouts_or_blended_materials()
    {
        EditableModel source = Triangle();
        PixelImage texture = new(2, 1, [new Rgba32(255, 255, 255, 128), default]);
        EditableMesh mesh = source.Meshes[0] with { Texture = 0, NativePart = 0, NativeTexture = 0, NativeMaterial = 0 };
        EditableModel model = source with { Meshes = [mesh], Textures = [texture] };
        RecordingRenderer opaque = new();
        ModelPreviewRenderer.Render(opaque, model, 800, 400, 0, 0, 1, 0);
        TriangleMaterial material = Assert.Single(opaque.Materials);
        Assert.Equal(TriangleBlend.Replace, material.Blend);
        Assert.Equal(1f / 255, material.MinimumAlpha);
        Assert.Equal((byte)128, material.Texture!.Pixels[0].A);
        Assert.Equal((byte)0, material.Texture.Pixels[1].A);

        RecordingRenderer blended = new();
        ModelPreviewRenderer.Render(blended, model with { Meshes = [mesh with { Transparent = true }] }, 800, 400, 0, 0, 1, 0);
        Assert.Equal(TriangleBlend.Alpha, Assert.Single(blended.Materials).Blend);

        RecordingRenderer mask = new();
        ModelPreviewRenderer.Render(mask, model with { Meshes = [mesh with { NativePart = -1, NativeTexture = -1, NativeMaterial = -1, AlphaCutoff = 0.25f }] }, 800, 400, 0, 0, 1, 0);
        Assert.Equal(0.25f, Assert.Single(mask.Materials).MinimumAlpha);
        Assert.Equal(TriangleBlend.Replace, Assert.Single(mask.Materials).Blend);
    }

    [Fact]
    public void Preview_centers_transformed_geometry_and_applies_zoom_rotation_uv_and_color()
    {
        EditableModel model = Triangle();
        RecordingRenderer original = new();
        ModelPreviewRenderer.Render(original, model, 800, 400, 0, 0, 1, 0);
        Assert.Equal((800, 400), original.Size);
        Assert.Equal(1, original.Ends);
        Assert.Equal(3, original.Vertices.Count);
        Assert.All(original.Vertices, vertex =>
        {
            Assert.InRange(vertex.Position.X, 0, 800);
            Assert.InRange(vertex.Position.Y, 0, 400);
            Assert.InRange(vertex.Depth, 0, 1);
        });
        Assert.True(original.Vertices[2].Position.Y < original.Vertices[0].Position.Y);
        Assert.Equal(model.Meshes[0].Vertices.Select(vertex => vertex.Uv), original.Vertices.Select(vertex => vertex.Uv));
        Assert.Equal(model.Meshes[0].Vertices.Select(vertex => vertex.Color), original.Vertices.Select(vertex => vertex.Color));
        Assert.Null(Assert.Single(original.Materials).Texture);
        RecordingRenderer zoomed = new();
        ModelPreviewRenderer.Render(zoomed, model, 800, 400, 0, 0, 2, 0);
        Vector2 center = new(400, 200);
        for (int index = 0; index < 3; index++)
        {
            Assert.InRange(Vector2.Distance((original.Vertices[index].Position - center) * 2, zoomed.Vertices[index].Position - center), 0, 0.001f);
        }
        RecordingRenderer rotated = new();
        ModelPreviewRenderer.Render(rotated, model, 800, 400, MathF.PI / 2, 0, 1, 0);
        Assert.NotEqual(original.Vertices[0].Position, rotated.Vertices[0].Position);
    }

    [Fact]
    public void Preview_uses_authoring_up_and_front_and_applies_screen_space_pan()
    {
        EditableModel model = Triangle();
        RecordingRenderer original = new();
        ModelPreviewRenderer.Render(original, model, 800, 400, 0, 0, 1, 0);
        Assert.True(original.Vertices[2].Position.Y < original.Vertices[0].Position.Y);
        Assert.True(original.Vertices[2].Depth > original.Vertices[0].Depth);
        GameUi.ModelPreviewCamera camera = new();
        camera.PanBy(0.25f, -0.1f);
        RecordingRenderer panned = new();
        ModelPreviewRenderer.Render(panned, model, 800, 400, camera, 0);
        for (int index = 0; index < original.Vertices.Count; index++)
        {
            Assert.True(Vector2.Distance(original.Vertices[index].Position + new Vector2(100, -40), panned.Vertices[index].Position) < 0.001f);
        }
    }

    [Fact]
    public void Preview_rejects_empty_geometry_before_begin_and_balances_end_after_draw_failure()
    {
        EditableModel model = Triangle();
        RecordingRenderer empty = new();
        Assert.Throws<InvalidDataException>(() => ModelPreviewRenderer.Render(empty, model with { Meshes = [] }, 800, 400, 0, 0, 1, 0));
        Assert.Equal(default, empty.Size);
        Assert.Equal(0, empty.Ends);
        RecordingRenderer failure = new() { FailDraw = true };
        Assert.Throws<InvalidOperationException>(() => ModelPreviewRenderer.Render(failure, model, 800, 400, 0, 0, 1, 0));
        Assert.Equal(1, failure.Ends);
    }

    private static EditableModel Triangle()
    {
        EditableVertex[] vertices =
        [
            new(new Vector3(10, 20, 0), Vector3.UnitZ, new Vector2(0, 0), Vector4.Zero, Vector4.Zero, new Vector4(1, 0, 0, 1)),
            new(new Vector3(12, 20, 0), Vector3.UnitZ, new Vector2(1, 0), Vector4.Zero, Vector4.Zero, new Vector4(0, 1, 0, 1)),
            new(new Vector3(10, 18, 1), Vector3.UnitZ, new Vector2(0, 1), Vector4.Zero, Vector4.Zero, new Vector4(0, 0, 1, 1))
        ];
        EditableMesh mesh = new("triangle", vertices, [0, 1, 2], -1, Matrix4x4.CreateTranslation(7, -4, 2));
        return new EditableModel("synthetic", "field", [], [mesh], [], [], JsonSerializer.SerializeToElement(new { }));
    }

    private sealed class RecordingRenderer : ITriangleRenderer
    {
        public (int Width, int Height) Size { get; private set; }
        public List<TriangleVertex> Vertices { get; } = [];
        public List<TriangleMaterial> Materials { get; } = [];
        public int Ends { get; private set; }
        public bool FailDraw { get; init; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Size = (width, height);
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            if (FailDraw)
            {
                throw new InvalidOperationException("Synthetic draw failure.");
            }
            Vertices.AddRange([a, b, c]);
            Materials.Add(material);
        }

        public void End()
        {
            Ends++;
        }
    }
}
