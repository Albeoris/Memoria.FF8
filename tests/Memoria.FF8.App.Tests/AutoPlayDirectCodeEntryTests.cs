using System.Text.Json;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayDirectCodeEntryTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 1)]
    public void TrainSubmitsOneWrongDigitThenAlwaysCorrectAcrossColdRestore(int expected, int wrong)
    {
        AutoPlayRunState run = new();

        Assert.Equal(wrong, Answer(run, AutoPlayCodePuzzle.Train, expected));
        Assert.True(run.DirectCodeEntry.TrainWrongCodeSubmitted);
        Assert.False(run.DirectCodeEntry.DelingWrongCodeSubmitted);

        run = ColdRestore(run);
        foreach (int next in new[] { expected, 4, 3, 2, 1, expected })
        {
            Assert.Equal(next, Answer(run, AutoPlayCodePuzzle.Train, next));
            run = ColdRestore(run);
        }
        Assert.True(run.DirectCodeEntry.TrainWrongCodeSubmitted);
        Assert.False(run.DirectCodeEntry.DelingWrongCodeSubmitted);
        Assert.Null(run.DirectCodeEntry.DelingActiveAnswer);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 51)]
    [InlineData(198, 199)]
    [InlineData(199, 0)]
    public void SwordLatchesOneWholeWrongNumberAcrossMidAnswerColdRestore(int expected, int wrong)
    {
        AutoPlayRunState run = new();

        Assert.Equal(wrong, Answer(run, AutoPlayCodePuzzle.DelingSword, expected, begins: true, completes: false));
        Assert.Equal(wrong, run.DirectCodeEntry.DelingActiveAnswer);
        Assert.False(run.DirectCodeEntry.DelingWrongCodeSubmitted);

        run = ColdRestore(run);
        Assert.Equal(wrong, Answer(run, AutoPlayCodePuzzle.DelingSword, expected, begins: false, completes: false));
        Assert.False(run.DirectCodeEntry.DelingWrongCodeSubmitted);

        run = ColdRestore(run);
        Assert.Equal(wrong, Answer(run, AutoPlayCodePuzzle.DelingSword, expected, begins: false, completes: true));
        Assert.True(run.DirectCodeEntry.DelingWrongCodeSubmitted);
        Assert.Null(run.DirectCodeEntry.DelingActiveAnswer);

        run = ColdRestore(run);
        foreach (int next in new[] { expected, 199, 0, 100, 50 })
        {
            Assert.Equal(next, Answer(run, AutoPlayCodePuzzle.DelingSword, next, begins: true, completes: false));
            run = ColdRestore(run);
            Assert.Equal(next, Answer(run, AutoPlayCodePuzzle.DelingSword, next, begins: false, completes: false));
            Assert.Equal(next, Answer(run, AutoPlayCodePuzzle.DelingSword, next, begins: false, completes: true));
            Assert.True(run.DirectCodeEntry.DelingWrongCodeSubmitted);
            Assert.Null(run.DirectCodeEntry.DelingActiveAnswer);
        }
        Assert.False(run.DirectCodeEntry.TrainWrongCodeSubmitted);
    }

    [Fact]
    public void TrainAndSwordWrongAttemptsRemainIndependentWhenInterleavedAndRestored()
    {
        AutoPlayRunState run = new();
        Assert.Equal(0, Answer(run, AutoPlayCodePuzzle.DelingSword, 199, begins: true, completes: false));
        Assert.Equal(2, Answer(run, AutoPlayCodePuzzle.Train, 1));

        run = ColdRestore(run);
        Assert.True(run.DirectCodeEntry.TrainWrongCodeSubmitted);
        Assert.False(run.DirectCodeEntry.DelingWrongCodeSubmitted);
        Assert.Equal(0, run.DirectCodeEntry.DelingActiveAnswer);
        Assert.Equal(0, Answer(run, AutoPlayCodePuzzle.DelingSword, 199, begins: false, completes: true));

        run = ColdRestore(run);
        Assert.Equal(4, Answer(run, AutoPlayCodePuzzle.Train, 4));
        Assert.Equal(50, Answer(run, AutoPlayCodePuzzle.DelingSword, 50));
        Assert.True(run.DirectCodeEntry.TrainWrongCodeSubmitted);
        Assert.True(run.DirectCodeEntry.DelingWrongCodeSubmitted);
        Assert.Null(run.DirectCodeEntry.DelingActiveAnswer);
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
