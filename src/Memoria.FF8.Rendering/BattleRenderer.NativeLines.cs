using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private void DrawNativePrimitiveLine(NativeBattlePrimitiveFrame frame, NativeBattleLogicalResolvedCommand resolved)
    {
        NativeBattleLogicalCommand command = resolved.Command;
        bool semi = (command.Opcode & 2) != 0;
        bool gouraud = (command.Opcode & 16) != 0;
        if (resolved.Pass == 1 && semi || resolved.Pass == 2 && !semi)
        {
            return;
        }
        if (command.Words.Length != (gouraud ? 4 : 3))
        {
            throw new InvalidDataException("Native LINE2 requires its original endpoint colors and packed XY words.");
        }
        bool perspective = !command.VertexPayload4.IsEmpty && command.VertexPayload4[0] != 0 && (frame.LastDescriptorPerspective ?? throw new NotSupportedException("Native LINE2 requires its executed descriptor depth mode."));
        NativeBattlePrimitiveDepthState depthState = frame.LastDepthState ?? throw new NotSupportedException("Native LINE2 requires its retained depth owner.");
        ImmutableArray<NativeBattlePrimitiveDepthVertex> projected = depthState.Project461220(command.OrderingBucket ?? throw new InvalidDataException("Native LINE2 lacks its original OT bucket."), perspective, command.Flags20, command.VertexPayload4);
        int blendMode = resolved.DrawMode.BlendMode ?? throw new NotSupportedException("Native LINE2 requires its executed ABR.");
        NativeBattlePrimitiveColorState colorState = frame.LastColorState ?? throw new NotSupportedException("Native LINE2 requires its retained diffuse color owner.");
        PrimitiveVertex a = Vertex(0);
        PrimitiveVertex b = Vertex(1);
        int blend = semi ? blendMode : -1;
        if (TriangleRenderer is { } renderer)
        {
            if (renderer is not ILineRenderer lines)
            {
                throw new NotSupportedException("Native LINE2 requires an ordered line-capable raster backend.");
            }
            TriangleBlend equation = blend switch
            {
                -1 => TriangleBlend.Replace,
                0 or 2 => TriangleBlend.Alpha,
                1 => TriangleBlend.Add,
                3 => TriangleBlend.AlphaAdd,
                _ => throw new NotSupportedException("Native LINE2 has no supported blend equation.")
            };
            TriangleMaterial material = new(null, equation, TriangleDepthComparison.LessEqual, resolved.Pass == 1, 0, TriangleSampler.PointWrap);
            lines.DrawLine(Convert(a), Convert(b), material);
            return;
        }
        nativeTriangles.Add(new(a, b, default, null, blend, resolved.Pass, 0, 0, Width - 1, 0, Height - 1, true));

        PrimitiveVertex Vertex(int corner)
        {
            int colorIndex = gouraud ? corner * 2 : 0;
            int coordinateIndex = gouraud ? corner * 2 + 1 : corner + 1;
            NativeBattlePrimitiveColor diffuse = colorState.Untextured(command.Words[colorIndex], semi, blendMode);
            Vector2 position = NativePrimitivePosition(command.Words[coordinateIndex], command.Flags20, resolved.DrawMode);
            return new(position.X, position.Y, projected[corner].Depth, projected[corner].ReciprocalW, Vector2.Zero, diffuse.Rgb, diffuse.Alpha);
        }

        static TriangleVertex Convert(PrimitiveVertex vertex) => new(new(vertex.X, vertex.Y), vertex.Z, vertex.ReciprocalW, vertex.Uv, new(vertex.Color / 255f, vertex.Alpha / 255f));
    }

    private Vector2 NativePrimitivePosition(uint coordinate, uint flags, NativeBattlePrimitiveDrawMode mode)
    {
        return NativePrimitivePosition(unchecked((short)coordinate), unchecked((short)(coordinate >> 16)), flags, mode);
    }

    private Vector2 NativePrimitivePosition(int signedX, int signedY, uint flags, NativeBattlePrimitiveDrawMode mode)
    {
        if (mode.NormalViewport is not null || mode.ViewportInputs is not null)
        {
            NativeSoftwareNormalViewportDerived viewport = mode.NormalViewport ?? throw new NotSupportedException("Native primitive viewport requires its retained derived outputs.");
            NativeSoftwareViewportInputs inputs = mode.ViewportInputs ?? throw new NotSupportedException("Native primitive viewport requires its actual input dimensions.");
            if (inputs.Width <= 0 || inputs.Height <= 0)
            {
                throw new InvalidDataException("Native primitive viewport requires positive dimensions.");
            }
            bool full = (flags & 1) != 0;
            float nativeX = (float)((double)signedX * (full ? viewport.ScaleX : viewport.LowCoordinateScaleX) + viewport.NormalOffsetX);
            float nativeY = (float)((double)signedY * (full ? viewport.ScaleY : viewport.LowCoordinateScaleY) + viewport.NormalOffsetY);
            return new(ProjectionBounds.X + (float)(((double)nativeX + 0.5) * ProjectionBounds.Width / inputs.Width), ProjectionBounds.Y + (float)(((double)nativeY + 0.5) * ProjectionBounds.Height / inputs.Height));
        }
        float unit = (flags & 1) != 0 ? 1 : 0.125f;
        return new(ProjectionBounds.X + signedX * unit * ProjectionBounds.Width / 320, ProjectionBounds.Y + signedY * unit * ProjectionBounds.Height / 216);
    }

    private void RasterNativeLine(NativeRasterTriangle line, int stripeTop, int stripeBottom)
    {
        PrimitiveVertex a = line.A;
        PrimitiveVertex b = line.B;
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        if (!float.IsFinite(a.X) || !float.IsFinite(a.Y) || !float.IsFinite(dx) || !float.IsFinite(dy))
        {
            return;
        }
        float first = 0;
        float last = 1;
        if (!Clip(-dx, a.X) || !Clip(dx, Width - a.X) || !Clip(-dy, a.Y) || !Clip(dy, Height - a.Y))
        {
            return;
        }
        float length = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (length == 0)
        {
            return;
        }
        int steps = Math.Max(1, (int)MathF.Ceiling(length * (last - first)));
        int previousX = int.MinValue;
        int previousY = int.MinValue;
        //Single-pixel, non-antialiased coverage with an excluded terminal endpoint.
        //Coordinates share the triangle renderer's half-pixel sample convention.
        for (int step = 0; step < steps; step++)
        {
            float t = first + (last - first) * step / steps;
            int x = (int)MathF.Floor(a.X + dx * t);
            int y = (int)MathF.Floor(a.Y + dy * t);
            if (x == previousX && y == previousY)
            {
                continue;
            }
            previousX = x;
            previousY = y;
            if ((uint)x >= Width || y < stripeTop || y > stripeBottom || (uint)y >= Height)
            {
                continue;
            }
            float z = a.Z + (b.Z - a.Z) * t;
            int index = y * Width + x;
            if (!float.IsFinite(z) || z < 0 || z > 1 || z > depth[index])
            {
                continue;
            }
            Vector3 color = a.Color + (b.Color - a.Color) * t;
            float alpha = (a.Alpha + (b.Alpha - a.Alpha) * t) / 255;
            BlendNativePcPrimitive(index * 4, color, alpha, line.Blend);
            if (line.Pass == 1)
            {
                depth[index] = z;
            }
        }

        bool Clip(float direction, float distance)
        {
            if (direction == 0)
            {
                return distance >= 0;
            }
            float t = distance / direction;
            if (direction < 0)
            {
                first = Math.Max(first, t);
            }
            else
            {
                last = Math.Min(last, t);
            }
            return first <= last;
        }
    }
}
