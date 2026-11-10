using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeEnemyState(int ActorSlot, short EnemyId, uint HitPoints, uint MaxHitPoints, ushort MagicPoints, ushort MaxMagicPoints, ushort Defense, ushort MagicDefense, byte Conditions, uint Statuses, uint StatusImmunities, byte IdleAction, byte HurtAction);

/// <summary>Builds the PC enemy actor slots before the native initialization scripts run.</summary>
public static class Ff7NativeEnemyInitialization
{
    public static IReadOnlyList<Ff7NativeEnemyState> Create(SceneBattleData formation)
    {
        ArgumentNullException.ThrowIfNull(formation);
        Ff7NativeEnemyState[] actors = new Ff7NativeEnemyState[formation.Enemies.Count];
        for (int index = 0; index < actors.Length; index++)
        {
            SceneBattleEnemy enemy = formation.Enemies[index];
            actors[index] = new(enemy.FormationSlot + 4, enemy.Placement.EnemyId, enemy.MaxHitPoints, enemy.MaxHitPoints, enemy.MaxMagicPoints, enemy.MaxMagicPoints, (ushort)(enemy.Defense * 2), (ushort)(enemy.MagicDefense * 2), (byte)(enemy.InitialConditionFlags & 0x1F), 0, ~enemy.NativeStatusMask, 0, 1);
        }
        return Array.AsReadOnly(actors);
    }
}
