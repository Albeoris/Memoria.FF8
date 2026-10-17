using System.Reflection;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayDelingCodeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(199)]
    public void DirectDigitsUseTheOrdinaryDialogueCallbackAndPreserveTheNativeAnswer(int answer)
    {
        EventEngine engine = CreateEngine(answer);
        List<AutoPlayCodeEntryRequest> requests = [];
        engine.AutoPlayCodeEntry = request =>
        {
            requests.Add(request);
            return answer;
        };
        foreach ((int operation, int message, int divisor, int last) in new[] { (2015, 52, 1, 10), (2034, 51, 10, 10), (2053, 50, 100, 2) })
        {
            DialogueState dialogue = new(6, message, "digit", 10, 10, 1, last, ObjectId: 15, ScriptId: 145, Operation: operation);
            Open(engine, dialogue);

            Assert.True(Apply(engine, dialogue));

            Assert.Null(engine.CurrentDialogue);
            Assert.Equal(1 + answer / divisor % 10, engine.DialogueOutcomes.Last().Choice);
            Assert.Equal(answer, engine.EventMemory.Read(728, 1).RequireExact());
            Assert.Empty(engine.ActionLog);
        }
        Assert.Equal(new[]
        {
            new AutoPlayCodeEntryRequest(AutoPlayCodePuzzle.DelingSword, answer, true, false),
            new AutoPlayCodeEntryRequest(AutoPlayCodePuzzle.DelingSword, answer, false, false),
            new AutoPlayCodeEntryRequest(AutoPlayCodePuzzle.DelingSword, answer, false, true)
        }, requests);
    }

    [Fact]
    public void EntryMenuChoosesTheAuthoredAnswerBranchWithoutConsumingAnAttempt()
    {
        EventEngine engine = CreateEngine(50);
        engine.AutoPlayCodeEntry = _ => throw new InvalidOperationException("The menu cannot consume an answer attempt.");
        DialogueState dialogue = new(0, 38, "menu", 61, 130, 0, 4, ObjectId: 15, ScriptId: 145, Operation: 1990);
        Open(engine, dialogue);

        Assert.True(Apply(engine, dialogue));

        Assert.Null(engine.CurrentDialogue);
        Assert.Equal(0, Assert.Single(engine.DialogueOutcomes).Choice);
        Assert.Empty(engine.ActionLog);
    }

    [Theory]
    [InlineData(false, 761, 15, 145, 2015, 52)]
    [InlineData(true, 760, 15, 145, 2015, 52)]
    [InlineData(true, 761, 14, 145, 2015, 52)]
    [InlineData(true, 761, 15, 144, 2015, 52)]
    [InlineData(true, 761, 15, 145, 2014, 52)]
    [InlineData(true, 761, 15, 145, 2015, 51)]
    public void ManualAndUnrelatedDialoguesRemainOpen(bool automatic, int field, int owner, int script, int operation, int message)
    {
        EventEngine engine = CreateEngine(50, field);
        if (automatic)
        {
            engine.AutoPlayCodeEntry = _ => throw new InvalidOperationException("An unrelated prompt cannot request the answer policy.");
        }
        DialogueState dialogue = new(6, message, "digit", 10, 10, 1, 10, ObjectId: owner, ScriptId: script, Operation: operation);
        Open(engine, dialogue);

        Assert.False(Apply(engine, dialogue));

        Assert.Equal(dialogue, engine.CurrentDialogue);
        Assert.Empty(engine.DialogueOutcomes);
    }

    private static EventEngine CreateEngine(int answer, int field = 761)
    {
        EventEngine engine = new(new GameSession(new GameState { Scene = SceneKind.Field, FieldId = field }), NativeEventMemory.Empty.Write(728, 1, NativeScalarValue.Exact(answer)));
        typeof(EventEngine).GetProperty(nameof(EventEngine.FieldId))!.SetValue(engine, field);
        return engine;
    }

    private static void Open(EventEngine engine, DialogueState dialogue)
    {
        SortedDictionary<int, DialogueState> dialogues = (SortedDictionary<int, DialogueState>)typeof(EventEngine).GetField("dialogues", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        dialogues[dialogue.Channel] = dialogue;
        typeof(EventEngine).GetProperty(nameof(EventEngine.CurrentDialogue))!.SetValue(engine, dialogue);
    }

    private static bool Apply(EventEngine engine, DialogueState dialogue)
    {
        return (bool)typeof(EventEngine).GetMethod("TryApplyAutoPlayDelingCode", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, [dialogue])!;
    }
}
