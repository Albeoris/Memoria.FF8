using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeSpecialStatusTests
{
    public static void Run()
    {
        Ff7BattleAttack Capture(byte type) => new(255, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 63, byte.MaxValue, 0, 0x80000000U | type, 0, ushort.MaxValue);
        foreach (byte type in new byte[] { 0, 1, 3 })
        {
            Ff7BattleAttack record = Capture(type);
            Check(Ff7NativeBattleStatuses.ReadRecordMask(record) == 0x80000000 && Ff7NativeBattleStatuses.ReadRecordMode(record) == 0 && Ff7NativeBattleStatuses.ReadImprisonedType(record) == type, "Capture low bits must select a battle mode rather than inflict Death or Near Death.");
            Check(Ff7NativeBattleStatuses.ReadRecordMode(record with { StatusChange = 0xBF }) == 0, "The native negative status-mask encoding always selects add mode.");
            Check(Ff7NativeBattleStatuses.IsBattleParticipant(0x80000000, type) == (type == 1), "Only the draining capture mode remains alive in the native battle outcome mask.");
            uint clock = Ff7NativeBattleStatuses.ProjectClockStatuses(0x80000000, type);
            Check((clock & (type == 1 ? 0x02000000U : 0x400U)) != 0 && Ff7NativeAtb.Increment(0, 30, 0, clock, 80, 128) == 0, "Capture must stop the native action gauge without mutating the recorded status bank.");
            Ff7BattleActionVitals vitals = new(320, 320, 20, 20, 0);
            Ff7BattleDamageActor stats = new(20, 30, 30, 30, 0, 0, 0, 20, 20);
            Ff7BattleRng random = Random();
            Ff7BattleActionReceipt capture = Ff7NativeBattleActionResolver.Resolve(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(record), record, random);
            Check(capture.Target.Statuses == 0x80000000 && capture.Target.Hp == 320 && capture.AddedStatuses == 0x80000000 && random.Capture().Indices.All(index => index == 0), "Capture production must retain HP, add only Imprisoned and skip a status-chance random roll.");
            Ff7BattleActionReceipt guest = Ff7NativeBattleActionResolver.ResolveGuestContact(stats, vitals, stats, vitals, Ff7BattleDamageAction.FromAttack(record), record, Random());
            Check(guest.Target.Statuses == 0x80000000 && guest.Target.Hp == 320, "Native capture must remain a host battle status when its recipient is a guest.");
        }
        Check(!Ff7NativeBattleStatuses.IsBattleParticipant(1, 1) && Ff7NativeBattleStatuses.IsBattleParticipant(0, 0), "A capture release must restore participation while real KO remains excluded.");

        const uint wards = Ff7NativeBattleStatuses.Barrier | Ff7NativeBattleStatuses.MagicBarrier | Ff7NativeBattleStatuses.Shield;
        Ff7BattleStatusTimers old = JsonSerializer.Deserialize<Ff7BattleStatusTimers>("{\"Remainder\":0,\"Sleep\":0,\"Poison\":0}");
        Ff7BattleStatusTick restored = Ff7NativeBattleStatusClock.Advance(old, wards, 128);
        Check(restored.Timers.Barrier == 127 && restored.Timers.MagicBarrier == 127 && restored.Timers.Shield == 64, "Absent nonprogress ward timers must restore documented durations instead of rejecting an old snapshot.");
        Ff7BattleStatusTimers timers = Ff7NativeBattleStatusClock.Apply(default, 0, wards);
        uint statuses = wards;
        int crossed = 0;
        while ((statuses & wards) != 0 && crossed < 20000)
        {
            Ff7BattleStatusTick tick = Ff7NativeBattleStatusClock.Advance(timers, statuses, 128);
            timers = tick.Timers;
            statuses = tick.Statuses;
            crossed++;
            if (timers.Shield == 0)
            {
                Check((statuses & Ff7NativeBattleStatuses.Shield) == 0, "Shield expiry must remove its live status.");
            }
        }
        Check((statuses & wards) == 0 && crossed < 20000, "Barrier, MBarrier and Shield must expire on their native variable clock.");
        Ff7BattleStatusTick frozen = Ff7NativeBattleStatusClock.Advance(Ff7NativeBattleStatusClock.Apply(default, 0, wards), wards | 0x80000000, 128, 0, 320);
        Check(frozen.Timers.Barrier == 127 && frozen.Timers.Remainder == 0 && frozen.HitPointDelta == 0, "Stopped capture must freeze variable timers and HP drain.");
        Ff7BattleStatusTick drain = Ff7NativeBattleStatusClock.Advance(default, 0x80000000, 128, 1, 320);
        int rate = Ff7NativeAtb.BaseRate(128) * 2;
        Check(drain.HitPointDelta == (-10 * rate >> 15) && drain.Timers.HitPointRemainder == ((-10 * rate) & 32767), "Draining capture must use maxHP/32 with the native signed fixed-point remainder.");
        Ff7BattleStatusTick release = Ff7NativeBattleStatusClock.Advance(drain.Timers, 0, 128, 1, 320);
        Check(release.HitPointDelta == 0, "Capture release must stop HP drain without changing HP by replay.");

        Ff7BattleDamageActor source = new(30, 60, 60, 30, 0, 0, 0, 20, 20);
        Ff7BattleAttack physicalRecord = Capture(0) with { DamageCalculation = 0x11, Power = 16, StatusChange = byte.MaxValue, SpecialFlags = ushort.MaxValue };
        Ff7BattleDamageAction physical = Ff7BattleDamageAction.FromAttack(physicalRecord) with { AlwaysHits = true };
        Check(physical.NativePhysicalCategory == true && Ff7NativeBattleDamage.Resolve(source, source with { NativeConditions = 0x100 }, physical, Random()).Damage == 0, "The native formula modifier must force physical immunity classification even when the original property word has bit4 set.");
        Check(Ff7NativeBattleDamage.Resolve(source, source with { NativeConditions = 0x200 }, physical, Random()).Damage > 0, "A forced physical formula must not use the magical immunity gate.");
        Ff7BattleAttack death = Capture(0) with { StatusMask = 1 };
        Ff7BattleActionVitals living = new(100, 100, 10, 10, 0);
        Ff7BattleActionReceipt protectedTarget = Ff7NativeBattleActionResolver.Resolve(source, living, source with { NativeConditions = 0x1000 }, living, Ff7BattleDamageAction.FromAttack(death), death, Random());
        Check((protectedTarget.Target.Statuses & 1) == 0, "The native death-protection condition must protect a target independently of its source record status mask.");
        Ff7BattleAttack bypass = death with { SpecialFlags = (ushort)(death.SpecialFlags & ~0x80) };
        Check((Ff7NativeBattleActionResolver.Resolve(source, living, source with { NativeConditions = 0x1000 }, living, Ff7BattleDamageAction.FromAttack(bypass), bypass, Random()).Target.Statuses & 1) != 0, "The native ignore-protection property must still override condition-based protection.");
        Ff7BattleDamageActor target = source with { Statuses = Ff7NativeBattleStatuses.Shield };
        Ff7BattleDamageAction elemental = new(0x22, 20, 255) { NativeElements = 1, AlwaysHits = true };
        Ff7BattleDamageResult absorbed = Ff7NativeBattleDamage.Resolve(source, target, elemental, Random());
        Check(absorbed.Damage < 0 && !absorbed.Missed, "Shield must absorb ordinary elemental damage.");
        Check(Ff7NativeBattleDamage.Resolve(source, target, elemental with { NativeElements = 0x800 }, Random()).Damage == 0, "Shield must nullify the native physical-type element bank.");
        Check(Ff7NativeBattleDamage.Resolve(source, target, elemental with { NativeElements = 0 }, Random()).Damage > 0, "Truly non-elemental damage must bypass Shield.");
        Check(Ff7NativeBattleDamage.Resolve(source, target, elemental with { IsItem = true }, Random()).Damage > 0, "Native items must bypass Shield's extra affinity masks.");
        Check(Ff7NativeBattleDamage.Resolve(source, target, elemental with { IgnoreNativeAffinity = true }, Random()).Damage > 0, "Attacks whose native property disables affinities must bypass Shield.");
        Ff7BattleAttack silence = Capture(0) with { StatusMask = Ff7NativeBattleStatuses.Silence, CastingCost = 1 };
        Check(!Ff7NativeBattleActionResolver.TrySpendCastingCost(new(100, 100, 10, 10, Ff7NativeBattleStatuses.Silence), silence, out _), "Produced Silence must prevent MP-cost casting.");
    }

    private static Ff7BattleRng Random() => new(new byte[3988], new(new byte[8]));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
