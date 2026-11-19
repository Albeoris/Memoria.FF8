namespace Memoria.FF8.Runtime;

public static class NativeBattleResultPresentation
{
    public static BattleRewardOffer[] GroupLootOffers(IEnumerable<BattleRewardOffer> offers)
    {
        List<BattleRewardOffer> rows = [];
        foreach (BattleRewardOffer offer in offers)
        {
            int index = rows.FindIndex(row => row.IsCard == offer.IsCard && row.Id == offer.Id);
            if (index < 0)
            {
                rows.Add(offer);
            }
            else
            {
                BattleRewardOffer previous = rows[index];
                rows[index] = previous with { Quantity = unchecked((byte)(previous.Quantity + offer.Quantity)), Accepted = previous.Accepted && offer.Accepted };
            }
        }
        return rows.ToArray();
    }

    public static BattleCharacterRewardPresentation[] BuildCharacters(NativeCharacterVitals before, NativeCharacterVitals after, IReadOnlyList<byte>? battleRows = null)
    {
        return (battleRows ?? before.CombatParty).Where(row => row != 255).Select(row =>
        {
            NativeCharacterVital previous = before.Records[row];
            NativeCharacterVital final = after.Records[row];
            NativeStatCurve curve = after.Definitions.Characters[final.CharacterId];
            int previousLevel = before.CharacterLevel(previous);
            int finalLevel = after.CharacterLevel(final);
            return new BattleCharacterRewardPresentation(row, previous.Experience, final.Experience, previousLevel, finalLevel, NativeStatDefinitions.ExperienceToNextLevel(previous.Experience, before.Definitions.Characters[previous.CharacterId], before.MaximumCharacterLevel), NativeStatDefinitions.ExperienceToNextLevel(final.Experience, curve, after.MaximumCharacterLevel));
        }).ToArray();
    }

    public static BattleGuardianForceRewardPresentation[] BuildGuardianForces(NativeCharacterVitals before, NativeCharacterVitals after)
    {
        List<BattleGuardianForceRewardPresentation> notices = [];
        for (int id = 0; id < after.GuardianForces.Length; id++)
        {
            NativeGuardianForceVital previous = before.GuardianForces[id];
            NativeGuardianForceVital final = after.GuardianForces[id];
            if ((final.Flags & 1) == 0)
            {
                continue;
            }
            NativeStatCurve curve = after.Definitions.GuardianForces[id];
            byte previousLevel = NativeStatDefinitions.Level(previous.Experience, curve);
            byte finalLevel = NativeStatDefinitions.Level(final.Experience, curve);
            int[] learned = Enumerable.Range(0, 128).Where(ability => final.HasLearnedAbility(ability) && !previous.HasLearnedAbility(ability)).ToArray();
            if (previousLevel != finalLevel || learned.Length > 0)
            {
                notices.Add(new BattleGuardianForceRewardPresentation(id, previousLevel, finalLevel, learned));
            }
        }
        return notices.ToArray();
    }
}
