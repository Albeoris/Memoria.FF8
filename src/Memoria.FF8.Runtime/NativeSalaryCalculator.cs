using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSalaryTable([property: JsonRequired] string ExecutableSha256, [property: JsonRequired] ImmutableArray<ushort> Entries)
{
    public bool Equals(NativeSalaryTable? other)
    {
        return other is not null && ExecutableSha256 == other.ExecutableSha256 && Entries.AsSpan().SequenceEqual(other.Entries.AsSpan());
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(ExecutableSha256);
        foreach (ushort entry in Entries)
        {
            hash.Add(entry);
        }
        return hash.ToHashCode();
    }

    public void Validate()
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(ExecutableSha256) || Entries.IsDefault || Entries.Length != 32)
        {
            throw new InvalidDataException("Salary lookup requires the authenticated original32 unsignedWORD entries.");
        }
    }

    public int Pay(int rank)
    {
        Validate();
        if ((uint)rank >= Entries.Length)
        {
            throw new NotSupportedException("Native salary display would read outside its actual32-entry rank table.");
        }
        return 10 * Entries[rank];
    }
}

public sealed record NativeSalaryPayment(int OldRank, int NewRank, int OldPay, int NewPay);
public sealed record NativeSalaryResult(NativeSeeDState SeeD, NativeEconomy? Economy, NativeSalaryPayment? Payment = null, bool CloseDisplay = false);

/// <summary>Native529FF0/6519D0 salary sampling, including52B140/651C10 saved effects. Process presentation consumes the explicit callback effects.</summary>
public static class NativeSalaryCalculator
{
    public static NativeSalaryResult Sample(NativeSeeDState current, NativeEconomy? economy, NativeCharacterVitals? vitals, uint units, uint eventFlags104)
    {
        current.Validate();
        economy?.Validate();
        vitals?.Validate();
        if ((eventFlags104 & 8) != 0 && unchecked((short)current.SalaryDisplayCountdown184) < 0)
        {
            // Both possible dream states have no salary/countdown write in this branch.
            return new NativeSalaryResult(current, economy);
        }
        if (vitals is null)
        {
            throw new NotSupportedException("Native salary callback requires its actual saved dream flag and physical kill words.");
        }
        if (vitals.DreamEnabled)
        {
            return new NativeSalaryResult(current, economy);
        }
        NativeSeeDState next = current;
        NativeEconomy? nextEconomy = economy;
        NativeSalaryPayment? payment = null;
        if ((eventFlags104 & 8) == 0)
        {
            uint counter = unchecked(current.SalaryMovementCounter + units);
            if (counter >= 0x6000)
            {
                if (economy is null || current.SalaryTable is not NativeSalaryTable table || economy.InitOutSha256 != vitals.InitOutSha256)
                {
                    throw new NotSupportedException("Salary payout requires current live gil, its actual32-entry table and matching physical character initialization.");
                }
                ushort kills = 0;
                foreach (NativeCharacterVital row in vitals.Records)
                {
                    kills = unchecked((ushort)(kills + row.KillCount));
                }
                ushort oldPoints = current.RankPoints;
                short rawPoints = unchecked((short)(oldPoints + kills - current.KillBaseline18 - 10));
                ushort points = (ushort)Math.Clamp((int)rawPoints, 100, 3100);
                int newRank = points / 100;
                int newPay = table.Pay(newRank);
                uint gil = Math.Min(unchecked(economy.Gil + (uint)newPay), NativeEconomy.GilCeiling);
                nextEconomy = economy with { Gil = gil };
                next = current with { RankPoints = points, PreviousRank186 = oldPoints, KillBaseline18 = kills, SalaryMovementCounter = 0 };
                if ((eventFlags104 & 0x1010) == 0)
                {
                    int oldRank = unchecked((short)oldPoints) / 100;
                    payment = new NativeSalaryPayment(oldRank, newRank, table.Pay(oldRank), newPay);
                    next = next with { SalaryDisplayCountdown184 = 150 };
                }
            }
            else
            {
                next = next with { SalaryMovementCounter = counter, RankPoints = (ushort)Math.Clamp((int)unchecked((short)current.RankPoints), 100, 3100) };
            }
        }
        bool close = false;
        if (unchecked((short)next.SalaryDisplayCountdown184) >= 0)
        {
            close = next.SalaryDisplayCountdown184 == 0;
            next = next with { SalaryDisplayCountdown184 = unchecked((ushort)(next.SalaryDisplayCountdown184 - 1)) };
        }
        next.Validate();
        nextEconomy?.Validate();
        return new NativeSalaryResult(next, nextEconomy, payment, close);
    }
}
