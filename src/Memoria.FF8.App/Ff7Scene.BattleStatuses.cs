using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private static readonly (uint Mask, BattleHudElement.StatusBadge Badge)[] BattleStatuses =
    [
        (Ff7NativeBattleActionResolver.Death, new("KO")),
        (Ff7NativeBattleActionResolver.Sleep, new("Sleep")),
        (Ff7NativeBattleActionResolver.Poison, new("Poison")),
        (0x10, new("Sadness")),
        (0x20, new("Fury")),
        (0x40, new("Confuse")),
        (Ff7NativeBattleStatuses.Silence, new("Silence")),
        (0x100, new("Haste")),
        (Ff7NativeBattleActionResolver.Slow, new("Slow")),
        (0x400, new("Stop")),
        (0x800, new("Frog")),
        (0x1000, new("Small")),
        (0x2000, new("Slow-numb")),
        (0x4000, new("Petrify")),
        (0x8000, new("Regen")),
        (Ff7NativeBattleStatuses.Barrier, new("Barrier")),
        (Ff7NativeBattleStatuses.MagicBarrier, new("MBarrier")),
        (0x40000, new("Reflect")),
        (0x80000, new("Dual")),
        (Ff7NativeBattleStatuses.Shield, new("Shield")),
        (0x200000, new("Death Sentence")),
        (0x400000, new("Manipulate")),
        (0x800000, new("Berserk")),
        (0x1000000, new("Peerless")),
        (0x2000000, new("Paralysis")),
        (Ff7NativeBattleActionResolver.Darkness, new("Darkness")),
        (Ff7NativeBattleStatuses.DeathForce, new("Death Force")),
        (Ff7NativeBattleStatuses.Resist, new("Resist")),
        (Ff7NativeBattleStatuses.Imprisoned, new("Imprisoned"))
    ];

    internal static BattleHudElement.StatusBadge[] Ff7StatusBadges(uint statuses)
    {
        return BattleStatuses.Where(status => (statuses & status.Mask) != 0).Select(status => status.Badge).ToArray();
    }

    internal static BattleHudElement.AtbVisualStatus Ff7AtbStatus(uint statuses)
    {
        return (BattleHudElement.AtbVisualStatus)((statuses >> 8) & 7);
    }
}
