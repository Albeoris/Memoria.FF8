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

    private bool TrySpendNativeMoogleGil(uint cost)
    {
        NativeBattleGil gil = nativeGil ?? throw new NotSupportedException("MiniMog requires the current battle Gil wallets.");
        if ((definition.NativeSavedBattleFlagsKnownMask & 1) == 0)
        {
            throw new NotSupportedException("MiniMog requires the saved dream flag to select its Gil wallet.");
        }

        bool dream = (CurrentNativeSavedBattleFlags() & 1) != 0;
        uint balance = dream ? gil.PastGil : gil.Gil;
        if (balance < cost)
        {
            return false;
        }

        nativeGil = dream ? gil with { PastGil = balance - cost } : gil with { Gil = balance - cost };
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
