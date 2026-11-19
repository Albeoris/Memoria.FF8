namespace Memoria.FF8.Runtime;

/// <summary>The terminal transition after the original ending movie, including its final credits.</summary>
public sealed record Ff8CompletionState(int Version, string Identity, int FieldId, long FieldFrame, string ProgramHash, int CreditsTerminalFrame, bool CreditsSkipped)
{
    public const string OriginalEndingProgramHash = "F9BEC02EF2D789925B3E662297A0E082A3FE077FB885DFC5E41B16FAE01154A3";

    public void Validate()
    {
        if (Version != 1 || !Guid.TryParseExact(Identity, "N", out _) || FieldId != 76 || FieldFrame < 0 || ProgramHash != OriginalEndingProgramHash || CreditsTerminalFrame <= 0)
        {
            throw new InvalidDataException("Unsupported or invalid FFVIII completion receipt.");
        }
    }

    public static bool IsTerminal(GameState state, string programHash, int objectId, int scriptId, int operation, bool nativeHalt)
    {
        return state.Scene == SceneKind.Field && state.FieldId == 76 && programHash == OriginalEndingProgramHash && objectId == 0 && scriptId == 1 && operation == 42 && nativeHalt && state.MovieSource is { DiscIndex: 3, MovieIndex: 5 } && state.MoviePlayback is { Completed: true, IsPlaying: false, TerminalFrame: > 0 };
    }
}
