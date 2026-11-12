using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class GuestFractionRecoveryTests
{
    public static void Run()
    {
        Ff7BattleActionVitals source = new(100, 100, 0, 0, 0);
        Ff7BattleActionVitals high = new(100, 100000, 0, 0, 0);
        Ff7BattleGuestItemEffect fraction = new(0, MaximumHitPointFraction: 16);
        Ff7BattleActionReceipt ordinary = Ff7NativeBattleActionResolver.ApplyGuestItem(source, high, fraction);
        Check(ordinary.Damage.Damage == -9999 && ordinary.Target.Hp == 10099, "Source fractional healing must retain the ordinary9999 limit against high-HP host actors.");
        Ff7BattleActionReceipt extended = Ff7NativeBattleActionResolver.ApplyGuestItem(source, high, fraction with { FractionRecoveryLimit = 60000 });
        Check(extended.Damage.Damage == -60000 && extended.Target.Hp == 60100, "The source recovery limit flag must raise fractional recovery to60000.");
        Ff7BattleActionVitals rounding = new(1, 105, 0, 0, 0);
        Ff7BattleActionReceipt doubled = Ff7NativeBattleActionResolver.ApplyGuestItem(source, rounding, new(0, MaximumHitPointFraction: 1, RecoveryMultiplier: 2));
        Check(doubled.Damage.Damage == -12 && doubled.Target.Hp == 13, "MedData must multiply after the source maxHP fraction has been truncated.");
        Ff7BattleGuestItemEffect older = JsonSerializer.Deserialize<Ff7BattleGuestItemEffect>("{\"HitPoints\":0,\"MaximumHitPointFraction\":16}")!;
        Check(older.FractionRecoveryLimit == 9999 && older.RecoveryMultiplier == 1, "Older recovery receipts must restore the ordinary source cap and multiplier defaults.");
        Ff7BattleActionReceipt fixedHp = Ff7NativeBattleActionResolver.ApplyGuestItem(source, high, new(12000));
        Check(fixedHp.Damage.Damage == -12000, "Fraction limits must not be applied again to independently prepared fixed-HP recovery.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
