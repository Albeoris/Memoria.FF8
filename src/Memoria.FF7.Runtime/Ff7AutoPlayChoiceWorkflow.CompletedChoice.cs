using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7CompletedChoiceOutcome(int Selection, int StoryGain, bool Reward);

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    private int SelectCompletedNativeChoice(Ff7SessionSave source, Ff7Dialogue prompt, AutoPlayChoiceCoverage coverage, int first)
    {
        if (nativeSession is null || nativeSession.World is not null || nativeSession.Battle is not null || prompt.FirstChoice is not int firstChoice || prompt.LastChoice is not int lastChoice || !coverage.Finished || state.Active is not { Accepted: true } active)
        {
            return first;
        }
        FieldHostRequest? owner = (source.Vm.PendingRequests ?? []).SingleOrDefault(request => request.Id == prompt.RequestId && request.Command is FieldDialogueCommand);
        owner ??= source.Process.Receipts.Select(receipt => receipt.Request).SingleOrDefault(request => request.Id == prompt.RequestId && request.Command is FieldDialogueCommand);
        if (owner is null || !Ff7AutoPlayScriptedInput.ReadInteractionOwners(source.Vm, active, source.FieldGeneration).TryGetValue(owner.Entity, out int priority) || priority != owner.Priority)
        {
            return first;
        }
        List<Ff7CompletedChoiceOutcome> outcomes = [];
        Ff7NativePlanningQuantum allowance = new(int.MaxValue, TimeSpan.FromSeconds(2));
        foreach (int selection in coverage.CompletedOptions.Where(selection => selection >= firstChoice && selection <= lastChoice).Distinct().Order())
        {
            if (!allowance.TryBeginOperation())
            {
                break;
            }
            Ff7CompletedChoiceOutcome? outcome;
            try
            {
                outcome = ProjectCompletedChoice(source, prompt, owner, selection, allowance);
            }
            catch (NotSupportedException)
            {
                continue;
            }
            if (outcome is not null)
            {
                outcomes.Add(outcome);
            }
        }
        int? positive = ReadPositiveCompletedOutcome(outcomes);
        return positive ?? ReadCompletedServicePreference(source, prompt, coverage) ?? first;
    }

    private Ff7CompletedChoiceOutcome? ProjectCompletedChoice(Ff7SessionSave source, Ff7Dialogue prompt, FieldHostRequest owner, int selection, Ff7NativePlanningQuantum allowance)
    {
        using Ff7Session branch = nativeSession!.ForkAutoPlayNavigation();
        bool returned = false;
        bool selected = false;
        bool storyWrite = false;
        bool ownedStoryWrite = false;
        HashSet<int> materia = [];
        HashSet<int> items = [];
        branch.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (!reply.Completed || request.Entity != owner.Entity || request.Priority != owner.Priority)
            {
                return;
            }
            if (request.Command is FieldMateriaCommand reward)
            {
                materia.Add((int)(reward.Encoded & 255));
            }
            if (request.Command is FieldItemCommand { Remove: false } item)
            {
                items.Add(item.Encoded & 511);
            }
        };
        branch.Machine.InstructionVariableAccessed += (variable, write) =>
        {
            storyWrite |= write && (variable.Bank is FieldBank.Persistent0Byte or FieldBank.Persistent0Word) && (byte)variable.Operand <= 1;
        };
        branch.Machine.InstructionExecuted += (entity, priority, _, _) =>
        {
            ownedStoryWrite |= storyWrite && entity == owner.Entity && priority == owner.Priority;
            storyWrite = false;
        };
        branch.NativeTickCompleted += observation => returned |= observation.ScriptReturns.Any(completed => completed.Entity == owner.Entity && completed.Priority == owner.Priority);
        int? Choose(Ff7Dialogue dialogue)
        {
            if (dialogue.RequestId != prompt.RequestId || dialogue.FirstChoice != prompt.FirstChoice || dialogue.LastChoice != prompt.LastChoice || selected)
            {
                throw new NotSupportedException("Completed-choice projection reached another native choice.");
            }
            selected = true;
            return selection;
        }
        while (!returned)
        {
            if (!allowance.TryBeginOperation())
            {
                // Optional projection is unknown when its work allowance expires.
                return null;
            }
            if (!branch.HasPendingTick)
            {
                foreach (Ff7Dialogue dialogue in branch.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && branch.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
                {
                    Ff7Dialogue acknowledged = dialogue;
                    if (dialogue.FirstChoice is not null)
                    {
                        acknowledged = Ff7DialogueCursor.Select(dialogue, Choose(dialogue)!.Value);
                    }
                    branch.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(acknowledged, FieldText.Read(branch.Field.Scripts.Texts[dialogue.Text].Span)));
                }
                branch.SetInput(0, 0);
            }
            branch.AdvanceTick();
            if (branch.World is not null || branch.Battle is not null || branch.MapId != nativeSession!.MapId || branch.FieldGeneration != source.FieldGeneration || branch.Host.Process.Transition is not null || branch.Host.Process.GlobalCommand == Ff7GlobalCommand.Menu || branch.Shop is not null || branch.Host.PendingMinigame is not null || branch.Machine.ReadCurrentInputDemands().Count != 0)
            {
                return null;
            }
        }
        if (!selected || !returned)
        {
            return null;
        }
        return ReadCompletedOutcome(selection, source.Progress, branch.Progress, ReadStory(source), branch.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0), ownedStoryWrite, materia, items);
    }

    internal static Ff7CompletedChoiceOutcome ReadCompletedOutcome(int selection, Ff7Progress before, Ff7Progress after, int beforeStory, int afterStory, bool ownedStoryWrite, IReadOnlySet<int> materiaCommands, IReadOnlySet<int> itemCommands)
    {
        Dictionary<int, int> beforeMateria = before.Materia.GroupBy(materia => materia.MateriaId).ToDictionary(group => group.Key, group => group.Count());
        Dictionary<int, int> afterMateria = after.Materia.GroupBy(materia => materia.MateriaId).ToDictionary(group => group.Key, group => group.Count());
        Dictionary<int, int> beforeItems = before.Inventory.ToDictionary(item => item.ItemId, item => item.Quantity);
        Dictionary<int, int> afterItems = after.Inventory.ToDictionary(item => item.ItemId, item => item.Quantity);
        bool lossless = beforeMateria.All(item => afterMateria.GetValueOrDefault(item.Key) >= item.Value) && beforeItems.All(item => afterItems.GetValueOrDefault(item.Key) >= item.Value);
        bool reward = lossless && (materiaCommands.Any(id => afterMateria.GetValueOrDefault(id) > beforeMateria.GetValueOrDefault(id)) || itemCommands.Any(id => afterItems.GetValueOrDefault(id) > beforeItems.GetValueOrDefault(id)));
        return new(selection, ownedStoryWrite ? Math.Max(0, afterStory - beforeStory) : 0, reward);
    }

    internal static int SelectCompletedOutcome(int fallback, IReadOnlyList<Ff7CompletedChoiceOutcome> outcomes)
    {
        return ReadPositiveCompletedOutcome(outcomes) ?? fallback;
    }

    private static int? ReadPositiveCompletedOutcome(IReadOnlyList<Ff7CompletedChoiceOutcome> outcomes) => outcomes.Where(outcome => outcome.StoryGain > 0 || outcome.Reward).OrderByDescending(outcome => outcome.StoryGain).ThenByDescending(outcome => outcome.Reward).FirstOrDefault()?.Selection;
}
