using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    public IReadOnlyList<int> MagicCastCounts(ushort ability)
    {
        CrossoverGuestAbility? choice = ReadOriginAbilities().SingleOrDefault(choice => choice.Key == ability);
        if (choice?.NativeAction is not { Command: 2 } action || IsDrawCast(ability))
        {
            return [1];
        }
        MagicCastQuote quote = MagicCastingService.Quote(encounter.Working, kit.RecordRow, action.ActionId, MagicCastContext.Battle, characterId: kit.CharacterId);
        return NativeBattleMagicRepetition.PlayerCounts(host.GetGuestOriginBattleStatuses(ActorSlot), quote.Available, kit.StatusPassives, encounter.Working.MagicSystem.InfiniteMagic, encounter.Working.MagicSystem.UnlimitedMagic);
    }

    public int MagicCastCost(ushort ability, int casts)
    {
        return NativeBattleMagicRepetition.Cost(casts, kit.StatusPassives, encounter.Working.MagicSystem.InfiniteMagic, encounter.Working.MagicSystem.UnlimitedMagic);
    }

    public bool CanRepeatMagic(ushort ability, int ordinal)
    {
        return kit.Spells.Any(action => action.ActionId == ability) && !IsDrawCast(ability) && NativeBattleMagicRepetition.AdmitsRepeat(host.GetGuestOriginBattleStatuses(ActorSlot), ordinal);
    }
}
