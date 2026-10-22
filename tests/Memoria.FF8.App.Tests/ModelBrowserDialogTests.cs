using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class ModelBrowserDialogTests : IDisposable
{
    private readonly string temporary = Path.Combine(Path.GetTempPath(), "Memoria.FF8.ModelBrowserTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Package_destination_accepts_only_normalized_strict_descendants()
    {
        string root = Path.Combine(temporary, "Mods", "Memoria");
        Directory.CreateDirectory(root);
        Assert.False(ModelBrowserDialogs.IsPackageDirectory(root, root));
        Assert.False(ModelBrowserDialogs.IsPackageDirectory(root, root + Path.DirectorySeparatorChar));
        Assert.False(ModelBrowserDialogs.IsPackageDirectory(root, root + "-sibling"));
        Assert.False(ModelBrowserDialogs.IsPackageDirectory(root, Path.Combine(root, "..", "outside")));
        Assert.False(ModelBrowserDialogs.IsPackageDirectory(root, Path.Combine(root, "package", "..")));
        Assert.True(ModelBrowserDialogs.IsPackageDirectory(root, Path.Combine(root, "new-package")));
        Assert.True(ModelBrowserDialogs.IsPackageDirectory(root + Path.DirectorySeparatorChar, Path.Combine(root, "new-package", "models")));
        Assert.True(ModelBrowserDialogs.IsPackageDirectory(root, Path.Combine(root, "package", "..", "new-package")));
    }

    [Fact]
    public void Filesystem_root_supports_descendants_without_accepting_itself()
    {
        Directory.CreateDirectory(temporary);
        string root = Path.GetPathRoot(temporary)!;
        Assert.False(ModelBrowserDialogs.IsPackageDirectory(root, root));
        Assert.True(ModelBrowserDialogs.IsPackageDirectory(root, temporary));
    }

    [Fact]
    public void Package_destination_rejects_reparse_points_in_destination_root_and_ancestors()
    {
        string root = Path.Combine(temporary, "Mods", "Memoria");
        string outside = Path.Combine(temporary, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        string destinationLink = Path.Combine(root, "linked-package");
        CreateLink(destinationLink, outside);
        Assert.False(ModelBrowserDialogs.IsPackageDirectory(root, destinationLink));
        Assert.False(ModelBrowserDialogs.IsPackageDirectory(root, Path.Combine(destinationLink, "new-models")));
        string rootLink = Path.Combine(temporary, "linked-root");
        CreateLink(rootLink, root);
        Assert.False(ModelBrowserDialogs.IsPackageDirectory(rootLink, Path.Combine(rootLink, "new-package")));
        string parentLink = Path.Combine(temporary, "linked-parent");
        CreateLink(parentLink, Path.Combine(temporary, "Mods"));
        string linkedRoot = Path.Combine(parentLink, "Memoria");
        Assert.False(ModelBrowserDialogs.IsPackageDirectory(linkedRoot, Path.Combine(linkedRoot, "new-package")));
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
            new(new Vector3(10, 22, 1), Vector3.UnitZ, new Vector2(0, 1), Vector4.Zero, Vector4.Zero, new Vector4(0, 0, 1, 1))
        ];
        EditableMesh mesh = new("triangle", vertices, [0, 1, 2], -1, Matrix4x4.CreateTranslation(7, -4, 2));
        return new EditableModel("synthetic", "field", [], [mesh], [], [], JsonSerializer.SerializeToElement(new { }));
    }

    private static void CreateLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or PlatformNotSupportedException || exception is IOException && (exception.HResult & 0xFFFF) == 1314)
        {
            Assert.Skip("Creating symbolic links is unavailable in this test environment.");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(temporary))
        {
            Directory.Delete(temporary, true);
        }
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
