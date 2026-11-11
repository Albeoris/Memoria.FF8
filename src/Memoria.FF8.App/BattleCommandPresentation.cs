using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Keeps a command's kernel identity alongside its input category.</summary>
internal sealed record BattleCommandPresentation(BattleAbility Ability, int Command, string Name)
{
    internal static BattleAbility AbilityFor(byte command) => command switch
    {
        1 or 12 => BattleAbility.Attack,
        2 => BattleAbility.Magic,
        3 => BattleAbility.GuardianForce,
        4 => BattleAbility.Item,
        6 => BattleAbility.Draw,
        _ => BattleAbility.NativeCommand
    };

    internal static BattleCommandPresentation Default(BattleAbility ability) => ability switch
    {
        BattleAbility.Attack => new(ability, 1, "Attack"),
        BattleAbility.Magic => new(ability, 2, "Magic"),
        BattleAbility.GuardianForce => new(ability, 3, "GF"),
        BattleAbility.Item => new(ability, 4, "Item"),
        BattleAbility.Draw => new(ability, 6, "Draw"),
        _ => throw new InvalidOperationException($"Command category {ability} requires a concrete native command.")
    };

    internal static IReadOnlyList<BattleCommandPresentation> Read(BattleCombatant actor)
    {
        if (!actor.NativeCommands.IsDefaultOrEmpty && actor.Abilities.All(ability => actor.NativeCommands.Any(binding => AbilityFor(binding.Command) == ability)))
        {
            return actor.NativeCommands.Select(binding => new BattleCommandPresentation(AbilityFor(binding.Command), binding.Command, binding.Name)).ToArray();
        }
        List<BattleCommandPresentation> commands = [];
        foreach (BattleAbility ability in actor.Abilities.Distinct())
        {
            if (ability == BattleAbility.LimitBreak && actor.NativeLimit is { } limit)
            {
                commands.Add(new(ability, limit.Command, limit.InvocationChoices.FirstOrDefault()?.Name ?? "Limit"));
                continue;
            }
            BattleNativeCommandBinding[] bindings = actor.NativeCommands.Where(binding => AbilityFor(binding.Command) == ability).ToArray();
            if (bindings.Length == 0)
            {
                commands.Add(Default(ability));
                continue;
            }
            foreach (BattleNativeCommandBinding binding in bindings)
            {
                commands.Add(new(ability, binding.Command, binding.Name));
            }
        }
        return commands;
    }
}
