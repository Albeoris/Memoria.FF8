using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Actual field callbacks since allocation, classified by the movie flag sampled before VM dispatch.</summary>
public sealed record NativeFieldCallbackClock(int Version, long OrdinaryCallbacks, long MovieCallbacks, [property: System.Text.Json.Serialization.JsonRequired] long MenuCallbacks, [property: System.Text.Json.Serialization.JsonRequired] long FieldLoaderCallbacks, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] long? BattleCallbacks = null, [property: System.Text.Json.Serialization.JsonRequired] long? InitializationTransactions = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] long RandomBattleCallbacks = 0, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] long MenuBattleCallbacks = 0)
{
    public void Validate(long frame)
    {
        if (Version != 1 || InitializationTransactions is null or < 0 or > 1 || OrdinaryCallbacks < 0 || MovieCallbacks < 0 || MenuCallbacks < 0 || FieldLoaderCallbacks < 0 || BattleCallbacks < 0 || RandomBattleCallbacks < 0 || MenuBattleCallbacks < 0 || OrdinaryCallbacks > frame - InitializationTransactions.Value || MovieCallbacks != frame - InitializationTransactions.Value - OrdinaryCallbacks)
        {
            throw new InvalidDataException("Native field callback clock does not match its executed frame history.");
        }
    }

    public long TimerCalls => checked(2 * OrdinaryCallbacks + 4 * MovieCallbacks + MenuCallbacks + 2 * FieldLoaderCallbacks + 4 * BattleCallbacks.GetValueOrDefault() + 4 * RandomBattleCallbacks + 4 * MenuBattleCallbacks);
}

public sealed partial class EventEngine
{
    private void AdvanceNativeFieldCallbackClock()
    {
        NativeFieldCallbackClock clock = Scene.FieldCallbackClock ?? throw new NotSupportedException("Native field callbacks have no executed allocation clock history; load a snapshot containing its native allocation state.");
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
