using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record GameplaySaveSeeD([property: JsonRequired] ushort RankPoints, [property: JsonRequired] bool? SalaryUiEnabled, [property: JsonRequired] uint SalaryMovement, [property: JsonRequired] ushort KillBaseline, [property: JsonRequired] ushort SalaryDisplayCountdown, [property: JsonRequired] ushort PreviousRank, [property: JsonRequired] byte TestLevel);

/// <summary>Accumulated time, correlated timer alternatives and random/movement history without installed salary tables.</summary>
public sealed record GameplaySaveClocks([property: JsonRequired] NativeExamTimerState? ExamTimer, [property: JsonRequired] NativePlaytimeState? Playtime, [property: JsonRequired] NativeEventRandomState? EventRandom, [property: JsonRequired] NativeCrtRandomState? CrtRandom, [property: JsonRequired] NativeMenuRandomState? MenuRandom, [property: JsonRequired] NativeGlobalRandomState? GlobalRandom, [property: JsonRequired] GameplaySaveSeeD? SeeD)
{
    internal static GameplaySaveClocks Capture(GameState state)
    {
        state.SeeD?.Validate();
        return new(state.ExamTimer, state.Playtime, state.EventRandom, state.CrtRandom, state.MenuRandom, state.GlobalRandom, state.SeeD is { } seed ? new(seed.RankPoints, seed.SalaryUiEnabled, seed.SalaryMovementCounter, seed.KillBaseline18, seed.SalaryDisplayCountdown184, seed.PreviousRank186, seed.TestLevel) : null);
    }

    public void Validate()
    {
        ExamTimer?.Validate();
        Playtime?.Validate();
        EventRandom?.Validate();
        MenuRandom?.Validate();
    }
}
