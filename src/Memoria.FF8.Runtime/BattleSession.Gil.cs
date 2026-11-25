using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGil([property: JsonRequired] uint Gil, [property: JsonRequired] uint PastGil)
{
    internal bool IsAtMost(NativeBattleGil other)
    {
        return Gil <= other.Gil && PastGil <= other.PastGil;
    }
}

public sealed partial class BattleSession
{
    private NativeBattleGil? nativeGil;

    private uint ReadNativeMoogleGil()
    {
        NativeBattleGil gil = nativeGil ?? throw new NotSupportedException("MiniMog requires the current battle Gil wallets.");
        if ((definition.NativeSavedBattleFlagsKnownMask & 1) == 0)
        {
            throw new NotSupportedException("MiniMog requires the saved dream flag to select its Gil wallet.");
        }

        return (CurrentNativeSavedBattleFlags() & 1) != 0 ? gil.PastGil : gil.Gil;
    }

    private bool TrySpendNativeMoogleGil(uint cost)
    {
        uint balance = ReadNativeMoogleGil();
        if (balance < cost)
        {
            return false;
        }

        nativeGil = (CurrentNativeSavedBattleFlags() & 1) != 0 ? nativeGil! with { PastGil = balance - cost } : nativeGil! with { Gil = balance - cost };
        return true;
    }

    private void RestoreNativeGil(NativeBattleGil? current)
    {
        NativeBattleGil? entry = definition.NativeGil;
        if ((current is null) != (entry is null) || current is not null && !current.IsAtMost(entry!))
        {
            throw new InvalidDataException("Battle Gil snapshot contradicts its entry wallet balances.");
        }

        nativeGil = current;
    }
}
