using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldBridgeRenderingTests
{
    [Fact]
    public void FrozenBridgeGeometryFeedsTheSharedTerrainSubmissionWithoutChangingOriginalMeshes()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for bridge presentation checks.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        Ff7WorldPresentationData presentation = Ff7PcExecutableData.LoadWorldPresentation(installation, resources);
        IReadOnlyList<Ff7WorldBridgeDefinition> definitions = Ff7PcExecutableData.LoadWorldBridges(installation);
        Ff7WorldBridgeGeometry geometry = new(definitions, new Ff7WorldMath(presentation));
        Ff7WorldTerrainAdapter renderer = new(new Ff7WorldTextureResources(installation, resources));
        Matrix4x4 rotation = Matrix4x4.CreateRotationX(MathF.PI / 4);
        foreach (Ff7WorldBridgeDefinition definition in definitions)
        {
            int block = (definition.ChunkZ >> 2) * 9 + (definition.ChunkX >> 2);
            int meshIndex = (definition.ChunkZ & 3) * 4 + (definition.ChunkX & 3);
            PcWorldMesh mesh = resources.Map.ReadMesh(block, meshIndex);
            Vector3[] original = mesh.Vertices.ToArray();
            Ff7WorldBridgeDeformation frozen = new(definition.Index, 7);
            Vector3[] deformed = original.Select((position, vertex) => geometry.Vertex(definition.ChunkX, definition.ChunkZ, vertex, position, frozen)).ToArray();
            Assert.False(original.SequenceEqual(deformed));
            Assert.All(Enumerable.Range(0, original.Length).Except(definition.Vertices), vertex => Assert.Equal(original[vertex], deformed[vertex]));
            float centerY = original.Average(position => position.Y);
            Ff7WorldView view = new(Matrix4x4.CreateTranslation(-4096, -centerY, -4096) * rotation * Matrix4x4.CreateTranslation(0, 0, 20000), rotation, 400, 640, 480, 240);
            Capture expected = new();
            renderer.DrawNative(expected, mesh with { Vertices = deformed }, Vector3.Zero, view, _ => 0);
            Assert.NotEmpty(expected.Vertices);
            int visits = 0;
            Capture actual = new();
            renderer.DrawNative(actual, mesh, Vector3.Zero, view, _ => 0, deformVertex: (vertex, position) =>
            {
                visits++;
                return geometry.Vertex(definition.ChunkX, definition.ChunkZ, vertex, position, frozen);
            });
            Assert.Equal(original.Length, visits);
            Assert.Equal(expected.Vertices, actual.Vertices);
            Assert.Equal(original, mesh.Vertices);
            Capture repeated = new();
            renderer.DrawNative(repeated, mesh, Vector3.Zero, view, _ => 0, deformVertex: (vertex, position) => geometry.Vertex(definition.ChunkX, definition.ChunkZ, vertex, position, frozen));
            Assert.Equal(actual.Vertices, repeated.Vertices);
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.AddRange([a, b, c]);
        }

        public void End()
        {
        }
    }
}
