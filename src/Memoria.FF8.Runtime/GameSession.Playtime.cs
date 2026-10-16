namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static GameState AdvanceNativePlaytime(GameState candidate)
    {
        if (candidate.Playtime is not { } playtime)
        {
            return candidate;
        }
        NativeExamTimerState timer = candidate.ExamTimer ?? throw new InvalidDataException("Native playtime requires its exact4701B0 admitted call count.");
        return candidate with { Playtime = playtime.AdvanceTo(timer.ClockCalls) };
    }

    private static void ValidateNativePlaytime(GameState state)
    {
        state.Playtime?.Validate();
        if (state.Playtime is { } playtime && (state.ExamTimer is null || playtime.ClockCalls != state.ExamTimer.ClockCalls))
        {
            throw new InvalidDataException("Native playtime and exam timer disagree about admitted4701B0 calls.");
        }
    }
}
