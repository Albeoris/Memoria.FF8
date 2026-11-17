using System.Numerics;
using Memoria.FF8.Mods;

namespace Memoria.FF8.Rendering;

public enum FieldLightingPass
{
    None,
    Model,
    GroundShadow
}

public enum FieldActorShadowState
{
    Silhouette,
    IntentionalNone,
    NativeFallback
}

public readonly record struct FieldShadowTriangle(TriangleVertex A, TriangleVertex B, TriangleVertex C, TriangleMaterial Material);

/// <summary>Optional field-only hardware lighting. Call after Begin and before the ordered scene stream.</summary>
public interface IFieldLightingRenderer
{
    bool FieldLightingSupported { get; }
    string? FieldLightingUnavailableReason { get; }
    void SetFieldLighting(FieldLightingScene? scene);
    bool PrepareFieldShadow(IReadOnlyList<FieldShadowTriangle> casters, out string? reason);
}

/// <summary>Local linear-light compatibility boundary. The legacy compositor remains in its original color space.</summary>
public static class FieldLightingMath
{
    public static float SrgbToLinear(float value) => value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    public static float LinearToSrgb(float value) => value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1 / 2.4f) - 0.055f;
    public static Vector3 LinearColor(Vector3 color) => new(SrgbToLinear(color.X), SrgbToLinear(color.Y), SrgbToLinear(color.Z));

    /// <summary>Squared finite falloff (1 - distance/range)^2, times smoothstep between cone cosines.</summary>
    public static float Influence(FieldSpotLight light, Vector3 position)
    {
        if (!light.Enabled || light.Intensity <= 0 || light.Color == Vector3.Zero)
        {
            return 0;
        }
        Vector3 outward = position - light.Position;
        float distance = outward.Length();
        if (distance >= light.Range)
        {
            return 0;
        }
        float cosine = distance > 0.00001f ? Vector3.Dot(outward / distance, light.Direction) : 1;
        float inner = MathF.Cos(light.InnerConeDegrees * MathF.PI / 180);
        float outer = MathF.Cos(light.OuterConeDegrees * MathF.PI / 180);
        float cone = Math.Clamp((cosine - outer) / (inner - outer), 0, 1);
        cone *= cone * (3 - 2 * cone);
        float attenuation = 1 - distance / light.Range;
        return light.Intensity * attenuation * attenuation * cone;
    }

    public static Vector3 Illumination(FieldLightingScene scene, Vector3 position, Vector3 normal)
    {
        Vector3 result = Vector3.Zero;
        normal = normal.LengthSquared() > 0.000001f ? Vector3.Normalize(normal) : Vector3.Zero;
        foreach (FieldSpotLight light in scene.Lights)
        {
            Vector3 incoming = light.Position - position;
            float length = incoming.Length();
            float diffuse = length > 0.00001f ? Math.Max(0, Vector3.Dot(normal, incoming / length)) : 1;
            result += LinearColor(light.Color) * (Influence(light, position) * diffuse);
        }
        return result;
    }

    public static Matrix4x4 ShadowViewProjection(FieldSpotLight light)
    {
        Vector3 up = Math.Abs(Vector3.Dot(light.Direction, Vector3.UnitZ)) < 0.95f ? Vector3.UnitZ : Vector3.UnitY;
        float near = Math.Min(light.Range * 0.01f, Math.Max(0.001f, light.Range * 0.0001f));
        return Matrix4x4.CreateLookAt(light.Position, light.Position + light.Direction, up) * Matrix4x4.CreatePerspectiveFieldOfView(light.OuterConeDegrees * MathF.PI / 90, 1, near, light.Range);
    }
}
