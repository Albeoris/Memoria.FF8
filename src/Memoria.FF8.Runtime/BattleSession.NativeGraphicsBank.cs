namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public bool NativeEntryReady => Presentation?.EntryReady ?? true;

    private void RequireNativeEntryReady()
    {
        if (HasPendingExpandedTerminal)
        {
            throw new InvalidOperationException("The admitted battle result owns the remaining presentation callbacks.");
        }
        if (!NativeEntryReady)
        {
            throw new InvalidOperationException("Battle commands wait for the original entry112 completion callback.");
        }
    }
}
