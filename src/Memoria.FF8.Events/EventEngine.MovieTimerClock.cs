using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Actual field callbacks since allocation, classified by the movie flag sampled before VM dispatch.</summary>
public sealed record NativeFieldCallbackClock(int Version, long OrdinaryCallbacks, long MovieCallbacks, [property: System.Text.Json.Serialization.JsonRequired] long MenuCallbacks)
{
    public void Validate(long frame)
    {
        if (Version != 1 || OrdinaryCallbacks < 0 || MovieCallbacks < 0 || MenuCallbacks < 0 || OrdinaryCallbacks > frame || MovieCallbacks != frame - OrdinaryCallbacks)
        {
            throw new InvalidDataException("Native field callback clock does not match its executed frame history.");
        }
    }

    public long TimerCalls => checked(2 * OrdinaryCallbacks + 4 * MovieCallbacks + MenuCallbacks);
}

public sealed partial class EventEngine
{
    private void AdvanceNativeFieldCallbackClock()
    {
        NativeFieldCallbackClock clock = Scene.FieldCallbackClock ?? throw new NotSupportedException("Native field callbacks have no executed allocation clock history; recreate the causal journal.");
        clock.Validate(Frame);
        bool movie = services.NativeMovieClockFlag ?? (services.IsMoviePlaying ? throw new NotSupportedException("Native movie callback requires the accepted start/close lifecycle flag.") : false);
        if (movie != services.IsMoviePlaying)
        {
            throw new NotSupportedException("Native movie flag disagrees with its active service lifecycle.");
        }
        NativeWorldProcess? process = services.CanStoreWorldProcess ? services.WorldProcess?.AdvanceMainLoopCallbacks(1) : null;
        NativeExamTimerState? timer = services.ExamTimer?.AdvanceCallbacks(movie ? NativeTimerCallbackKind.MovieField : NativeTimerCallbackKind.NormalField, 1);
        if (process is not null)
        {
            // Native52DA20/52DCF0 execute4022B0 once before dispatching the field callback.
            services.SetWorldProcess(process);
        }
        if (timer is not null)
        {
            // 46FEE0 samples209A798 before4767B0: normal2calls, active movie4calls.
            services.SetExamTimer(timer);
        }
        Scene.FieldCallbackClock = movie ? clock with { MovieCallbacks = checked(clock.MovieCallbacks + 1) } : clock with { OrdinaryCallbacks = checked(clock.OrdinaryCallbacks + 1) };
    }
}
