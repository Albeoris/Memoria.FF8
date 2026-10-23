namespace Memoria.FF8.Runtime;

public static class NativeBattleModelResourceRow
{
    public static uint Address100(NativeBattleGraphicsModel model)
    {
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            return staged;
        }
        int row = model.ActiveResourceRow100 ?? throw new NotSupportedException("Native body row access requires its actual current CPU or staged resource owner.");
        if ((uint)row >= 11)
        {
            throw new InvalidDataException("Native body resource row leaves its actual eleven physical slots.");
        }
        return checked(0x1D99774u + (uint)row * 0x34);
    }
}
