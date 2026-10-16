namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void InitializeNewGameGlobalRandom(NativeGlobalRandomState random)
    {
        if (State.GlobalRandom is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame || random.State != 0)
        {
            throw new NotSupportedException("Native global random initialization requires the executed untouched New Game clear of savedDWORD1CFEFB4.");
        }
        State = State with { GlobalRandom = random };
    }

    private NativeGlobalRandomState? ProjectNativeGlobalRandom(NativeGlobalRandomChange? change)
    {
        if (change is null)
        {
            return State.GlobalRandom;
        }
        change.Validate();
        if (State.GlobalRandom != change.Before)
        {
            throw new InvalidDataException("Battle global random receipt differs from its current savedDWORD1CFEFB4 owner.");
        }
        return change.After;
    }
}
