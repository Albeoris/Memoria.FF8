using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record NativeGuardianForceLegacyLoss(int GuardianForce, int? Ability, string Kind, string Description)
{
    public string? DescriptionId { get; init; }
    public IReadOnlyList<int> DescriptionArguments { get; init; } = [];
}

/// <summary>A deterministic export proposal. The projected state owns copies of all mutable character data.</summary>
public sealed record NativeGuardianForceLegacyExportPlan(GameState ProjectedState, IReadOnlyList<NativeGuardianForceLegacyLoss> Losses)
{
    public bool RequiresConfirmation => Losses.Count != 0;
}

/// <summary>Projects GF state onto the learning definitions supplied by the target original game.</summary>
public static class NativeGuardianForceLegacyCompatibility
{
    public static NativeGuardianForceLegacyExportPlan Analyze(GameState state, NativeCharacterVitals targetSource)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(targetSource);
        NativeCharacterVitals current = state.CharacterVitals ?? throw new NotSupportedException("GF export requires initialized characters.");
        current.Validate();
        targetSource.Validate();
        if (targetSource.MenuDefinitions.Learning.Any(rules => rules.Length != 21))
        {
            throw new NotSupportedException("Select the original target game's source definitions with twenty-one GF learning slots.");
        }
        List<NativeGuardianForceLegacyLoss> losses = [];
        NativeCharacterVitals projected = current.DeepCopy() with
        {
            MenuDefinitions = targetSource.MenuDefinitions with
            {
                Templates = default
            },
            Definitions = targetSource.Definitions,
            GuardianForceProfile = null,
            GuardianForceRecoveryProfile = null,
            GuardianForceRecoveryProgression = [],
            GuardianForceRecoveryTemplates = [],
            GuardianForceOpaqueBaseline = false,
            GuardianForceCompatibilityDiagnostics = []
        };
        if (current.GuardianForceProfile is not null || current.GuardianForceRecoveryProfile is not null || current.GuardianForceOpaqueBaseline)
        {
            losses.Add(new(-1, null, "Attribution", "GF mod-layer identities, save-local contributions and their attribution cannot be retained in an original FFVIII save.") { DescriptionId = "attribution" });
        }
        for (int id = 0; id < 16; id++)
        {
            ImmutableArray<NativeGuardianForceLearningRule> sourceRules = current.MenuDefinitions.Learning[id];
            ImmutableArray<NativeGuardianForceLearningRule> targetRules = targetSource.MenuDefinitions.Learning[id];
            NativeGuardianForceVital gf = current.GuardianForces[id];
            void Loss(int? ability, string kind, string detail, string textId, params int[] arguments) => losses.Add(new(id, ability, kind, $"GF {id}: {detail}") { DescriptionId = textId, DescriptionArguments = [id, .. arguments] });
            if (gf.MoveFindDisabled)
            {
                Loss(79, "Passive", "the disabled Move-Find preference cannot be retained in an original FFVIII save.", "passive");
            }
            if (!sourceRules.SequenceEqual(targetRules) || !current.MenuDefinitions.Templates.IsDefault && !current.ResolveGuardianForceTemplates()[id].SequenceEqual(targetSource.ResolveGuardianForceTemplates()[id]))
            {
                Loss(null, "Configuration", "authored learning order, entries or conditions revert to the target game's definitions.", "configuration");
            }
            if (gf.Progression is { Initialized: false })
            {
                NativeGuardianForceInitialState initial = !targetSource.MenuDefinitions.InitialStates.IsDefault
                    ? targetSource.MenuDefinitions.InitialStates[id]
                    : new(targetSource.GuardianForces[id].LearnedBank, targetSource.GuardianForces[id].AbilityProgress, targetSource.GuardianForces[id].Learning, targetSource.GuardianForces[id].ForgottenAbilities);
                projected.GuardianForces[id] = gf with
                {
                    LearnedBank = initial.LearnedBank,
                    AbilityProgress = initial.AbilityProgress,
                    Learning = initial.Learning,
                    ForgottenAbilities = initial.ForgottenAbilities,
                    Progression = null,
                    LastProjection = null,
                    MoveFindDisabled = false
                };
                continue;
            }
            byte[] progress = new byte[21];
            uint forgotten = sourceRules.SequenceEqual(targetRules) ? gf.ForgottenAbilities & 0xE00000u : 0;
            if (!sourceRules.SequenceEqual(targetRules) && (gf.ForgottenAbilities & 0xE00000u) != 0)
            {
                Loss(null, "Unknown", "unmapped high forgotten bits cannot be retained after learning structure changes.", "unknown");
            }
            IEnumerable<GuardianForceAbilityProgress> ledger = gf.Progression?.Abilities ?? sourceRules.Select((rule, index) => new GuardianForceAbilityProgress(rule.Ability, gf.LearnedBank.Has(rule.Ability), gf.AbilityProgress[index], (gf.ForgottenAbilities & (1u << index)) != 0)).ToImmutableArray();
            foreach (GuardianForceAbilityProgress entry in ledger)
            {
                int ability = entry.Ability;
                int index = -1;
                for (int source = 0; source < sourceRules.Length; source++)
                {
                    if (sourceRules[source].Ability == ability)
                    {
                        index = source;
                        break;
                    }
                }
                int target = -1;
                for (int candidate = 0; candidate < targetRules.Length; candidate++)
                {
                    if (targetRules[candidate].Ability == ability)
                    {
                        target = candidate;
                        break;
                    }
                }
                if (ability == 0 && index >= 0 && index < 21 && sourceRules[index] == targetRules[index])
                {
                    target = index;
                }
                int ap = entry.EarnedPoints;
                bool isForgotten = entry.Forgotten;
                if (target >= 0)
                {
                    progress[target] = (byte)Math.Clamp(ap, 0, 255);
                    if (ap > 255)
                    {
                        Loss(ability, "Progress", $"ability {ability} AP {ap} is clamped to the target byte range.", "progress-clamped", ability, ap);
                    }
                    if (isForgotten)
                    {
                        forgotten |= 1u << target;
                    }
                    if (target != index && (ap != 0 || isForgotten))
                    {
                        Loss(ability, "Remap", $"ability {ability} AP/forgotten state maps from slot {index} to {target}.", "remap", ability, index, target);
                    }
                }
                else if (ap != 0 || isForgotten)
                {
                    Loss(ability, "Progress", $"ability {ability} has no target learning slot; AP {ap} and forgotten state cannot be preserved.", "progress-missing", ability, ap);
                }
            }
            // Original enumeration visits learned IDs first, in ascending order, with twenty-two effective entries.
            int[] learned = gf.Progression is null ? Enumerable.Range(0, 128).Where(gf.LearnedBank.Has).ToArray() : current.GuardianForceAbilities(id).Where(entry => entry.Learned).Select(entry => entry.Ability).Concat(Enumerable.Range(0, 128).Where(ability => (ability == 0 || ability >= current.MenuDefinitions.AbilityPoints.Length) && gf.LearnedBank.Has(ability))).Distinct().Order().ToArray();
            if (gf.Progression is not null)
            {
                foreach (GuardianForceAbilityProgress entry in gf.Progression.Abilities.Where(entry => entry.Learned && !entry.Forgotten && !learned.Contains(entry.Ability)))
                {
                    Loss(entry.Ability, "Dormant", $"dormant ability {entry.Ability} learning history cannot be retained by the target save.", "dormant", entry.Ability);
                }
            }
            foreach (int ability in learned.Skip(22))
            {
                Loss(ability, "Capacity", $"learned ability {ability} exceeds the target's twenty-two effective entries and is dropped.", "capacity", ability);
            }
            ulong low = 0;
            ulong high = 0;
            foreach (int ability in learned.Take(22))
            {
                if (ability < 64)
                {
                    low |= 1UL << ability;
                }
                else
                {
                    high |= 1UL << (ability - 64);
                }
            }
            NativeGuardianForceVital next = gf with
            {
                AbilityProgress = progress.ToImmutableArray(),
                ForgottenAbilities = forgotten,
                LearnedBank = new(low, high),
                Progression = null,
                LastProjection = null,
                MoveFindDisabled = false
            };
            projected.GuardianForces[id] = next;
            Dictionary<int, bool> available = learned.Take(22).ToDictionary(ability => ability, _ => true);
            int level = NativeStatDefinitions.Level(gf.Experience, targetSource.Definitions.GuardianForces[id]);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int index = 0; index < 21 && available.Count < 22; index++)
                {
                    NativeGuardianForceLearningRule rule = targetRules[index];
                    bool eligible = pass == 0 ? rule.Requirement <= 100 && level >= rule.Requirement : rule.Requirement is >= 101 and <= 121 && available.TryGetValue(targetRules[rule.Requirement - 101].Ability, out bool prerequisite) && prerequisite && (rule.Exclusion == 255 || !available.TryGetValue(targetRules[rule.Exclusion].Ability, out bool excluded) || !excluded);
                    if (eligible && (forgotten & (1u << index)) == 0)
                    {
                        available.TryAdd(rule.Ability, false);
                    }
                }
            }
            foreach (NativeGuardianForceAbility entry in current.GuardianForceAbilities(id).Where(entry => !entry.Learned && !available.ContainsKey(entry.Ability)))
            {
                Loss(entry.Ability, "Availability", $"ability {entry.Ability} is unavailable under the target game's learning conditions or twenty-two-entry limit.", "availability", entry.Ability);
            }
            if (next.Learning != 0 && (!available.TryGetValue(next.Learning, out bool isLearned) || isLearned))
            {
                Loss(next.Learning, "Learning", $"learning selection {next.Learning} is invalid in the target and is cleared.", "learning", next.Learning);
                projected.GuardianForces[id] = next with
                {
                    Learning = 0
                };
            }
        }
        for (int row = 0; row < projected.Records.Length; row++)
        {
            NativeCharacterVital character = projected.Records[row];
            HashSet<int> learned = character.Junction.GuardianForceAssignments.SelectMany(id => Enumerable.Range(0, 128).Where(projected.GuardianForces[id].LearnedBank.Has)).ToHashSet();
            uint commands = character.Junction.Commands;
            uint abilities = character.Junction.Abilities;
            int slots = learned.Contains(19) ? 4 : learned.Contains(18) ? 3 : 2;
            for (int slot = 0; slot < 4; slot++)
            {
                byte command = (byte)(commands >> (8 * slot));
                byte passive = (byte)(abilities >> (8 * slot));
                if (command != 0 && !learned.Contains(command))
                {
                    commands &= ~(255u << (8 * slot));
                    losses.Add(new(-1, command, "Equipped", $"Character {row}: unavailable command {command} is cleared.") { DescriptionId = "equipped-command", DescriptionArguments = [row, command] });
                }
                if (passive != 0 && (slot >= slots || !learned.Contains(passive)))
                {
                    abilities &= ~(255u << (8 * slot));
                    losses.Add(new(-1, passive, "Equipped", $"Character {row}: unavailable passive {passive} is cleared.") { DescriptionId = "equipped-passive", DescriptionArguments = [row, passive] });
                }
            }
            byte[] magic = character.Junction.MagicJunctions.ToArray();
            for (int slot = 0; slot < 19; slot++)
            {
                if (magic[slot] != 0 && !projected.JunctionMagicSlotAvailable(row, slot))
                {
                    magic[slot] = 0;
                    losses.Add(new(-1, null, "Equipped", $"Character {row}: unavailable magic junction slot {slot} is cleared.") { DescriptionId = "equipped-junction", DescriptionArguments = [row, slot] });
                }
            }
            projected.Records[row] = character with
            {
                Junction = character.Junction with
                {
                    Commands = commands,
                    Abilities = abilities,
                    MagicJunctions = magic
                }
            };
        }
        return new(state with
        {
            CharacterVitals = projected
        }, losses);
    }
}
