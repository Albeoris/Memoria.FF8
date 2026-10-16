namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void InitializeNewGameKnownMagic(NativeKnownMagic known)
    {
        known.Validate();
        if (known.Words.Any(word => word != 0) || State.KnownMagic is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Native known magic initialization requires the untouched authentic New Game clear.");
        }
        State = State with { KnownMagic = known };
    }

    private void ValidateNativeKnownMagicReceipt(NativeKnownMagic? before, NativeKnownMagic? after)
    {
        NativeKnownMagic.ValidateReceipt(before, after);
        if (before is not null && (State.KnownMagic is not { } current || !current.SameValue(before)))
        {
            throw new InvalidDataException("Native known magic receipt differs from its current canonical saved history.");
        }
    }
}
