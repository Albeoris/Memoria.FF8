namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void InitializeNewGameScannedModels(NativeScannedModels scanned)
    {
        scanned.Validate();
        if (scanned.Words.Any(word => word != 0) || State.ScannedModels is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Native Scan initialization requires the untouched authentic New Game clear.");
        }
        State = State with { ScannedModels = scanned };
    }

    private void ValidateNativeScanReceipt(NativeScannedModels? before, NativeScannedModels? after)
    {
        NativeScannedModels.ValidateReceipt(before, after);
        if (before is not null && (State.ScannedModels is not { } current || !current.SameValue(before)))
        {
            throw new InvalidDataException("Native Scan receipt differs from its current canonical saved history.");
        }
    }
}
