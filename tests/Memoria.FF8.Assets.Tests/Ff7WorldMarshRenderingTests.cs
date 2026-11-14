using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldMarshRenderingTests
{
    [Fact]
    public void OriginalMarshQuadsUseTheTwelveNativePalettesAtlasAndSharedGroundDepth()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for marsh rendering.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        PcTexture texture = PcTexture.Read(resources.ReadRequired("midg.tex"));
        Ff7WorldMarshAdapter adapter = new(texture, Ff7PcExecutableData.LoadWorldPresentation(installation, resources));
        Matrix4x4 rotation = Matrix4x4.CreateRotationX(MathF.PI / 4);
        Ff7WorldView view = new(rotation * Matrix4x4.CreateTranslation(0, 0, 10000), rotation, 400, 640, 480, 240);
        Ff7WorldMarshSegment[] segments = Enumerable.Range(0, 12).Select(index => new Ff7WorldMarshSegment(index, Vector3.Zero, 0)).ToArray();
        Ff7WorldChunkPlacement[] chunks = [new(0, 0, 0, 0, Vector3.Zero)];
        Capture first = new();
        Capture repeated = new();
        Assert.Equal(24, adapter.Draw(first, view, Vector3.Zero, 0, 5000, chunks, segments));
        adapter.Draw(repeated, view, Vector3.Zero, 0, 5000, chunks, segments);
        Assert.Equal(first.Triangles, repeated.Triangles);
        int[] palettes = [7, 6, 5, 4, 3, 3, 3, 3, 3, 2, 1, 0];
        for (int index = 0; index < palettes.Length; index++)
        {
            var triangle = first.Triangles[index * 2];
            Assert.Equal(new Vector2(palettes[index] * 16f / texture.Width, 0), triangle.A.Uv);
            Assert.Equal(new Vector2((palettes[index] * 16f + 15) / texture.Width, 0), triangle.B.Uv);
            Assert.Equal(new Vector4(128 / 255f), triangle.A.Color);
            Assert.Equal(1, triangle.A.ReciprocalW);
            Assert.Equal(triangle.A.Depth, triangle.B.Depth);
            Assert.Equal(triangle.A.Depth, triangle.C.Depth);
            Assert.Equal(TriangleBlend.Replace, triangle.Material.Blend);
            Assert.False(triangle.Material.DepthWrite);
        }
        Assert.Equal(0, adapter.Draw(new Capture(), view, Vector3.Zero, 0, 5000, [], segments));
        Assert.Equal(0, adapter.Draw(new Capture(), view, new(31000, 0, 0), 0, 5000, chunks, segments));
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<(TriangleVertex A, TriangleVertex B, TriangleVertex C, TriangleMaterial Material)> Triangles { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles.Add((a, b, c, material));
        }

        public void End()
        {
        }
    }
}
