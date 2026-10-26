using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private void DrawNativePcUi(NativeBattlePcUiFrame? frame, Func<int, int, bool, PixelImage>? resolveTexture, bool semi, bool rasterImmediately = false)
    {
        if (frame is null)
        {
            return;
        }
        if (frame.SourceWidth <= 0 || frame.SourceHeight <= 0)
        {
            throw new InvalidDataException("Native PC UI requires its selected positive device dimensions.");
        }
        IEnumerable<NativeBattlePcUiQuad> quads = semi ? frame.Semi.SelectMany(batch => batch.Quads) : frame.Opaque;
        int first = nativeTriangles.Count;
        foreach (NativeBattlePcUiQuad quad in quads)
        {
            if (quad.Vertices.IsDefault || quad.Vertices.Length != 4)
            {
                throw new InvalidDataException("Native PC UI requires exactly four transformed vertices.");
            }
            PixelImage texture = (resolveTexture ?? throw new NotSupportedException("Native PC UI requires its initialized texture/palette resource owner."))(quad.Page, quad.Palette, semi);
            NativeBattlePcUiVertex a = quad.Vertices[0];
            NativeBattlePcUiVertex d = quad.Vertices[3];
            NativeBattlePcUiViewport clip = quad.Viewport;
            float left = Math.Max(a.X + 0.5f, clip.X);
            float top = Math.Max(a.Y + 0.5f, clip.Y);
            float right = (float)Math.Min(d.X + 0.5f, (double)clip.X + clip.Width);
            float bottom = (float)Math.Min(d.Y + 0.5f, (double)clip.Y + clip.Height);
            if (right <= left || bottom <= top)
            {
                continue;
            }
            Vector3 color = new((quad.DiffuseArgb >> 16) & 255, (quad.DiffuseArgb >> 8) & 255, quad.DiffuseArgb & 255);
            PrimitiveVertex At(float x, float y)
            {
                double horizontal = (x - (double)a.X - 0.5) / (d.X - (double)a.X);
                double vertical = (y - (double)a.Y - 0.5) / (d.Y - (double)a.Y);
                return new(ProjectionBounds.X + x * ProjectionBounds.Width / frame.SourceWidth, ProjectionBounds.Y + y * ProjectionBounds.Height / frame.SourceHeight, a.Z, 1, new((float)(a.U + (d.U - (double)a.U) * horizontal), (float)(a.V + (d.V - (double)a.V) * vertical)), color, quad.DiffuseArgb >> 24);
            }
            PrimitiveVertex firstVertex = At(left, top);
            PrimitiveVertex secondVertex = At(left, bottom);
            PrimitiveVertex thirdVertex = At(right, top);
            PrimitiveVertex fourthVertex = At(right, bottom);
            QueueNativePrimitive(firstVertex, secondVertex, thirdVertex, texture, semi ? 0 : -1, semi ? (byte)4 : (byte)3);
            QueueNativePrimitive(secondVertex, fourthVertex, thirdVertex, texture, semi ? 0 : -1, semi ? (byte)4 : (byte)3);
        }
        if (TriangleRenderer is null && rasterImmediately)
        {
            for (int index = first; index < nativeTriangles.Count; index++)
            {
                RasterNativePrimitive(nativeTriangles[index], 0, Height - 1);
            }
        }
    }
}
