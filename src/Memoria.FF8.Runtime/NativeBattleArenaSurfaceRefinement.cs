using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleArenaSurfaceQueries
{
    private readonly record struct SurfacePoint(double X, double Z);
    private const int MaximumRefinedFaces = 65536;

    /// <summary>Overlays a nonoverlapping projected control mesh on the original surface, retaining the area outside that mesh and interpolating original materials.</summary>
    public static NativeBattleArenaGeometry Refine(NativeBattleArenaGeometry geometry, ImmutableArray<(Vector3 A, Vector3 B, Vector3 C)> controlTriangles)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (geometry.Faces.IsDefaultOrEmpty || controlTriangles.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Surface refinement requires original triangles and an actual control mesh.");
        }
        ImmutableArray<NativeBattleArenaFace>.Builder output = ImmutableArray.CreateBuilder<NativeBattleArenaFace>();
        foreach (NativeBattleArenaFace face in geometry.Faces)
        {
            if (!HorizontalAt(face, face.A.Y) || face.Surface.Uv.Length != 3 || face.Surface.Colors.Length != 3)
            {
                throw new NotSupportedException("Surface refinement requires horizontal triangles with three retained material corners.");
            }
            SurfacePoint[] original = [Point(face.A), Point(face.B), Point(face.C)];
            double area = Side(original[0], original[1], original[2]);
            if (area == 0 || !double.IsFinite(area))
            {
                throw new InvalidDataException("Surface refinement requires finite nondegenerate original triangles.");
            }
            List<List<SurfacePoint>> remaining = [original.ToList()];
            foreach (var control in controlTriangles)
            {
                SurfacePoint[] triangle = [Point(control.A), Point(control.B), Point(control.C)];
                double controlArea = Side(triangle[0], triangle[1], triangle[2]);
                if (controlArea == 0 || !double.IsFinite(controlArea))
                {
                    throw new InvalidDataException("Surface refinement control triangles must have finite nondegenerate ground projections.");
                }
                List<List<SurfacePoint>> next = [];
                foreach (List<SurfacePoint> polygon in remaining)
                {
                    if (!Overlap(polygon, triangle))
                    {
                        next.Add(polygon);
                        continue;
                    }
                    List<SurfacePoint> inside = polygon;
                    for (int edge = 0; edge < 3 && inside.Count >= 3; edge++)
                    {
                        SurfacePoint a = triangle[edge];
                        SurfacePoint b = triangle[(edge + 1) % 3];
                        List<SurfacePoint> outside = Clip(inside, a, b, controlArea < 0);
                        if (HasArea(outside))
                        {
                            next.Add(outside);
                        }
                        inside = Clip(inside, a, b, controlArea > 0);
                    }
                    Emit(inside);
                    if (next.Count + output.Count > MaximumRefinedFaces)
                    {
                        throw new NotSupportedException("The projected arena control mesh exceeds the bounded surface refinement budget.");
                    }
                }
                remaining = next;
            }
            foreach (List<SurfacePoint> polygon in remaining)
            {
                Emit(polygon);
            }

            void Emit(List<SurfacePoint> polygon)
            {
                for (int index = 1; index + 1 < polygon.Count; index++)
                {
                    SurfacePoint a = polygon[0];
                    SurfacePoint b = polygon[index];
                    SurfacePoint c = polygon[index + 1];
                    double selectedArea = Side(a, b, c);
                    if (selectedArea == 0)
                    {
                        continue;
                    }
                    if (Math.Sign(selectedArea) != Math.Sign(area))
                    {
                        (b, c) = (c, b);
                    }
                    Vector3 first = Vertex(a);
                    Vector3 second = Vertex(b);
                    Vector3 third = Vertex(c);
                    if (Cross(first, second, third) == 0)
                    {
                        continue;
                    }
                    NativeBattleForeignSurface surface = new(face.Surface.Material, [Uv(a), Uv(b), Uv(c)], [Color(a), Color(b), Color(c)]);
                    output.Add(new(face.Id, first, second, third, surface));
                    if (output.Count > MaximumRefinedFaces)
                    {
                        throw new NotSupportedException("The projected arena control mesh exceeds the bounded surface refinement budget.");
                    }
                }
            }
            Vector3 Vertex(SurfacePoint point) => new((float)point.X, face.A.Y, (float)point.Z);
            Vector3 Weights(SurfacePoint point) => new((float)(Side(original[1], original[2], point) / area), (float)(Side(original[2], original[0], point) / area), (float)(Side(original[0], original[1], point) / area));
            Vector2 Uv(SurfacePoint point)
            {
                Vector3 weights = Weights(point);
                return face.Surface.Uv[0] * weights.X + face.Surface.Uv[1] * weights.Y + face.Surface.Uv[2] * weights.Z;
            }
            Vector4 Color(SurfacePoint point)
            {
                Vector3 weights = Weights(point);
                return face.Surface.Colors[0] * weights.X + face.Surface.Colors[1] * weights.Y + face.Surface.Colors[2] * weights.Z;
            }
        }
        return new(output.ToImmutable());
    }

    private static SurfacePoint Point(Vector3 point) => new(point.X, point.Z);

    private static double Side(SurfacePoint a, SurfacePoint b, SurfacePoint point) => (b.X - a.X) * (point.Z - a.Z) - (b.Z - a.Z) * (point.X - a.X);

    private static List<SurfacePoint> Clip(List<SurfacePoint> polygon, SurfacePoint a, SurfacePoint b, bool positive)
    {
        List<SurfacePoint> output = [];
        SurfacePoint previous = polygon[^1];
        double previousSide = Side(a, b, previous);
        bool previousInside = positive ? previousSide >= 0 : previousSide <= 0;
        foreach (SurfacePoint current in polygon)
        {
            double currentSide = Side(a, b, current);
            bool currentInside = positive ? currentSide >= 0 : currentSide <= 0;
            if (previousInside != currentInside)
            {
                double fraction = previousSide / (previousSide - currentSide);
                Add(new(previous.X + (current.X - previous.X) * fraction, previous.Z + (current.Z - previous.Z) * fraction));
            }
            if (currentInside)
            {
                Add(current);
            }
            previous = current;
            previousSide = currentSide;
            previousInside = currentInside;
        }
        if (output.Count > 1 && output[0] == output[^1])
        {
            output.RemoveAt(output.Count - 1);
        }
        return output;

        void Add(SurfacePoint point)
        {
            if (output.Count == 0 || output[^1] != point)
            {
                output.Add(point);
            }
        }
    }

    private static bool HasArea(List<SurfacePoint> polygon)
    {
        for (int index = 1; index + 1 < polygon.Count; index++)
        {
            if (Side(polygon[0], polygon[index], polygon[index + 1]) != 0)
            {
                return true;
            }
        }
        return false;
    }

    private static bool Overlap(List<SurfacePoint> polygon, SurfacePoint[] triangle) => polygon.Min(point => point.X) <= triangle.Max(point => point.X) && polygon.Max(point => point.X) >= triangle.Min(point => point.X) && polygon.Min(point => point.Z) <= triangle.Max(point => point.Z) && polygon.Max(point => point.Z) >= triangle.Min(point => point.Z);
}
