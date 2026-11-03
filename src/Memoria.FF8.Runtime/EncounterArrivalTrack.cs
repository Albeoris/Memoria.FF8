namespace Memoria.FF8.Runtime;

/// <summary>Arrival sampled from accepted battle callbacks, independent of renderer refresh rate.</summary>
public sealed record EncounterArrivalTrack(string ActorId, byte Animation, byte FirstFrame, byte LastFrame, long StartedAtCallback, int CallbacksPerFrame = 1, float OpacityFrom = 0, float OpacityTo = 1)
{
    public EncounterArrivalSample Sample(long callback)
    {
        if (FirstFrame > LastFrame || LastFrame == byte.MaxValue || CallbacksPerFrame < 1 || callback < StartedAtCallback || !float.IsFinite(OpacityFrom) || !float.IsFinite(OpacityTo) || OpacityFrom < 0 || OpacityFrom > 1 || OpacityTo < 0 || OpacityTo > 1)
        {
            throw new InvalidDataException("Arrival track requires ordered zero-based source frames, a positive source clock and a callback at or after its start.");
        }
        long elapsed = callback - StartedAtCallback;
        long duration = (long)(LastFrame - FirstFrame) * CallbacksPerFrame;
        float progress = duration == 0 ? 1 : (float)Math.Min(elapsed, duration) / duration;
        byte sourceFrame = checked((byte)(FirstFrame + Math.Min(elapsed / CallbacksPerFrame, LastFrame - FirstFrame)));
        return new(Animation, sourceFrame, checked((byte)(sourceFrame + 1)), OpacityFrom + (OpacityTo - OpacityFrom) * progress, elapsed >= duration);
    }
}

public sealed record EncounterArrivalSample(byte Animation, byte SourceFrame, byte NativeFrameCounter, float Opacity, bool Complete);
