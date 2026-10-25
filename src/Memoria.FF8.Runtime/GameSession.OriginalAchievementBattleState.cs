namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private GameState StageOriginalAchievementBattleState(GameState candidate, BattleSnapshot owned)
    {
        if (candidate.PendingBattleEncounter is not { } encounter || owned.Definition.NativeEntry is null || owned.Definition.EncounterId != encounter.Formation || owned.DebugOutcome is not null)
        {
            return candidate;
        }
        NativeCharacterVitals? vitals = owned.MagicVitals ?? candidate.CharacterVitals;
        if (vitals is not null)
        {
            vitals.Validate();
            NativeGuardianForceVital[] guardianForces = vitals.GuardianForces.ToArray();
            foreach (BattleGuardianForceVital guardianForce in owned.GuardianForces)
            {
                if (guardianForce.Id is < 0 or >= 16)
                {
                    throw new InvalidDataException("Live achievement observation has an invalid native guardian force owner.");
                }
                if (guardianForce.Acquired)
                {
                    guardianForces[guardianForce.Id] = guardianForces[guardianForce.Id] with { Flags = (byte)(guardianForces[guardianForce.Id].Flags | 1) };
                }
            }
            vitals = vitals with { GuardianForces = guardianForces };
        }
        NativeEconomy? economy = candidate.Economy;
        if (economy is not null && owned.NativeItemInventory is { } items)
        {
            economy = economy with { Items = items.ToArray() };
        }
        GameState observed = candidate with { CharacterVitals = vitals, CardInventory = owned.NativeCards ?? candidate.CardInventory, Economy = economy };
        return StageOriginalAchievementStateObservation(candidate, OriginalAchievementState.Observe(observed));
    }
}
