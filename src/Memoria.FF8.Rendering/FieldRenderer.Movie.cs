using System.Numerics;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class FieldRenderer
{
    private PixelImage? movieImage;
    /// <summary>Composites the current original CAM/MSK and actors over the actual decoded movie pixels.</summary>
    public byte[] RenderMovieFrame(ReadOnlySpan<byte> moviePixels, int movieWidth, int movieHeight, FieldCamera movieCamera, IEnumerable<ActorVisual> actors, FieldMovieMask? mask)
    {
        if (movieWidth < 1 || movieHeight < 1 || moviePixels.Length != checked(movieWidth * movieHeight * 4))
        {
            throw new InvalidDataException("Movie compositing requires a complete actual decoded RGBA frame.");
        }
        ActorVisual[] visible = actors.Where(actor => actor.Visible && (uint)actor.ModelId < field.Models.Count).ToArray();
        foreach (ActorVisual actor in visible.Where(actor => field.Models[actor.ModelId].Bones.Count > 0 && field.Models[actor.ModelId].Faces.Count > 0))
        {
            if (actor.PolygonColor is not Vector3 color || new[] { color.X, color.Y, color.Z }.Any(value => !float.IsFinite(value) || value < 0 || value > 255 || value != MathF.Truncate(value)))
            {
                throw new NotSupportedException("Movie model drawing requires the original primitive RGB bytes.");
            }
        }
        //476450's active-movie branch subtracts CAM WORD32, adds WORD34, and does not apply the ordinary tracking/shake offsets.
        camera = movieCamera with { Pan = new Vector2(-movieCamera.Pan.X, movieCamera.Pan.Y) };
        actorScrollOffset = Vector2.Zero;
        if (TriangleRenderer is not null)
        {
            TriangleRenderer.Begin(OutputWidth, OutputHeight, new Vector4(0, 0, 0, 1));
            if (movieImage is null || movieImage.Width != movieWidth || movieImage.Height != movieHeight)
            {
                movieImage = new PixelImage(movieWidth, movieHeight);
            }
            MemoryMarshal.Cast<byte, Rgba32>(moviePixels).CopyTo(movieImage.Pixels);
            TriangleMaterial material = GpuMaterial(movieImage, TriangleBlend.Replace, false) with
            {
                DepthComparison = TriangleDepthComparison.Always,
                Sampler = TriangleSampler.LinearClamp,
                DynamicTexture = true
            };
            GpuQuad(new Vector2(offsetX, offsetY), new Vector2(Width * scale, NativeDrawingHeight * scale), 0, Vector4.One, material, Vector2.One);
        }
        else
        {
            Array.Clear(pixels);
            CopyMovieFrame(moviePixels, movieWidth, movieHeight);
            Array.Fill(depth, float.PositiveInfinity);
        }
        if (mask is not null)
        {
            foreach (FieldMovieMaskTriangle triangle in mask.Triangles)
            {
                DrawMovieMaskTriangle(ToViewport(camera.Project(triangle.A)), ToViewport(camera.Project(triangle.B)), ToViewport(camera.Project(triangle.C)));
            }
        }
        List<(float Depth, Action Draw)> translucent = [];
        foreach (ActorVisual actor in visible)
        {
            DrawActorShadow(actor);
            DrawActor(actor, translucent);
        }
        foreach (var primitive in translucent.OrderByDescending(primitive => primitive.Depth))
        {
            primitive.Draw();
        }
        TriangleRenderer?.End();
        return pixels;
    }

    private void CopyMovieFrame(ReadOnlySpan<byte> source, int width, int height)
    {
        for (int y = clipTop; y < clipBottom; y++)
        {
            float sourceY = Math.Clamp((y + 0.5f - offsetY) * height / (NativeDrawingHeight * scale) - 0.5f, 0, height - 1);
            int firstY = (int)sourceY;
            int secondY = Math.Min(firstY + 1, height - 1);
            float fractionY = sourceY - firstY;
            for (int x = clipLeft; x < clipRight; x++)
            {
                float sourceX = Math.Clamp((x + 0.5f - offsetX) * width / (Width * scale) - 0.5f, 0, width - 1);
                int firstX = (int)sourceX;
                int secondX = Math.Min(firstX + 1, width - 1);
                float fractionX = sourceX - firstX;
                int destination = (y * OutputWidth + x) * 4;
                for (int channel = 0; channel < 3; channel++)
                {
                    float top = source[(firstY * width + firstX) * 4 + channel] * (1 - fractionX) + source[(firstY * width + secondX) * 4 + channel] * fractionX;
                    float bottom = source[(secondY * width + firstX) * 4 + channel] * (1 - fractionX) + source[(secondY * width + secondX) * 4 + channel] * fractionX;
                    pixels[destination + channel] = (byte)Math.Clamp(MathF.Round(top * (1 - fractionY) + bottom * fractionY), 0, 255);
                }
                pixels[destination + 3] = 255;
            }
        }
    }

    private void DrawMovieMaskTriangle(ProjectedPoint a, ProjectedPoint b, ProjectedPoint c)
    {
        //475300 submits black additive MSK triangles with explicit depth10 and packet flag4.
        //The OT reverses its E7 state packets: state1 selects the additive mask material (465CE0/464220).
        //Black leaves the decoded movie color intact; writing depth prevents the actors behind the mask from covering it.
        //Do not paint black over the movie or replace this prepass with the late CAM byte: that byte belongs to the next draw.
        float area = Edge(a, b, c.X, c.Y);
        if (Math.Abs(area) < 0.001f)
        {
            return;
        }
        if (TriangleRenderer is not null)
        {
            TriangleMaterial material = GpuMaterial(null, TriangleBlend.Replace, true) with { ColorWrite = false };
            TriangleRenderer.Draw(GpuVertex(a, Vector2.Zero, Vector4.One, 10, false), GpuVertex(b, Vector2.Zero, Vector4.One, 10, false), GpuVertex(c, Vector2.Zero, Vector4.One, 10, false), material);
            return;
        }
        int left = Math.Max(clipLeft, (int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
        int right = Math.Min(clipRight - 1, (int)MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
        int top = Math.Max(clipTop, (int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
        int bottom = Math.Min(clipBottom - 1, (int)MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                float first = Edge(b, c, x + 0.5f, y + 0.5f) / area;
                float second = Edge(c, a, x + 0.5f, y + 0.5f) / area;
                if (first >= 0 && second >= 0 && first + second <= 1)
                {
                    depth[y * OutputWidth + x] = Math.Min(depth[y * OutputWidth + x], 10);
                }
            }
        }
    }
}
