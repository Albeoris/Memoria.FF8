namespace Memoria.FF7.Runtime;

internal static class Ff7NativeEnemyCommandBanks
{
    public static (Ff7BattleCommand Command, ushort Argument) Resolve(ushort attack)
    {
        return attack switch
        {
            < 0x38 => (Ff7BattleCommand.Magic, attack),
            < 0x48 => (Ff7BattleCommand.Summon, checked((ushort)(attack - 0x38))),
            < 0x60 => (Ff7BattleCommand.EnemySkill, checked((ushort)(attack - 0x48))),
            < 0x100 => (Ff7BattleCommand.Limit, checked((ushort)(attack - 0x60))),
            _ => throw new ArgumentOutOfRangeException(nameof(attack), "Scene attacks do not use a global command bank.")
        };
    }
}
