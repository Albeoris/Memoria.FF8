using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private void DrawNativePrimitiveLine(NativeBattlePreparedPrimitiveFrame frame, in NativeBattleLogicalCommandView command, NativeBattlePrimitiveDrawMode drawMode, byte pass)
    {
        bool semi = (command.Opcode & 2) != 0;
        bool gouraud = (command.Opcode & 16) != 0;
        if (pass == 1 && semi || pass == 2 && !semi)
        {
            return;
        }
        if (command.WordSpan.Length != (gouraud ? 4 : 3))
        {
            throw new InvalidDataException("Native LINE2 requires its original endpoint colors and packed XY words.");
        }
        ReadOnlySpan<int> depths = command.VertexPayloadSpan;
        bool perspective = !depths.IsEmpty && depths[0] != 0 && (frame.LastDescriptorPerspective ?? throw new NotSupportedException("Native LINE2 requires its executed descriptor depth mode."));
        NativeBattlePrimitiveDepthState depthState = frame.LastDepthState ?? throw new NotSupportedException("Native LINE2 requires its retained depth owner.");
        Span<NativeBattlePrimitiveDepthVertex> projected = stackalloc NativeBattlePrimitiveDepthVertex[4];
        depthState.Project461220(command.OrderingBucket ?? throw new InvalidDataException("Native LINE2 lacks its original OT bucket."), perspective, command.Flags20, depths, projected);
        int blendMode = drawMode.BlendMode ?? throw new NotSupportedException("Native LINE2 requires its executed ABR.");
        NativeBattlePrimitiveColorState colorState = frame.LastColorState ?? throw new NotSupportedException("Native LINE2 requires its retained diffuse color owner.");
        NativeBattlePilotPrimitiveSample pilot = PreparePilotPrimitiveSample(command, depthState, perspective);
        NativeBattlePrimitiveViewportProjector viewport = default;
        PrimitiveVertex a = Vertex(command, pilot, 0, projected[0]);
        PrimitiveVertex b = Vertex(command, pilot, 1, projected[1]);
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
            TriangleMaterial material = new(null, equation, TriangleDepthComparison.LessEqual, pass == 1, 0, TriangleSampler.PointWrap);
            material = WithNativeActorOpacity(material, blend, frame.ActorOpacity(command.PresentationActorId));
            lines.DrawLine(Convert(a), Convert(b), material);
            ObserveLine(command);
            return;
        }
        nativeTriangles.Add(new(a, b, default, null, blend, pass, 0, 0, Width - 1, 0, Height - 1, true, frame.ActorOpacity(command.PresentationActorId)));
        ObserveLine(command);

        PrimitiveVertex Vertex(in NativeBattleLogicalCommandView command, in NativeBattlePilotPrimitiveSample pilot, int corner, NativeBattlePrimitiveDepthVertex depth)
        {
            int colorIndex = gouraud ? corner * 2 : 0;
            int coordinateIndex = gouraud ? corner * 2 + 1 : corner + 1;
            NativeBattlePrimitiveColor diffuse = colorState.Untextured(command.WordSpan[colorIndex], semi, blendMode);
            uint coordinate = command.WordSpan[coordinateIndex];
            if (!viewport.IsPrepared)
            {
                viewport = new(ProjectionBounds, command.Flags20, drawMode);
            }
            if (TrySamplePilotCorner(pilot, viewport, corner, out Vector2 position, out NativeBattlePrimitiveDepthVertex sampledDepth))
            {
                depth = sampledDepth;
            }
            else
            {
                position = viewport.Project(unchecked((short)coordinate), unchecked((short)(coordinate >> 16)));
            }
            return new(position.X, position.Y, depth.Depth, depth.ReciprocalW, Vector2.Zero, diffuse.Rgb, diffuse.Alpha);
        }

        void ObserveLine(in NativeBattleLogicalCommandView command)
        {
            if (!PilotDiagnosticsEnabled || command.Pilot is not { IsEmpty: false } geometry || !NativeBattleSmoothPresentation.Supports(geometry.Identity.Effect))
            {
                return;
            }
            pilotSubmission = pilotSubmission with { MaterialSignature = PilotHash(PilotHash(pilotSubmission.MaterialSignature, command.Opcode), pass) };
            ObservePilotSubmittedCorner(command, 0, new(a.X, a.Y), new(a.Z, a.ReciprocalW), a.Uv, a.Color, a.Alpha);
            ObservePilotSubmittedCorner(command, 1, new(b.X, b.Y), new(b.Z, b.ReciprocalW), b.Uv, b.Color, b.Alpha);
        }

        static TriangleVertex Convert(PrimitiveVertex vertex) => new(new(vertex.X, vertex.Y), vertex.Z, vertex.ReciprocalW, vertex.Uv, new(vertex.Color / 255f, vertex.Alpha / 255f));
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
            BlendNativePcPrimitive(index * 4, color, alpha, line.Blend, line.ActorOpacity);
            if (line.Pass == 1 && line.ActorOpacity == 1)
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
