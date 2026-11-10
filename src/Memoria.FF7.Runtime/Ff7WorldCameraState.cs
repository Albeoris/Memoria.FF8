using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldCameraState(int Heading = 0, int Pitch = 1500, int Distance = 10000, int GroundHeight = 0, int Roll = 0, int Focal = 400);

/// <summary>Advances the native normal camera's profile smoothing outside rendering.</summary>
public sealed class Ff7WorldCamera(IReadOnlyList<Ff7WorldCameraProfile> profiles)
{
    public Ff7WorldCameraState Advance(Ff7WorldCameraState state, int viewMode, int frameMultiplier = 2)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegative(viewMode);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(viewMode, profiles.Count);
        if (frameMultiplier is not (1 or 2))
        {
            throw new NotSupportedException("FFVII camera smoothing requires an original supported frame multiplier.");
        }
        Ff7WorldCameraProfile profile = profiles[viewMode];
        int shift = frameMultiplier == 1 ? 3 : 2;
        int weight = (1 << shift) - 1;
        return state with { Pitch = checked(state.Pitch * weight + profile.Pitch) >> shift, Distance = checked(state.Distance * weight + profile.Distance) >> shift };
    }
}
