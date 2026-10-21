using System.Buffers.Binary;
using System.Numerics;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class WorldTerrainTexelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_texture_addresses_decode_to_texel_centers(bool water)
    {
        WorldTriangle triangle = Decode([0, 96, 255], water);
        Vector2[] actual = [triangle.UvA, triangle.UvB, triangle.UvC];
        int[] indices = [0, 96, 255];
        Assert.Equal(water, triangle.Water);
        for (int corner = 0; corner < actual.Length; corner++)
        {
            float center = (indices[corner] + 0.5f) / 256;
            Assert.Equal(new Vector2(center), actual[corner]);
            if (water)
            {
                Assert.Equal((indices[corner] + 0.5f) / 128, actual[corner].Y * 2);
            }
        }
    }

    [Theory]
    [InlineData(96, false, false, 0)]
    [InlineData(96, false, false, 8)]
    [InlineData(96, false, true, 0)]
    [InlineData(96, false, true, 8)]
    [InlineData(96, true, false, 0)]
    [InlineData(96, true, false, 8)]
    [InlineData(96, true, true, 0)]
    [InlineData(96, true, true, 8)]
    [InlineData(160, false, false, 0)]
    [InlineData(160, false, false, 8)]
    [InlineData(160, false, true, 0)]
    [InlineData(160, false, true, 8)]
    [InlineData(160, true, false, 0)]
    [InlineData(160, true, false, 8)]
    [InlineData(160, true, true, 0)]
    [InlineData(160, true, true, 8)]
    public void Constant_native_texture_row_survives_perspective_and_camera_motion(int size, bool retained, bool water, int samples)
    {
        using GpuTestDevice device = new(samples);
        using GpuTriangleRenderer renderer = new(device.Device);
        WorldTriangle triangle = Decode([96, 96, 96], water);
        Vector3 origin = new(-131072, 0, -98304);
        Vector2 uvScale = water ? new Vector2(1, 2) : Vector2.One;
        WorldTerrainMesh mesh = new([
            new((triangle.A - origin) / 16, triangle.UvA * uvScale),
            new((triangle.B - origin) / 16, triangle.UvB * uvScale),
            new((triangle.C - origin) / 16, triangle.UvC * uvScale)
        ]);
        int height = water ? 128 : 256;
        Rgba32[] pixels = Enumerable.Repeat(new Rgba32(255, 0, 0, 255), 256 * height).ToArray();
        Array.Fill(pixels, new Rgba32(0, 255, 0, 255), 96 * 256, 256);
        TriangleMaterial material = new(new PixelImage(256, height, pixels), TriangleBlend.Replace, TriangleDepthComparison.Greater, true, Sampler: TriangleSampler.PointClamp)
        {
            DepthMapping = TriangleDepthMapping.Positive,
            DepthScale = 1
        };
        foreach (float offset in new[] { 0f, 0.03125f, 0.09375f })
        {
            WorldTerrainView view = new(Matrix4x4.CreateTranslation(offset, -offset * 0.5f, -offset), Vector3.Zero, size * 2f / 3, false);
            renderer.Begin(size, size, new Vector4(0, 0, 0, 1), 0);
            if (retained)
            {
                renderer.DrawTerrain(mesh, view, material);
            }
            else
            {
                TriangleVertex Project(WorldTerrainVertex vertex)
                {
                    Vector3 camera = Vector3.Transform(vertex.Position, view.View);
                    float reciprocal = -1 / camera.Z;
                    return new TriangleVertex(new Vector2(size / 2f + camera.X * reciprocal * view.Focal, size / 2f - camera.Y * reciprocal * view.Focal), reciprocal, reciprocal, vertex.Uv, Vector4.One);
                }
                ReadOnlySpan<WorldTerrainVertex> vertices = mesh.Vertices.Span;
                renderer.Draw(Project(vertices[0]), Project(vertices[1]), Project(vertices[2]), material);
            }
            renderer.End();
            Color[] actual = GpuTestDevice.Read(renderer.Target);
            Assert.True(actual.Count(pixel => pixel == Color.Lime) > 100, "The varying-depth triangle must cover a meaningful image area.");
            Assert.All(actual, pixel =>
            {
                Assert.Equal(0, pixel.R);
                Assert.Equal(0, pixel.B);
                Assert.Equal(255, pixel.A);
            });
        }
    }

    private static WorldTriangle Decode(byte[] coordinates, bool water)
    {
        const int block = 68;
        const int vertexStart = block + 4 + 16;
        byte[] bytes = new byte[vertexStart + 3 * 8 + 4];
        for (int index = 0; index < 16; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + index * 4), block);
        }
        bytes[block] = 1;
        bytes[block + 1] = 3;
        Span<byte> polygon = bytes.AsSpan(block + 4, 16);
        polygon[1] = 1;
        polygon[2] = 2;
        polygon[14] = water ? (byte)0x40 : (byte)0;
        for (int corner = 0; corner < 3; corner++)
        {
            polygon[6 + corner * 2] = coordinates[corner];
            polygon[7 + corner * 2] = coordinates[corner];
        }
        short[] positions = [-96, 80, 112, 112, 64, 208, -16, -112, 160];
        for (int vertex = 0; vertex < 3; vertex++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(vertexStart + vertex * 8 + axis * 2), positions[vertex * 3 + axis]);
            }
        }
        Type reader = typeof(WorldAssets).Assembly.GetType("Memoria.FF8.Assets.WorldAssetReader", throwOnError: true)!;
        MethodInfo method = reader.GetMethod("ReadSegment", BindingFlags.Public | BindingFlags.Static)!;
        WorldSegment segment = (WorldSegment)method.Invoke(null, [bytes, 0, 0])!;
        return segment.Triangles[0];
    }
}
