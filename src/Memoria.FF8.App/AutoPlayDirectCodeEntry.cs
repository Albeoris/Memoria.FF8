using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed class AutoPlayDirectCodeEntryState
{
    public bool TrainWrongCodeSubmitted { get; set; }
    public bool DelingWrongCodeSubmitted { get; set; }
    public int? DelingActiveAnswer { get; set; }
    public string? TrainRetryCheckpoint { get; set; }
    public AutoPlayInputSearchContext? TrainRetryContext { get; set; }
    public List<AutoPlayChoiceSelection>? TrainRetrySelections { get; set; }
    public int TrainNativeLosses { get; set; }
    public int TrainCorrectDigitsSubmitted { get; set; }
    public int TrainCodeSequencesCompleted { get; set; }
}

internal static class AutoPlayDirectCodeEntry
{
    internal static int Answer(AutoPlayDirectCodeEntryState state, AutoPlayCodeEntryRequest request)
    {
        switch (request.Puzzle)
        {
            case AutoPlayCodePuzzle.Train:
                if (request.ExpectedAnswer is < 1 or > 4)
                {
                    throw new InvalidDataException("The automatic train digit is outside its original range.");
                }
                if (!state.TrainWrongCodeSubmitted)
                {
                    state.TrainWrongCodeSubmitted = true;
                    return request.ExpectedAnswer % 4 + 1;
                }
                state.TrainCorrectDigitsSubmitted++;
                return request.ExpectedAnswer;
            case AutoPlayCodePuzzle.DelingSword:
                if (request.ExpectedAnswer is < 0 or > 199)
                {
                    throw new InvalidDataException("The automatic sword number is outside its original range.");
                }
                if (request.BeginsAttempt || state.DelingActiveAnswer is null)
                {
                    state.DelingActiveAnswer = state.DelingWrongCodeSubmitted ? request.ExpectedAnswer : (request.ExpectedAnswer + 1) % 200;
                }
                int answer = state.DelingActiveAnswer.Value;
                if (request.CompletesAttempt)
                {
                    state.DelingWrongCodeSubmitted |= answer != request.ExpectedAnswer;
                    state.DelingActiveAnswer = null;
                }
                return answer;
            default:
                throw new ArgumentOutOfRangeException(nameof(request));
        }
    }
}
