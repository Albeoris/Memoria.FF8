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
    bool PrepareFieldShadow(IReadOnlyList<FieldShadowTriangle> casters, out string? reason, IReadOnlyList<Vector3>? receivers = null);
}

/// <summary>Local linear-light compatibility boundary. The legacy compositor remains in its original color space.</summary>
public static class FieldLightingMath
{
    public static float SrgbToLinear(float value) => value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    public static float LinearToSrgb(float value) => value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1 / 2.4f) - 0.055f;
    public static Vector3 LinearColor(Vector3 color) => new(SrgbToLinear(color.X), SrgbToLinear(color.Y), SrgbToLinear(color.Z));

    /// <summary>Constant intensity for sunlight. Spots use squared distance falloff and a smooth cone.</summary>
    public static float Influence(FieldLight light, Vector3 position)
    {
        if (!light.Enabled || light.Intensity <= 0 || light.Color == Vector3.Zero)
        {
            return 0;
        }
        if (light.IsSunlight)
        {
            return light.Intensity;
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
        foreach (FieldLight light in scene.Lights)
        {
            Vector3 incoming = light.IsSunlight ? -light.Direction : light.Position - position;
            float length = incoming.Length();
            float diffuse = length > 0.00001f ? Math.Max(0, Vector3.Dot(normal, incoming / length)) : 1;
            result += LinearColor(light.Color) * (Influence(light, position) * diffuse);
        }
        return result;
    }

    public static Matrix4x4 ShadowViewProjection(FieldLight light)
    {
        Vector3 up = Math.Abs(Vector3.Dot(light.Direction, Vector3.UnitZ)) < 0.95f ? Vector3.UnitZ : Vector3.UnitY;
        float near = Math.Min(light.Range * 0.01f, Math.Max(0.001f, light.Range * 0.0001f));
        return Matrix4x4.CreateLookAt(light.Position, light.Position + light.Direction, up) * Matrix4x4.CreatePerspectiveFieldOfView(light.OuterConeDegrees * MathF.PI / 90, 1, near, light.Range);
    }

    public static Matrix4x4 SunlightShadowViewProjection(FieldLight light, IReadOnlyList<FieldShadowTriangle> casters, IReadOnlyList<Vector3> receivers)
    {
        Vector3 up = Math.Abs(Vector3.Dot(light.Direction, Vector3.UnitZ)) < 0.95f ? Vector3.UnitZ : Vector3.UnitY;
        Vector3 origin = casters[0].A.WorldPosition;
        Matrix4x4 view = Matrix4x4.CreateTranslation(-origin) * Matrix4x4.CreateLookAt(Vector3.Zero, light.Direction, up);
        Vector3 min = Vector3.Zero;
        Vector3 max = Vector3.Zero;
        foreach (FieldShadowTriangle caster in casters)
        {
            IncludeCaster(caster.A.WorldPosition);
            IncludeCaster(caster.B.WorldPosition);
            IncludeCaster(caster.C.WorldPosition);
        }
        float minZ = min.Z;
        foreach (Vector3 receiver in receivers)
        {
            minZ = Math.Min(minZ, Vector3.Transform(receiver, view).Z);
        }
        // Keep silhouettes away from the texture edge for the nine-tap shadow filter.
        float padX = Math.Max(1, (max.X - min.X) * 0.01f);
        float padY = Math.Max(1, (max.Y - min.Y) * 0.01f);
        view *= Matrix4x4.CreateTranslation(0, 0, -max.Z - 1);
        return view * Matrix4x4.CreateOrthographicOffCenter(min.X - padX, max.X + padX, min.Y - padY, max.Y + padY, 0, max.Z - minZ + 2);

        void IncludeCaster(Vector3 position)
        {
            Vector3 local = Vector3.Transform(position, view);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }
    }
}
