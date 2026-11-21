using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayNavigationChoiceDeferral(ushort Destination, string ProgramIdentity, int[] Options);

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    // Reviewed native vine-route choice strategy (NG-104).
    private const string VinesProgram = "0416817BBE10A56BFBD83EF25424E548080A669EAA5AFB2C4C1D94BDFE553AE0";

    internal static int? ReadVinesChoicePreference(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue)
    {
        if (state.DestinationField != 629 || source.Vm.ProgramIdentity != VinesProgram || source.World is not null || source.Battle is not null || state.Active is not { Accepted: true, Kind: Ff7AutoPlayTargetKind.LinePush, Script: 2 } active || !Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, source.Vm.ProgramIdentity) || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || dialogue is not { Window: 0, FirstChoice: 0, LastChoice: 1, Completed: false } || ReadOwnedServicePrompt(source, active, dialogue.RequestId) is not { Entity: 1, Priority: 6 } request || ReadNativeChoiceOwner(source, dialogue) is null)
        {
            return null;
        }
        var variables = new FieldVariables(source.Vm.PersistentVariables, source.Vm.MapVariables);
        Ff7Actor? player = source.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.PlayerControlled);
        if (player is not { Entity: 1 } || player.PartyCharacter != variables.ReadByte(FieldBank.Persistent1Byte, 9))
        {
            return null;
        }
        (int Entity, int Script, int Text, int Selection)? stage = request.ProgramCounter switch
        {
            0x089D => (4, 3, 25, 1),
            0x09B6 => (7, 5, 25, 0),
            0x0BC8 => (11, 8, 26, 0),
            0x0C9D when variables.ReadWord(FieldBank.MapWord, 0) == 184 => (11, 8, 25, 1),
            0x1043 => (19, 13, 26, 0),
            _ => null
        };
        return stage is { } choice && active.Entity == choice.Entity && dialogue.Text == choice.Text && active.ExecutionProvenance!.Owners.Any(owner => !owner.Queued && owner.Entity == 1 && owner.Priority == 6 && owner.Script == choice.Script) ? choice.Selection : null;
    }

    internal static bool HasCompletedVinesChoiceNavigation(Ff7SessionSave source, Ff7AutoPlayState state, Ff7AutoPlayChoiceState choice)
    {
        if (state.DestinationField != 629 || source.Vm.ProgramIdentity != VinesProgram || source.World is not null || source.Battle is not null || state.Active is not { Accepted: true, Returned: true, Kind: Ff7AutoPlayTargetKind.LinePush, Script: 2, ExecutionProvenance: { RootReturned: true, Owners.Length: 0 } } active || !MatchesChoiceSource(choice, active) || !Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, source.Vm.ProgramIdentity) || source.Process.PlayerLocked || source.Process.MenuDisabled || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || source.Process.Dialogues.Any(dialogue => !dialogue.Completed) || (source.Vm.PendingRequests ?? []).Length != 0)
        {
            return false;
        }
        int expected = active.Entity switch { 4 => 134, 7 => 82, 11 => 48, 16 => 32, 19 => 21, _ => -1 };
        return expected >= 0 && source.Process.Actors.Any(actor => actor.EntityBound && actor.PlayerControlled && actor.Entity == 1 && actor.Triangle == expected && actor.Jump is null && actor.Ladder is null);
    }

    private void ApplyVinesChoicePreference(string key, int? preferred, string program)
    {
        var coverage = Coverage(key);
        if (state.NavigationChoiceDeferrals.TryGetValue(key, out var previous) && (preferred is null || previous.Destination != state.DestinationField || previous.ProgramIdentity != program))
        {
            foreach (int option in previous.Options)
            {
                coverage.PolicyDeferredOptions.Remove(option);
            }
            if (previous.Options.Any(option => !coverage.CompletedOptions.Contains(option)))
            {
                coverage.Finished = false;
            }
            state.NavigationChoiceDeferrals.Remove(key);
        }
        if (preferred is int selection)
        {
            int[] deferred = Enumerable.Range(coverage.FirstOption, coverage.OptionCount).Where(option => option != selection && !coverage.CompletedOptions.Contains(option) && !coverage.PolicyDeferredOptions.Contains(option)).ToArray();
            foreach (int option in deferred)
            {
                coverage.PolicyDeferredOptions.Add(option);
            }
            int[] owned = state.NavigationChoiceDeferrals.TryGetValue(key, out var existing) ? existing.Options.Concat(deferred).Distinct().ToArray() : deferred;
            state.NavigationChoiceDeferrals[key] = new(629, program, owned);
        }
    }
}
