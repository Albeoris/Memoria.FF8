using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>Presentation history only; native spark counters and trail rings remain unchanged.</summary>
public sealed record DrawPointParticleBlend(NativeDrawPointParticles Previous, float Fraction)
{
    public void Validate()
    {
        Previous.Validate();
        if (!float.IsFinite(Fraction) || Fraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Fraction));
        }
    }

    public Vector3 Point(NativeDrawPointParticles current, NativeDrawPointSpark spark, int age, bool secondEdge)
    {
        Vector3 after = (secondEdge ? spark.SecondEdge : spark.FirstEdge)[(spark.RenderCounter - age) & 7].Vector;
        int index = current.Sparks.IndexOf(spark);
        if (index < 0 || Fraction == 1)
        {
            return after;
        }
        NativeDrawPointSpark before = Previous.Sparks[index];
        if (before.RenderStatus == 0 || before.RenderStatus != spark.RenderStatus || before.RenderCounter + 1 != spark.RenderCounter || before.Anchor != spark.Anchor || before.Control != spark.Control || before.Target != spark.Target || before.Duration != spark.Duration)
        {
            return after;
        }
        // Follow trail age, not physical ring slots: the head advances one slot per callback.
        Vector3 from = (secondEdge ? before.SecondEdge : before.FirstEdge)[(before.RenderCounter - age) & 7].Vector;
        return Vector3.Lerp(from, after, Fraction);
    }
}
