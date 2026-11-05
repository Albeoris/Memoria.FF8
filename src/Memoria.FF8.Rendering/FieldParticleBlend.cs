using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>Completed native particle history used only while drawing between field callbacks.</summary>
public sealed record FieldParticleBlend(NativeFieldParticlesState PreviousState, IReadOnlyList<NativeFieldParticleSprite> Previous, float Fraction)
{
    public void Validate()
    {
        PreviousState.Validate();
        PreviousState.ValidateDrawings(Previous);
        if (!float.IsFinite(Fraction) || Fraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Fraction));
        }
    }

    public bool TryPrevious(NativeFieldParticleSprite current, out NativeFieldParticleSprite previous)
    {
        previous = current;
        if (Fraction == 1 || (uint)current.PoolIndex >= 128)
        {
            return false;
        }
        int slot = 10016 + current.PoolIndex * 32;
        // Emitters allocate before the next particle advance. An occupied slot in the
        // completed previous state cannot be reused during that next callback.
        // A final drawing may already have retired its slot, so drawings alone do not prove identity.
        if (PreviousState.Memory[slot + 27] != 1 || PreviousState.Memory[slot + 24] != current.TypeIndex)
        {
            return false;
        }
        foreach (NativeFieldParticleSprite candidate in Previous)
        {
            if (candidate.PoolIndex == current.PoolIndex && candidate.TypeIndex == current.TypeIndex)
            {
                previous = candidate;
                return true;
            }
        }
        return false;
    }

    public Vector2 Offset(Vector2 before, Vector2 after, short beforeAngle, short afterAngle)
    {
        if (Fraction == 0)
        {
            return before;
        }
        if (Fraction == 1)
        {
            return after;
        }
        int difference = ((afterAngle - beforeAngle + 2048) & 4095) - 2048;
        float radians = difference * (MathF.Tau / 4096);
        // Rotate both native endpoint offsets toward the intermediate orientation before
        // interpolating their extents. A half turn must not collapse the billboard.
        return Vector2.Lerp(Rotate(before, radians * Fraction), Rotate(after, -radians * (1 - Fraction)), Fraction);
    }

    private static Vector2 Rotate(Vector2 point, float angle)
    {
        float cosine = MathF.Cos(angle);
        float sine = MathF.Sin(angle);
        return new Vector2(point.X * cosine - point.Y * sine, point.X * sine + point.Y * cosine);
    }
}
