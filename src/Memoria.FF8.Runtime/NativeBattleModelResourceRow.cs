namespace Memoria.FF8.Runtime;

public static class NativeBattleModelResourceRow
{
    public static uint Address100(NativeBattleGraphicsModel model, int resourceRowCapacity = NativeBattleModelResourceRowAddress.NativeRowCount)
    {
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            return staged;
        }
        int row = model.ActiveResourceRow100 ?? throw new NotSupportedException("Native body row access requires its actual current CPU or staged resource owner.");
        NativeBattleModelResourceRowAddress.ValidateCapacity(resourceRowCapacity);
        if ((uint)row >= resourceRowCapacity)
        {
            throw new InvalidDataException(resourceRowCapacity == NativeBattleModelResourceRowAddress.NativeRowCount ? "Native body resource row leaves its actual eleven physical slots." : "Hosted body resource row leaves its current resource alias bank.");
        }
        return NativeBattleModelResourceRowAddress.Encode(row, resourceRowCapacity);
    }
}
