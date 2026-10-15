namespace Memoria.FF8.Runtime;

/// <summary>Incoming direction in field input axes, without a historical position, speed or duration.</summary>
public sealed record ReplayActorApproach(float Horizontal, float Vertical, bool Inferred = false)
{
    public void Validate()
    {
        double lengthSquared = (double)Horizontal * Horizontal + (double)Vertical * Vertical;
        if (!float.IsFinite(Horizontal) || !float.IsFinite(Vertical) || lengthSquared <= 0 || lengthSquared > 1.000001)
        {
            throw new InvalidDataException("Actor approach requires a finite, nonzero direction with magnitude at most one.");
        }
    }
}
