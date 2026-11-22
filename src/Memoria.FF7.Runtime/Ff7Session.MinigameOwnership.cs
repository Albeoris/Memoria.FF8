using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeMinigameReturn(long SourceGeneration, long DestinationGeneration, long Tick, FieldHostRequest Request, Ff7AutoPlayActiveEvent SourceActivation);

public sealed partial class Ff7Session
{
    public event Action<Ff7NativeMinigameReturn>? NativeMinigameReturned;

    private void CompleteOwnedMinigameFieldReturn(FieldHostRequest owner, FieldVariables variables, Ff7Progress progress)
    {
        if (owner.Command is not FieldMinigameCommand { Game: 4 } command || Host.PendingMinigame != owner)
        {
            throw new InvalidDataException("The native minigame return lost its pending field request.");
        }
        FieldVmSnapshot source = Machine.Capture();
        if ((uint)owner.Entity >= source.Entities.Length || source.Entities[owner.Entity].Priority != owner.Priority || source.Entities[owner.Entity].ProgramCounter != owner.ProgramCounter || !(source.PendingRequests ?? []).Contains(owner))
        {
            throw new InvalidDataException("The native minigame return lost its suspended script owner.");
        }
        long generation = FieldGeneration;
        Ff7AutoPlayActiveEvent? activation = AutoPlayState?.Active;
        if (activation is not null && (!Ff7AutoPlayExecutionOwners.Matches(activation, generation, source.ProgramIdentity) || !Ff7AutoPlayExecutionOwners.ReadCurrentOwners(source, activation, generation).TryGetValue(owner.Entity, out int priority) || priority != owner.Priority))
        {
            activation = null;
        }
        CompleteMinigameFieldReturn(command.Return, variables, progress);
        if (activation is not null)
        {
            NativeMinigameReturned?.Invoke(new(generation, FieldGeneration, source.Tick, owner, activation));
        }
    }
}
