namespace Memoria.FF8.Runtime;

public sealed partial record NativeSavedWorldState
{
    public NativeSavedWorldState WithSavedCarEntry(byte entityType)
    {
        Validate();
        NativeSavedWorldCarReturn receipt = CarReturn ?? throw new NotSupportedException("Saved car entry requires its native pose4 and auxiliary owners.");
        if (receipt.EntityType105 != entityType)
        {
            throw new InvalidDataException("Saved car entry changes its native transport identity.");
        }
        return this with { Slot4 = Slot4 with { ShapeId = NativeCarWorldRuntimeState.ShapeForEntity(entityType) }, CarReturn = null, UnsupportedEntry = null };
    }
}
