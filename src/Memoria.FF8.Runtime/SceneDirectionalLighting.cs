using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>Directional shading on an authored visible surface. The receiver supplies a normal in the direction's coordinate space.</summary>
public readonly record struct SceneDirectionalLighting(Vector3 Direction, float Strength)
{
    public float Multiplier(Vector3 surfaceNormal)
    {
        if (Strength == 0 || surfaceNormal.LengthSquared() <= 1e-12f)
        {
            return 1;
        }
        float towards = Math.Clamp(Vector3.Dot(Vector3.Normalize(surfaceNormal), Direction), -1, 1);
        return 1 - Strength * (0.5f - 0.5f * towards);
    }

    public static SceneDirectionalLighting Resolve(SceneLightingState? state)
    {
        if (state?.Look is not { } look || look.Sun <= 0)
        {
            return default;
        }
        double phase = 0.5;
        if (state.Time == SceneTime.Cycle)
        {
            double seconds = state.Cycle.Wrap(state.ClockSeconds);
            phase = seconds < state.Cycle.Day ? seconds / state.Cycle.Day : (seconds - state.Cycle.Day) / state.Cycle.Night;
        }
        float azimuth = look.SunAzimuth * MathF.PI / 180;
        float height = look.SunHeight * MathF.PI / 180;
        float angle = (float)phase * MathF.PI;
        Vector3 rise = new(MathF.Cos(azimuth), 0, MathF.Sin(azimuth));
        Vector3 up = -Vector3.UnitY;
        Vector3 lean = Vector3.Cross(up, rise);
        if (lean.Z > 0)
        {
            lean = -lean;
        }
        Vector3 direction = Vector3.Normalize(rise * MathF.Cos(angle) + (up * MathF.Sin(height) + lean * MathF.Cos(height)) * MathF.Sin(angle));
        float elevation = Math.Clamp(-direction.Y / 0.17f, 0, 1);
        float strength = look.Sun * elevation * elevation * (3 - 2 * elevation);
        return new(direction, strength);
    }
}
