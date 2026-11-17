using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

public sealed partial class GpuTriangleRenderer
{
    private const int MaximumCachedTerrainMeshes = 4096;
    private const int MaximumCachedTerrainVertices = 2097152;
    private const int TerrainRetentionFrames = 600;
    private readonly Dictionary<WorldTerrainMesh, LinkedListNode<TerrainBuffer>> terrainBuffers = new(ReferenceEqualityComparer.Instance);
    private readonly LinkedList<TerrainBuffer> terrainRecency = new();
    private int cachedTerrainVertices;
    private int cachedTerrainBytes;
    private long terrainFrame;
    private WorldTerrainView? terrainView;
    private VertexBuffer? boundTerrain;

    internal int FrameTerrainUploads { get; private set; }
    internal int CachedTerrainBytes => cachedTerrainBytes;

    public void DrawTerrain(WorldTerrainMesh mesh, in WorldTerrainView view, in TriangleMaterial material, int startVertex = 0, int vertexCount = -1)
    {
        if (!active)
        {
            throw new InvalidOperationException("Begin must precede DrawTerrain.");
        }
        // Keep consecutive material runs on the same retained buffer bound. A streamed draw,
        // eviction, frame completion or transient buffer release ends that binding.
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Vertices.Length % 3 != 0)
        {
            throw new ArgumentException("Terrain geometry must contain complete triangles.", nameof(mesh));
        }
        if (startVertex < 0 || startVertex > mesh.Vertices.Length || startVertex % 3 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startVertex), "Terrain ranges must start at a triangle boundary inside the mesh.");
        }
        if (vertexCount == -1)
        {
            vertexCount = mesh.Vertices.Length - startVertex;
        }
        if (vertexCount < 0 || vertexCount > mesh.Vertices.Length - startVertex || vertexCount % 3 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexCount), "Terrain ranges must contain complete triangles inside the mesh.");
        }
        if (vertexCount == 0)
        {
            return;
        }
        if ((view.Ff7 is { Underwater: true }) != !mesh.Ff7Triangles.IsEmpty)
        {
            throw new InvalidDataException("FFVII underwater projection and retained triangle metadata must use the same vertex contract.");
        }
        Flush();
        VertexBuffer buffer = TerrainVertices(mesh);
        bool transient = mesh.Vertices.Length > MaximumCachedTerrainVertices;
        try
        {
            if (terrainView != view)
            {
                System.Numerics.Matrix4x4 matrix = view.View;
                effect.Parameters["WorldView"].SetValue(new Matrix(matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24, matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44));
                effect.Parameters["WorldOffset"].SetValue(new Vector3(view.Offset.X, view.Offset.Y, view.Offset.Z));
                effect.Parameters["WorldFocal"].SetValue(view.Focal);
                effect.Parameters["WorldNativeCurvature"].SetValue(view.NativeCurvature ? 1f : 0f);
                Ff7WorldTerrainView? ff7 = view.Ff7;
                effect.Parameters["WorldFf7Projection"].SetValue(ff7 is { } selected ? new Vector4(1, selected.CenterY, selected.CurvatureThreshold, selected.OffsetX) : Vector4.Zero);
                System.Numerics.Vector3? axis = ff7?.BridgeAxis;
                effect.Parameters["WorldFf7Bridge"].SetValue(axis is { } bridge ? new Vector4(bridge.X, bridge.Y, bridge.Z, 1) : Vector4.Zero);
                terrainView = view;
            }
            effect.CurrentTechnique = effect.Techniques[view.Ff7 is { Underwater: true } ? "Ff7UnderwaterTerrainTechnique" : "WorldTerrainTechnique"];
            SamplerState sampler = ApplyMaterial(material);
            RasterizerState rasterizer = view.Ff7 is null ? RasterizerState.CullNone : RasterizerState.CullCounterClockwise;
            if (!ReferenceEquals(device.RasterizerState, rasterizer))
            {
                device.RasterizerState = rasterizer;
            }
            if (!ReferenceEquals(boundTerrain, buffer))
            {
                device.SetVertexBuffer(buffer);
                boundTerrain = buffer;
            }
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.SamplerStates[0] = sampler;
                device.DrawPrimitives(PrimitiveType.TriangleList, startVertex, vertexCount / 3);
                FrameDrawCalls++;
            }
        }
        finally
        {
            ClearTextureBindings();
            ReleaseTransientTexture();
            if (transient)
            {
                UnbindTerrain();
                buffer.Dispose();
            }
        }
    }

    private void UnbindTerrain()
    {
        if (boundTerrain is not null)
        {
            device.SetVertexBuffer(null);
            device.RasterizerState = RasterizerState.CullNone;
            boundTerrain = null;
        }
    }

    private VertexBuffer TerrainVertices(WorldTerrainMesh mesh)
    {
        if (terrainBuffers.TryGetValue(mesh, out LinkedListNode<TerrainBuffer>? node))
        {
            node.Value.LastFrame = terrainFrame;
            terrainRecency.Remove(node);
            terrainRecency.AddLast(node);
            return node.Value.Buffer;
        }
        bool retained = mesh.Vertices.Length <= MaximumCachedTerrainVertices;
        if (retained)
        {
            while (terrainBuffers.Count >= MaximumCachedTerrainMeshes || cachedTerrainVertices + mesh.Vertices.Length > MaximumCachedTerrainVertices)
            {
                RemoveOldestTerrainBuffer();
            }
        }
        ReadOnlySpan<WorldTerrainVertex> source = mesh.Vertices.Span;
        bool underwater = !mesh.Ff7Triangles.IsEmpty;
        VertexBuffer buffer = new(device, underwater ? Ff7UnderwaterGpuVertex.Declaration : WorldGpuVertex.Declaration, source.Length, BufferUsage.WriteOnly);
        try
        {
            if (underwater)
            {
                if (mesh.Ff7Triangles.Length != source.Length)
                {
                    throw new InvalidDataException("FFVII underwater terrain requires metadata for every retained triangle corner.");
                }
                Ff7UnderwaterGpuVertex[] upload = new Ff7UnderwaterGpuVertex[source.Length];
                for (int index = 0; index < upload.Length; index++)
                {
                    upload[index] = new(source[index], mesh.Ff7Triangles.Span[index]);
                }
                buffer.SetData(upload);
            }
            else
            {
                WorldGpuVertex[] upload = new WorldGpuVertex[source.Length];
                for (int index = 0; index < upload.Length; index++)
                {
                    upload[index] = new(source[index]);
                }
                buffer.SetData(upload);
            }
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
        FrameTerrainUploads++;
        if (retained)
        {
            node = terrainRecency.AddLast(new TerrainBuffer(mesh, buffer, terrainFrame));
            terrainBuffers.Add(mesh, node);
            cachedTerrainVertices += source.Length;
            cachedTerrainBytes += source.Length * buffer.VertexDeclaration.VertexStride;
        }
        return buffer;
    }

    private void TrimTerrainBuffers()
    {
        while (terrainRecency.First is { } oldest && terrainFrame - oldest.Value.LastFrame >= TerrainRetentionFrames)
        {
            RemoveOldestTerrainBuffer();
        }
    }

    private void RemoveOldestTerrainBuffer()
    {
        TerrainBuffer oldest = terrainRecency.First!.Value;
        terrainRecency.RemoveFirst();
        terrainBuffers.Remove(oldest.Mesh);
        cachedTerrainVertices -= oldest.Mesh.Vertices.Length;
        cachedTerrainBytes -= oldest.Mesh.Vertices.Length * oldest.Buffer.VertexDeclaration.VertexStride;
        if (ReferenceEquals(boundTerrain, oldest.Buffer))
        {
            UnbindTerrain();
        }
        oldest.Buffer.Dispose();
    }

    private void DisposeTerrainBuffers()
    {
        while (terrainRecency.Count > 0)
        {
            RemoveOldestTerrainBuffer();
        }
    }

    private sealed class TerrainBuffer(WorldTerrainMesh mesh, VertexBuffer buffer, long lastFrame)
    {
        public WorldTerrainMesh Mesh { get; } = mesh;
        public VertexBuffer Buffer { get; } = buffer;
        public long LastFrame { get; set; } = lastFrame;
    }

    private readonly struct WorldGpuVertex : IVertexType
    {
        internal static readonly VertexDeclaration Declaration = new(new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0), new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0), new VertexElement(20, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0), new VertexElement(32, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1));
        private readonly Vector3 position;
        private readonly Vector2 uv;
        private readonly Vector3 normal;
        private readonly float deform;

        public VertexDeclaration VertexDeclaration => Declaration;

        public WorldGpuVertex(WorldTerrainVertex vertex)
        {
            position = new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z);
            uv = new Vector2(vertex.Uv.X, vertex.Uv.Y);
            normal = new Vector3(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z);
            deform = vertex.Deform;
        }
    }

    private readonly struct Ff7UnderwaterGpuVertex : IVertexType
    {
        internal static readonly VertexDeclaration Declaration = new(new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0), new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0), new VertexElement(20, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0), new VertexElement(32, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1), new VertexElement(36, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 2), new VertexElement(48, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 3), new VertexElement(60, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 4));
        private readonly WorldGpuVertex vertex;
        private readonly Vector3 next;
        private readonly Vector3 last;
        private readonly float corner;

        public VertexDeclaration VertexDeclaration => Declaration;

        public Ff7UnderwaterGpuVertex(WorldTerrainVertex source, Ff7WorldTriangleMetadata metadata)
        {
            vertex = new(source);
            next = new(metadata.Next.X, metadata.Next.Y, metadata.Next.Z);
            last = new(metadata.Last.X, metadata.Last.Y, metadata.Last.Z);
            corner = metadata.Corner;
        }
    }
}
