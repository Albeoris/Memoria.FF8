using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed record Ff7BattleArenaFace(NativeBattleArenaPrimitiveId Id, Vector3 A, Vector3 B, Vector3 C, NativeBattleForeignSurface Surface);
public sealed record Ff7BattleArenaGeometry(ImmutableArray<Ff7BattleArenaFace> Faces);

public sealed partial class Ff7BattleRenderer
{
    private Ff7BattleArenaGeometry? capturedArena;

    /// <summary>Original stage primitives in host world coordinates, with stable source identities and resolved materials.</summary>
    public Ff7BattleArenaGeometry CaptureArenaGeometry()
    {
        if (capturedArena is not null)
        {
            return capturedArena;
        }
        ImmutableArray<Ff7BattleArenaFace>.Builder faces = ImmutableArray.CreateBuilder<Ff7BattleArenaFace>();
        Ff7ModelMaterialState material = new() { Cull = true };
        for (int partIndex = 0; partIndex < arena.Geometry.Count; partIndex++)
        {
            PcPolygonModel model = arena.Geometry[partIndex].Polygon;
            if (model.Groups.Count != model.RenderStates.Count)
            {
                throw new InvalidDataException("FFVII battle P groups require their original material records.");
            }
            for (int groupIndex = 0; groupIndex < model.Groups.Count; groupIndex++)
            {
                PcModelGroup group = model.Groups[groupIndex];
                PcRenderState state = PcRenderState.Read(model.RenderStates[groupIndex].Span);
                material.Apply(state);
                PixelImage? image = null;
                if (group.TexturesUsed != 0)
                {
                    PcTexture texture = arenaTextures[group.TextureNumber];
                    if (!images.TryGetValue(texture, out image))
                    {
                        image = Ff7FieldRenderer.DecodeTexture(texture);
                        images.Add(texture, image);
                    }
                }
                NativeBattleForeignRenderMaterial surfaceMaterial = new(new(image, material.Blend, material.DepthTest ? TriangleDepthComparison.GreaterEqual : TriangleDepthComparison.Always, material.DepthWrite, material.AlphaReference, material.Sampler) { PreserveTargetAlpha = false, TextureSelectsAlpha = false }, material.Cull);
                for (int polygonIndex = group.PolygonStart; polygonIndex < group.PolygonStart + group.PolygonCount; polygonIndex++)
                {
                    PcModelPolygon polygon = model.Polygons[polygonIndex];
                    Vector4 first = Color(polygon.A);
                    NativeBattleForeignSurface surface = new(surfaceMaterial, [Uv(polygon.A), Uv(polygon.B), Uv(polygon.C)], [first, material.Gouraud ? Color(polygon.B) : first, material.Gouraud ? Color(polygon.C) : first]);
                    faces.Add(new(new(partIndex, groupIndex, polygonIndex), Vertex(polygon.A), Vertex(polygon.B), Vertex(polygon.C), surface));
                }
                Vector3 Vertex(int local) => model.Vertices[group.VertexStart + local];
                Vector2 Uv(int local) => image is null ? default : model.TextureCoordinates[group.TextureCoordinateStart + local];
                Vector4 Color(int local)
                {
                    int vertex = group.VertexStart + local;
                    uint argb = vertex < model.VertexColors.Count ? model.VertexColors[vertex] : uint.MaxValue;
                    return new((argb >> 16 & 255) / 255f, (argb >> 8 & 255) / 255f, (argb & 255) / 255f, (argb >> 24) / 255f * state.VertexAlpha / 255f);
                }
            }
        }
        capturedArena = new(faces.ToImmutable());
        return capturedArena;
    }

    /// <summary>Submits only the original arena into an independently selected render target and camera.</summary>
    public void RenderArena(ITriangleRenderer renderer, int width, int height, SceneCameraPlacement camera)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        CameraView view = CameraView.Create(camera, width, height);
        renderer.Begin(width, height, new Vector4(0, 0, 0, 1), 0);
        try
        {
            Ff7ModelMaterialState material = new() { Cull = true };
            foreach (Ff7BattleArenaPart part in arena.Geometry)
            {
                DrawModel(renderer, part.Polygon, arenaTextures, Matrix4x4.Identity, view, material);
            }
        }
        finally
        {
            renderer.End();
        }
    }
}
