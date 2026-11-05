using System.Text.Json;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayDirectCodeEntryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void TrainAlwaysSubmitsCorrectDigitsAcrossColdRestore(int expected)
    {
        AutoPlayRunState run = new();
        foreach (int next in new[] { expected, 4, 3, 2, 1, expected })
        {
            Assert.Equal(next, Answer(run, AutoPlayCodePuzzle.Train, next));
            run = ColdRestore(run);
        }
        Assert.Equal(6, run.DirectCodeEntry.TrainCorrectDigitsSubmitted);
        Assert.False(run.DirectCodeEntry.TrainWrongCodeSubmitted);
        Assert.False(run.DirectCodeEntry.DelingWrongCodeSubmitted);
        Assert.Null(run.DirectCodeEntry.DelingActiveAnswer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(198)]
    [InlineData(199)]
    public void SwordAlwaysSubmitsCorrectNumberAcrossMidAnswerColdRestore(int expected)
    {
        AutoPlayRunState run = new();
        Assert.Equal(expected, Answer(run, AutoPlayCodePuzzle.DelingSword, expected, begins: true, completes: false));
        Assert.Equal(expected, run.DirectCodeEntry.DelingActiveAnswer);
        run = ColdRestore(run);
        Assert.Equal(expected, Answer(run, AutoPlayCodePuzzle.DelingSword, expected, begins: false, completes: false));
        run = ColdRestore(run);
        Assert.Equal(expected, Answer(run, AutoPlayCodePuzzle.DelingSword, expected, begins: false, completes: true));
        Assert.False(run.DirectCodeEntry.DelingWrongCodeSubmitted);
        Assert.Null(run.DirectCodeEntry.DelingActiveAnswer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyWrongAttemptStateCannotCauseAnotherWrongSubmission(bool submitted)
    {
        AutoPlayRunState run = new();
        run.DirectCodeEntry.TrainWrongCodeSubmitted = submitted;
        run.DirectCodeEntry.DelingWrongCodeSubmitted = submitted;
        run.DirectCodeEntry.DelingActiveAnswer = 0;
        run = ColdRestore(run);
        Assert.Equal(1, Answer(run, AutoPlayCodePuzzle.Train, 1));
        Assert.Equal(199, Answer(run, AutoPlayCodePuzzle.DelingSword, 199, begins: false, completes: false));
        Assert.Equal(199, run.DirectCodeEntry.DelingActiveAnswer);
        run = ColdRestore(run);
        Assert.Equal(199, Answer(run, AutoPlayCodePuzzle.DelingSword, 199, begins: false, completes: true));
        Assert.Null(run.DirectCodeEntry.DelingActiveAnswer);
        Assert.Equal(submitted, run.DirectCodeEntry.TrainWrongCodeSubmitted);
        Assert.Equal(submitted, run.DirectCodeEntry.DelingWrongCodeSubmitted);
    }

    private static int Answer(AutoPlayRunState run, AutoPlayCodePuzzle puzzle, int expected, bool begins = true, bool completes = true)
    {
        return AutoPlayDirectCodeEntry.Answer(run.DirectCodeEntry, new(puzzle, expected, begins, completes));
    }

    private static AutoPlayRunState ColdRestore(AutoPlayRunState run)
    {
        return JsonSerializer.Deserialize<AutoPlayRunState>(JsonSerializer.Serialize(run, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;
    }
}
