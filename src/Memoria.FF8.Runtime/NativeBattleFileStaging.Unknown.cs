namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    public NativeBattleFileStaging MarkUnknown(uint address, int length)
    {
        if (length < 0 || (ulong)address + (uint)length > (ulong)uint.MaxValue + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        RequireWritableRange(address, length);
        if (length == 0)
        {
            return this;
        }
        if (address < BaseAddress)
        {
            Rebase(address);
        }
        if (Transaction is { } transaction)
        {
            transaction.MarkUnknown(address, length);
        }
        else
        {
            RequireOwner().MarkUnknown(address, length);
        }
        return this;
    }
}
