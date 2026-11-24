using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    public static Vector3[] MeasureModelPreview(CrossoverFf8BattleActorVisual visual)
    {
        List<Vector3> points = [];
        CrossoverFf8BattleActorRenderer.VisitGeometry(visual, (mesh, _, bones, _) => points.AddRange(CrossoverFf8BattleActorRenderer.WorldVertices(visual, mesh, bones)));
        return points.ToArray();
    }

    public byte[] RenderModelPreview(CrossoverFf8BattleActorVisual visual, BattleCamera camera)
    {
        Begin(camera, transparentBackground: true);
        CrossoverFf8BattleActorRenderer.VisitGeometry(visual, (mesh, textures, bones, _) =>
        {
            Draw(mesh, CrossoverFf8BattleActorRenderer.WorldVertices(visual, mesh, bones), textures, 1);
        });
        return pixels;
    }
}
