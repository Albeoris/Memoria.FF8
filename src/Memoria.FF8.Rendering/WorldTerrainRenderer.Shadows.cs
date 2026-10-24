using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class WorldTerrainRenderer
{
    private void DrawWorldShadows(IReadOnlyList<NativeWorldShadowDraw>? shadows)
    {
        if (shadows is null)
        {
            return;
        }
        foreach (NativeWorldShadowDraw shadow in shadows)
        {
            WorldShadowGeometry geometry = WorldShadowPresentation.Create(shadow, assets.ReadNativeContactShape(shadow.ShapeId));
            PixelImage texture = assets.ReadNativeSectionTexture(38, geometry.TextureMember);
            foreach (WorldShadowTriangle triangle in geometry.Triangles)
            {
                Vertex Convert(WorldShadowVertex vertex)
                {
                    return new(vertex.Position / 16, vertex.Uv);
                }
                Draw(Convert(triangle.A), Convert(triangle.B), Convert(triangle.C), texture, true, TriangleBlend.Subtract);
            }
        }
    }
}
