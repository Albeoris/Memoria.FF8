namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void InitializeNewGamePocketStation(NativePocketStationState pocketStation)
    {
        if (State.PocketStation is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("PocketStation initialization requires the untouched authentic New Game template load.");
        }
        pocketStation.Validate();
        State = State with { PocketStation = pocketStation };
    }
}
