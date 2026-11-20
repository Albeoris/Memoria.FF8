using Memoria.FF7.Runtime;

internal static class CompletedNativeChoiceTests
{
    internal static void Run()
    {
        Ff7Progress before = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] };
        Ff7Progress materia = before with { Materia = [new(78, 0)] };
        Ff7Progress item = before with { Inventory = [new(12, 1)] };
        Ff7CompletedChoiceOutcome decline = Read(2, before, before);
        Ff7CompletedChoiceOutcome award = Read(3, before, materia, materiaCommands: new HashSet<int> { 78 });
        Check(Ff7AutoPlayChoiceWorkflow.SelectCompletedOutcome(2, [decline, award]) == 3, "A typed native materia award did not outrank a completed decline.");
        Check(Read(3, before, item, itemCommands: new HashSet<int> { 12 }).Reward, "A typed native item award was not recognized.");
        Check(!Read(3, before, materia).Reward, "An unowned inventory change became a reward.");
        Check(!Read(3, before, before with { Gil = 1000, FieldRandomIndex = 12 }).Reward, "Unrelated native state became a reward.");
        Check(Read(3, before, before, afterStory: 2, ownedStory: false).StoryGain == 0, "An unrelated story writer became an owned outcome.");
        Check(Read(3, before, before, afterStory: 2, ownedStory: true).StoryGain == 2, "The exact owner's story advance was lost.");
        Ff7Progress owned = before with { Inventory = [new(5, 1)] };
        Check(!Read(3, owned, materia, materiaCommands: new HashSet<int> { 78 }).Reward, "A lossy exchange was treated as a positive pickup.");
        Check(Ff7AutoPlayChoiceWorkflow.SelectCompletedOutcome(2, []) == 2, "An unknown projection did not retain the existing fallback.");
        Check(Ff7AutoPlayChoiceWorkflow.SelectCompletedOutcome(2, [award, award with { Selection = 4 }]) == 3, "Equal native outcomes were not deterministic.");
        Console.WriteLine("PASS completed current native choice typed outcomes and conservative unknown fallback");
    }

    private static Ff7CompletedChoiceOutcome Read(int selection, Ff7Progress before, Ff7Progress after, int afterStory = 0, bool ownedStory = false, IReadOnlySet<int>? materiaCommands = null, IReadOnlySet<int>? itemCommands = null)
    {
        return Ff7AutoPlayChoiceWorkflow.ReadCompletedOutcome(selection, before, after, 0, afterStory, ownedStory, materiaCommands ?? new HashSet<int>(), itemCommands ?? new HashSet<int>());
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
