using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    public bool SupportsMagicSequence(ushort ability)
    {
        return !IsDrawCast(ability) && ReadOriginAbilities().Any(choice => choice.Key == ability && choice.NativeAction is { Command: 2 });
    }

    public IReadOnlyList<int> MagicCastCounts(ushort ability)
    {
        return host.GuestMagicCastCounts(ActorSlot, ability);
    }

    public int MagicCastCost(ushort ability, int casts)
    {
        uint statuses = host.GetGuestOriginBattleStatuses(ActorSlot);
        int mode = casts == 2 && (statuses & 0x20000) == 0 && (statuses & 0x40000) != 0 ? 3 : casts;
        return Math.Min(casts, NativeBattleMagicRepetition.Cost(mode, kit.StatusPassives, encounter.Working.MagicSystem.InfiniteMagic, encounter.Working.MagicSystem.UnlimitedMagic));
    }

    public bool CanRepeatMagic(ushort ability, int ordinal)
    {
        return kit.Spells.Any(action => action.ActionId == ability) && !IsDrawCast(ability) && NativeBattleMagicRepetition.AdmitsRepeat(host.GetGuestOriginBattleStatuses(ActorSlot), ordinal);
    }
}
