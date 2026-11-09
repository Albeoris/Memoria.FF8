using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeModelEffectState
{
    public string Profile { get; init; } = Ff7NativeModelEffect.Profile;
    public int NativeScale { get; init; } = 512;
    public bool Initialized { get; init; }
    public bool Applied { get; init; }
    public bool ResetRequested { get; init; }
    public bool Restored { get; init; }
    public bool Completed { get; init; }
    public short Phase { get; init; }
    public short Counter { get; init; }
    public short Stage { get; init; }
    public int SampleFrame { get; init; }
    public bool SuppressesNativeLighting => Initialized && !Restored;

    public void Validate()
    {
        if (Profile is not (Ff7NativeModelEffect.Profile or Ff7NativeModelEffect.SinglePart35Profile or Ff7NativeModelEffect.SinglePartDefaultProfile or Ff7NativeModelEffect.SinglePart72_140Profile) || NativeScale is < short.MinValue or > short.MaxValue || Phase is < 0 or > 288 || Phase % 16 != 0 || Counter is < 0 or > 100 || Stage is < 0 or > 3 || SampleFrame is < 0 or > 18 || Applied && !Initialized || Completed && !Restored)
        {
            throw new InvalidDataException("The native KAWAI13 presentation continuation is invalid or unsupported.");
        }
        if (Profile == Ff7NativeModelEffect.SinglePart72_140Profile && (Phase % 32 != 0 || SampleFrame > 9))
        {
            throw new InvalidDataException("The native step32 procedural-color continuation has an invalid cache index or phase.");
        }
    }
}

/// <summary>The admitted native topology-selected procedural color profiles, independent of script PCs and battle RNG.</summary>
public static class Ff7NativeModelEffect
{
    public const string Profile = "pc-kawai13-four-part-16-42-v1";
    public const string SinglePart35Profile = "pc-kawai13-single-part-35-v1";
    public const string SinglePartDefaultProfile = "pc-kawai13-single-part-default-v1";
    public const string SinglePart72_140Profile = "pc-kawai13-single-part-72-140-v1";

    public static Ff7NativeModelEffectState Request(Ff7NativeModelEffectState? current, byte mode, IReadOnlyList<int> partVertexCounts, int nativeScale = 512, IReadOnlyList<int>? partPolygonCounts = null)
    {
        ArgumentNullException.ThrowIfNull(partVertexCounts);
        if (partVertexCounts.Count == 0 || partVertexCounts.Any(count => count <= 0) || partPolygonCounts is not null && (partPolygonCounts.Count != partVertexCounts.Count || partPolygonCounts.Any(count => count < 0)))
        {
            throw new InvalidDataException("Native procedural colors require valid rigid-part topology counts.");
        }
        // NG-022: this is the original topology-selected pulse profile, not a model-name inference.
        string profile;
        if (partVertexCounts.Count == 4 && partVertexCounts[0] == 16 && partVertexCounts[1] == 42)
        {
            profile = Profile;
        }
        else if (partVertexCounts.Count == 1 && partVertexCounts[0] == 35)
        {
            profile = SinglePart35Profile;
        }
        else if (partVertexCounts.Count == 1)
        {
            int vertices = partVertexCounts[0];
            if (vertices == 54)
            {
                throw new NotSupportedException("The native one-part54 procedural color profile requires its distinct phase contract.");
            }
            if (vertices is 112 or 109 or 94 or 95 or 72 or 80)
            {
                int polygons = partPolygonCounts?[0] ?? throw new NotSupportedException("The native procedural color classifier requires this part's polygon count.");
                if ((vertices is 112 or 109 or 94) && polygons == 64 || vertices == 95 && polygons == 62 || vertices == 80 && polygons == 76)
                {
                    throw new NotSupportedException("This native procedural color topology selects a distinct unsupported orbit or phase contract.");
                }
            }
            profile = vertices == 72 && partPolygonCounts![0] == 140 ? SinglePart72_140Profile : SinglePartDefaultProfile;
        }
        else
        {
            throw new NotSupportedException("KAWAI13 requires a proved native procedural-color topology profile.");
        }
        current?.Validate();
        if (current is not null && !current.Restored && current.Profile != profile)
        {
            throw new InvalidDataException("The native procedural-color continuation does not match the current model topology.");
        }
        Ff7NativeModelEffectState state = current is null || current.Restored ? new() { Profile = profile, NativeScale = nativeScale } : current;
        state = state with { ResetRequested = mode == 2, Completed = false };
        state.Validate();
        return state;
    }

    /// <summary>Call only at the native model-effect processing boundary. A rejected native render/depth gate preserves every field.</summary>
    public static Ff7NativeModelEffectState Advance(Ff7NativeModelEffectState state, bool modelEffectDrawEligible)
    {
        state.Validate();
        if (!modelEffectDrawEligible || state.Completed)
        {
            return state;
        }
        if (state.ResetRequested)
        {
            return state.Restored ? state with { Completed = true } : state with { Restored = true, Applied = false };
        }
        if (!state.Initialized)
        {
            return state with { Initialized = true };
        }
        Ff7NativeModelEffectState result = state with { Applied = true, SampleFrame = state.Phase / PhaseStep(state) };
        switch (state.Stage)
        {
            case 0:
                result = result with { Counter = (short)(state.Counter + 1) };
                if (result.Counter >= 100)
                {
                    result = result with { Phase = 0, Stage = 1 };
                }
                break;
            case 1:
                result = result with { Phase = (short)(state.Phase + PhaseStep(state)) };
                if (result.Phase >= 288)
                {
                    result = result with { Counter = 0, Stage = 2 };
                }
                break;
            case 2:
                result = result with { Counter = (short)(state.Counter + 1) };
                if (result.Counter >= 10)
                {
                    result = result with { Phase = 0, Stage = 3 };
                }
                break;
            case 3:
                result = result with { Phase = (short)(state.Phase + PhaseStep(state)) };
                if (result.Phase >= 288)
                {
                    result = result with { Counter = 0, Stage = 0 };
                }
                break;
        }
        result.Validate();
        return result;
    }

    public static bool PartVisible(Ff7NativeModelEffectState state, int part)
    {
        state.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(part);
        bool singlePart = state.Profile is SinglePart35Profile or SinglePartDefaultProfile or SinglePart72_140Profile;
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(part, singlePart ? 1 : 4);
        return !state.Initialized || state.Restored || state.Applied && (singlePart || part is 1 or 3);
    }

    public static Vector3 OrbitPoint(Ff7NativeModelEffectState state, int frame, IReadOnlyList<short> nativeSinCos)
    {
        state.Validate();
        if ((uint)frame > 288 / PhaseStep(state) || nativeSinCos.Count != 512 || nativeSinCos.Any(value => value is < -4096 or > 4096))
        {
            throw new InvalidDataException("KAWAI13 requires its native orbit frame and original sine/cosine table.");
        }
        int scale = state.NativeScale > 512 ? state.NativeScale / 512 : 1;
        int phase = frame * PhaseStep(state) & 255;
        int sine = nativeSinCos[phase * 2];
        int cosine = nativeSinCos[phase * 2 + 1];
        int xyShift = UsesDefaultOrbit(state) ? 4 : state.Profile == SinglePart35Profile ? 7 : 6;
        int zShift = state.Profile == SinglePart35Profile ? 6 : 5;
        int zOffset = UsesDefaultOrbit(state) ? 0 : 16;
        return new(unchecked((short)((cosine >> xyShift) * scale)), unchecked((short)((-sine >> xyShift) * scale)), unchecked((short)(((sine >> zShift) + zOffset) * scale)));
    }

    /// <summary>Reconstructs one native cached color from the original primitive position/normal and packed-camera rotation, excluding actor/model transforms.</summary>
    public static uint ShadeVertex(Ff7NativeModelEffectState state, int frame, IReadOnlyList<short> nativeSinCos, Vector3 primitivePosition, Vector3 primitiveNormal, uint originalArgb, Matrix4x4 packedCamera)
    {
        Vector3 orbit = OrbitPoint(state, frame, nativeSinCos);
        if (!Finite(primitivePosition) || !Finite(primitiveNormal) || !Matrix4x4.Invert(packedCamera, out Matrix4x4 inverseCamera))
        {
            throw new InvalidDataException("Native procedural colors require finite primitive data and an invertible packed camera.");
        }
        Vector3 point = RotateInteger(new(unchecked((short)(int)primitivePosition.X), unchecked((short)(int)primitivePosition.Y), unchecked((short)(int)primitivePosition.Z)));
        Vector3 center = RotateInteger(orbit);
        center = new(unchecked((short)(int)center.X), unchecked((short)(int)center.Y), unchecked((short)(int)center.Z));
        Vector3 delta = center - point;
        double squared = (double)delta.X * delta.X + (double)delta.Y * delta.Y + (double)delta.Z * delta.Z;
        if (squared > int.MaxValue)
        {
            throw new NotSupportedException("Native procedural-color distance exceeds its signed integer square-root contract.");
        }
        int distance = (int)Math.Sqrt((int)squared) + 2;
        int scale = state.NativeScale > 512 ? state.NativeScale / 512 : 1;
        int nativeRadius = UsesDefaultOrbit(state) ? 2048 : state.Profile == SinglePart35Profile ? 512 : 1024;
        int radius = unchecked((short)(nativeRadius * scale)) + 1;
        int falloff = unchecked(Math.Max(radius - distance, 0) << 12) / radius;
        Vector3 direction = new(Component((int)delta.X), Component((int)delta.Y), Component((int)delta.Z));
        direction = Vector3.TransformNormal(direction, inverseCamera);
        float light = MathF.Max(192 / 255f, Vector3.Dot(direction, primitiveNormal) + 192 / 255f);
        uint red = Channel((byte)(originalArgb >> 16));
        uint green = Channel((byte)(originalArgb >> 8));
        uint blue = Channel((byte)originalArgb);
        return originalArgb & 0xFF000000 | red << 16 | green << 8 | blue;

        Vector3 RotateInteger(Vector3 value)
        {
            Vector3 transformed = Vector3.TransformNormal(value, packedCamera);
            return new((int)transformed.X + (int)packedCamera.Translation.X, (int)transformed.Y + (int)packedCamera.Translation.Y, (int)transformed.Z + (int)packedCamera.Translation.Z);
        }

        int Component(int value)
        {
            float direction12 = (float)((double)unchecked(value << 12) / distance);
            return (int)((double)falloff * direction12) >> 12;
        }

        uint Channel(byte value)
        {
            float lit = MathF.Min(255, value * light);
            return (uint)MathF.Round(lit * (value / 255f), MidpointRounding.ToEven);
        }
    }

    private static int PhaseStep(Ff7NativeModelEffectState state) => state.Profile == SinglePart72_140Profile ? 32 : 16;
    private static bool UsesDefaultOrbit(Ff7NativeModelEffectState state) => state.Profile is SinglePartDefaultProfile or SinglePart72_140Profile;

    private static bool Finite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}
