using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Assets;

public readonly record struct FieldSceneLandmark(Vector3 Asset, Vector3 Field);

public sealed record FieldSceneCalibrationResult(Matrix4x4 AssetToField, float Scale, float RmsFieldUnits, float MaximumFieldUnits);

public static class FieldSceneCalibration
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };

    private sealed record LandmarkRow(float[] Asset, float[] Field);

    public static FieldSceneLandmark[] ReadLandmarks(ReadOnlySpan<byte> source)
    {
        if (source.Length > 256 * 1024 || JsonSerializer.Deserialize<LandmarkRow[]>(source, Options) is not { Length: >= 3 and <= 128 } rows)
        {
            throw new InvalidDataException("Supply a JSON array of 3–128 landmark pairs, with asset and field XYZ coordinates.");
        }
        return rows.Select(row => row is null ? throw new InvalidDataException("A landmark row cannot be null.") : new FieldSceneLandmark(ReadVector(row.Asset), ReadVector(row.Field))).ToArray();
    }

    // The first three correspondences establish an oriented basis. All supplied points contribute to scale and translation.
    public static FieldSceneCalibrationResult Fit(IReadOnlyList<FieldSceneLandmark> points)
    {
        if (points.Count is < 3 or > 128 || points.Any(point => !Valid(point.Asset) || !Valid(point.Field)))
        {
            throw new InvalidDataException("Calibration requires 3–128 finite, bounded landmark pairs.");
        }
        Matrix4x4 assetFrame = Basis(points[0].Asset, points[1].Asset, points[2].Asset);
        Matrix4x4 fieldFrame = Basis(points[0].Field, points[1].Field, points[2].Field);
        Matrix4x4 orientation = Matrix4x4.Transpose(assetFrame) * fieldFrame;
        Vector3 assetCenter = Vector3.Zero;
        Vector3 fieldCenter = Vector3.Zero;
        foreach (FieldSceneLandmark point in points)
        {
            assetCenter += point.Asset / points.Count;
            fieldCenter += point.Field / points.Count;
        }

        double numerator = 0;
        double denominator = 0;
        foreach (FieldSceneLandmark point in points)
        {
            Vector3 delta = point.Asset - assetCenter;
            numerator += Vector3.Dot(Vector3.TransformNormal(delta, orientation), point.Field - fieldCenter);
            denominator += delta.LengthSquared();
        }
        float scale = (float)(numerator / denominator);
        if (!float.IsFinite(scale) || scale <= 0)
        {
            throw new InvalidDataException("Landmark correspondence does not define a positive uniform scale.");
        }
        Matrix4x4 placement = orientation * Matrix4x4.CreateScale(scale);
        placement.Translation = fieldCenter - Vector3.TransformNormal(assetCenter, placement);
        double sum = 0;
        float maximum = 0;
        foreach (FieldSceneLandmark point in points)
        {
            float error = Vector3.Distance(Vector3.Transform(point.Asset, placement), point.Field);
            sum += (double)error * error;
            maximum = MathF.Max(maximum, error);
        }
        return new(placement, scale, (float)Math.Sqrt(sum / points.Count), maximum);
    }

    private static Matrix4x4 Basis(Vector3 origin, Vector3 second, Vector3 third)
    {
        Vector3 edge = second - origin;
        Vector3 other = third - origin;
        Vector3 cross = Vector3.Cross(edge, other);
        if (edge.LengthSquared() < 1e-12f || other.LengthSquared() < 1e-12f || cross.LengthSquared() <= edge.LengthSquared() * other.LengthSquared() * 1e-6)
        {
            throw new InvalidDataException("The first three landmarks must be distinct and non-collinear in both spaces.");
        }
        Vector3 x = Vector3.Normalize(edge);
        Vector3 z = Vector3.Normalize(cross);
        Vector3 y = Vector3.Cross(z, x);
        return new(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1);
    }

    private static Vector3 ReadVector(float[] values) => values is { Length: 3 } ? new(values[0], values[1], values[2]) : throw new InvalidDataException("Landmark coordinates require exactly three values.");
    private static bool Valid(Vector3 point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z) &&
        MathF.Abs(point.X) <= 1e8f && MathF.Abs(point.Y) <= 1e8f && MathF.Abs(point.Z) <= 1e8f;
}
