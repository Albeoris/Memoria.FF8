namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeKnownMagic? nativeKnownMagic;
    public NativeKnownMagic? KnownMagic => nativeKnownMagic;

    private void DiscoverNativeMagic(int spell)
    {
        nativeKnownMagic = (nativeKnownMagic ?? throw new NotSupportedException("Native Draw discovery requires the saved64-bit known magic bank; explicitly migrate an old unknown owner.")).Add(spell);
    }

    private void RefreshNativeKnownMagicStocks()
    {
        if (nativeKnownMagic is null)
        {
            return;
        }
        //48B8B0 scans all32 slots of the three physical party records after pending counters.
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsPlayer && actor.NativeIndex is >= 0 and < 3))
        {
            foreach (NativeMagicStock stock in actor.NativeMagicInventory.Where(stock => stock.SpellId != 0))
            {
                DiscoverNativeMagic(stock.SpellId);
            }
        }
    }

    private void RestoreNativeKnownMagic(NativeKnownMagic? saved)
    {
        NativeKnownMagic.ValidateReceipt(definition.NativeKnownMagic, saved);
        nativeKnownMagic = saved;
    }
}
