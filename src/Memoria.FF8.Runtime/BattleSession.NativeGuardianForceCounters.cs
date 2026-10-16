namespace Memoria.FF8.Runtime;

public sealed record BattleGuardianForceCounterChange(int Id, ushort? KillsBefore, ushort? KillsAfter, ushort? DeathsBefore, ushort? DeathsAfter)
{
    public void Validate()
    {
        if (Id is < 0 or >= 16 || (KillsBefore is null) != (KillsAfter is null) || (DeathsBefore is null) != (DeathsAfter is null) || KillsBefore == KillsAfter && DeathsBefore == DeathsAfter)
        {
            throw new InvalidDataException("Native GF counter receipt lacks its exact before/after WORD owners.");
        }
    }
}

public sealed partial class BattleSession
{
    public BattleGuardianForceCounterChange[] GuardianForceCounterChanges => guardianForces.Values.OrderBy(gf => gf.Id).Select(gf =>
    {
        BattleGuardianForceVital before = definition.GuardianForces.Single(initial => initial.Id == gf.Id);
        return new BattleGuardianForceCounterChange(gf.Id, before.KillCount, gf.KillCount, before.DeathCount, gf.DeathCount);
    }).Where(change => change.KillsBefore != change.KillsAfter || change.DeathsBefore != change.DeathsAfter).ToArray();

    private void IncrementNativeGuardianForceKill(int id)
    {
        BattleGuardianForceVital gf = guardianForces.GetValueOrDefault(id) ?? throw new NotSupportedException("Native GF kill counter requires its original saved GF row.");
        ushort before = gf.KillCount ?? throw new NotSupportedException("Native GF kill requires the actual saved counter WORD at GF row+60.");
        //494AF0 increments1CFDCE4[34*(actionId-64)] without saturation.
        guardianForces[id] = gf with { KillCount = unchecked((ushort)(before + 1)) };
    }

    private static BattleGuardianForceVital NativeGuardianForceShieldResult(BattleGuardianForceVital gf, ushort hitPoints)
    {
        if (gf.HitPoints != 0 && hitPoints == 0)
        {
            ushort deaths = gf.DeathCount ?? throw new NotSupportedException("Native GF defeat requires the actual saved counter WORD at GF row+62.");
            //494410 increments1CFDCE6 only on the summoning shield HPWORD transition to0.
            return gf with { HitPoints = 0, DeathCount = unchecked((ushort)(deaths + 1)) };
        }
        return gf with { HitPoints = hitPoints };
    }
}
