using System.Collections.Immutable;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7TargetScan(long Tick, int Slot, string Title, string Name, string Description, int Level, uint Hp, uint MaximumHp, int Strength, int Vitality, int Speed, int Magic, int Spirit, int Evasion, ImmutableArray<int> ElementResistances, uint StatusImmunities);

public sealed partial class Ff7BattleSession
{
    public Ff7TargetScan? LastSense { get; private set; }

    public Ff7TargetScan ReadTargetScan(int slot, string title)
    {
        Ff7BattleActorState target = Actor(slot);
        Ff7BattleAiActor actor = target.Actor;
        SceneBattleEnemy enemy = Enemy(slot);
        ImmutableArray<int> defenses = Enumerable.Range(0, 9).Select(index =>
        {
            uint bit = 1u << index;
            if (((actor.ElementNullify ?? 0) & bit) != 0)
            {
                return 100;
            }
            if ((((actor.ElementAbsorb ?? 0) | (actor.ExtraElementAbsorb ?? 0)) & bit) != 0)
            {
                return 200;
            }
            bool weak = ((actor.ElementWeakness ?? 0) & bit) != 0;
            bool half = ((actor.ElementHalve ?? 0) & bit) != 0;
            return 100 - (weak ? 200 : 100) / (half ? 2 : 1);
        }).ToImmutableArray();
        return new(Tick, slot, title, Name(slot), EnemyExtension(slot)?.ScanDescription ?? "", actor.Level ?? enemy.Level, actor.HitPoints, actor.MaxHitPoints, actor.Strength ?? enemy.Strength, actor.Defense, actor.Speed ?? enemy.Speed, actor.Magic ?? enemy.Magic, actor.MagicDefense, actor.PhysicalEvasion ?? enemy.Evasion, defenses, ~enemy.NativeStatusMask | target.StatusProtection);
    }

    private void ApplySense(Ff7BattleQueuedCommand command)
    {
        Ff7BattleActorState? target = state.Actors.FirstOrDefault(actor => actor.Actor.Slot >= 4 && (command.TargetMask & 1 << actor.Actor.Slot) != 0 && IsAlive(actor) && (actor.Actor.Conditions & 8) != 0);
        if (target is not null)
        {
            LastSense = ReadTargetScan(target.Actor.Slot, commandResources?.Name((byte)Ff7BattleCommand.Sense) ?? "Sense");
        }
    }
}
