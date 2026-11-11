using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private Ff7BattleBillboardRenderer[]? silkSprites;

    private void DrawSilkSprite(ITriangleRenderer renderer, Ff7BattlePrimarySilkSpriteDraw sprite, CameraView view)
    {
        const string path = "ff7/data/battle/special/ito/";
        silkSprites ??=
        [
            LoadBillboard(path + "hit1.s", [path + "hit1.tex"], 7, _ => 0),
            LoadBillboard(path + "hit2.s", [path + "hit1.tex"], 7, _ => 0),
            LoadBillboard("ito1.s", ["ito1.tex"], 2, _ => 0)
        ];
        bool intro = sprite.Kind == 3;
        int frame = intro ? sprite.Frame % 2 : sprite.Frame;
        silkSprites[intro ? 2 : sprite.Kind].Draw(renderer, frame, view.Transform(sprite.Position.Position), view.Width, view.Height, view.Focal, intro ? 3 : 2, brightness: intro ? 0.75f : 1);
    }

    private static void DrawSilkLine(ITriangleRenderer renderer, Ff7BattlePrimarySilkLineDraw segment, CameraView view)
    {
        if (renderer is not ILineRenderer lines)
        {
            throw new NotSupportedException("FFVII Silk requires an ordered line-capable raster backend.");
        }
        Vector3 start = view.Transform(segment.Start.Position);
        Vector3 end = view.Transform(segment.End.Position);
        if (end.Z <= 0)
        {
            return;
        }
        Vector4 color = new(new Vector3(segment.Brightness / 255f), 1);
        TriangleVertex Project(Vector3 point)
        {
            CameraSpaceScreenVertex projected = CameraSpaceTriangleSubmission.Project(new(point, Vector2.Zero, color), view.Width, view.Height, view.Focal);
            return new(projected.Position, projected.ReciprocalZ, projected.ReciprocalZ, Vector2.Zero, color);
        }
        lines.DrawLine(Project(start), Project(end), new(null, TriangleBlend.Add, TriangleDepthComparison.GreaterEqual, false));
    }
}
