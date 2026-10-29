using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>One completed native interval, retained only while the three compatible spells are presenting.</summary>
internal sealed class BattlePilotPresentationTimeline
{
    private NativeBattlePreparedPresentation? earlier;
    private NativeBattlePreparedPresentation? later;
    private object? presentationOwner;
    private float sampledAlpha;

    internal (NativeBattlePreparedPresentation Frame, NativeBattlePreparedPresentation? Next, float Alpha) Select(NativeBattlePreparedPresentation current, bool smooth, float alpha, object? owner = null)
    {
        if (!ReferenceEquals(owner, presentationOwner))
        {
            earlier = null;
            later = null;
            sampledAlpha = 0;
            presentationOwner = owner;
        }
        if (!smooth || current.PilotEffect is not (2 or 3 or 144))
        {
            earlier = null;
            later = null;
            sampledAlpha = 0;
            return (current, null, 0);
        }
        if (later is null || current.Callback != later.Callback || current.PilotEffect != later.PilotEffect || current.PilotGeneration != later.PilotGeneration)
        {
            sampledAlpha = 0;
            earlier = later is not null && current.Callback == later.Callback + 1 && current.PilotEffect == later.PilotEffect && current.PilotGeneration == later.PilotGeneration ? later : null;
        }
        later = current;
        if (earlier is null || earlier.PilotProjection is null || current.PilotProjection is null || earlier.PilotCamera is null || current.PilotCamera is null)
        {
            return (current, null, 0);
        }
        // The explicit live phase belongs only to this completed interval.
        // Keep repeated submissions monotonic; source and ownership boundaries reset it.
        sampledAlpha = Math.Max(sampledAlpha, float.IsFinite(alpha) ? Math.Clamp(alpha, 0, 1) : 0);
        return (earlier, current, sampledAlpha);
    }
}
