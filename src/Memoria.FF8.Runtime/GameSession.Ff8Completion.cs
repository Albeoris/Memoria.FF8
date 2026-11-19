namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    /// <summary>Publishes a completion receipt once, while all final progression owners still belong to this session.</summary>
    public bool TryCompleteFf8Ending(string programHash, long fieldFrame, int objectId, int scriptId, int operation, bool nativeHalt)
    {
        if (State.Ff8Completion is not null || !Ff8CompletionState.IsTerminal(State, programHash, objectId, scriptId, operation, nativeHalt))
        {
            return false;
        }
        Ff8CompletionState completion = new(1, Guid.NewGuid().ToString("N"), State.FieldId, fieldFrame, programHash, State.MoviePlayback!.TerminalFrame, State.MoviePlayback.Skipped);
        completion.Validate();
        State = State with { Ff8Completion = completion };
        return true;
    }
}
