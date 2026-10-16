namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void InitializeNewGameLimitProgress(NativeLimitProgress progress)
    {
        if (State.LimitProgress is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Limit progression initialization requires the untouched authentic New Game template load.");
        }
        progress.Validate();
        State = State with { LimitProgress = progress };
    }
}
