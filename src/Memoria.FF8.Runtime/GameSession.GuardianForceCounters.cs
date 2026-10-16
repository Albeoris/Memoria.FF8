namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private void ValidateNativeGuardianForceCounterReceipt(IReadOnlyList<BattleGuardianForceCounterChange>? changes, NativeCharacterVitals projected)
    {
        IReadOnlyList<NativeGuardianForceVital>? current = State.CharacterVitals?.GuardianForces;
        if (current is null)
        {
            throw new InvalidDataException("Native GF counter publication requires its current canonical saved bank.");
        }
        if (changes is not null && (changes.Any(change => change is null) || changes.Select(change => change.Id).Distinct().Count() != changes.Count))
        {
            throw new InvalidDataException("Native GF counter publication contains duplicate receipts.");
        }
        foreach (BattleGuardianForceCounterChange change in changes ?? [])
        {
            change.Validate();
            if (current[change.Id].KillCount != change.KillsBefore || current[change.Id].DeathCount != change.DeathsBefore)
            {
                throw new InvalidDataException("Native GF counter publication has a stale saved before owner.");
            }
        }
        for (int id = 0; id < current.Count; id++)
        {
            BattleGuardianForceCounterChange? change = changes?.SingleOrDefault(change => change.Id == id);
            ushort? kills = change is null ? current[id].KillCount : change.KillsAfter;
            ushort? deaths = change is null ? current[id].DeathCount : change.DeathsAfter;
            if (projected.GuardianForces[id].KillCount != kills || projected.GuardianForces[id].DeathCount != deaths)
            {
                throw new InvalidDataException("Native GF counter publication differs from its factual after owner.");
            }
        }
    }
}
