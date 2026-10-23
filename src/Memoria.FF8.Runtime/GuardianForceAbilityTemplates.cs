using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record GuardianForceAbilityTemplate(int Ability, bool InitialLearned, int RequiredPoints, int RequiredLevel = 1, int? PrerequisiteAbility = null, int? ExcludedAbility = null, int LearningPriority = 0, bool SupportedCondition = true);
public sealed record GuardianForceAbilityProgress(int Ability, bool Learned, int EarnedPoints = 0, bool Forgotten = false, bool ItemTaught = false, bool Active = true);
public sealed record GuardianForceProgression(bool Initialized, ImmutableArray<GuardianForceAbilityProgress> Abilities);

public sealed partial record NativeCharacterVitals
{
    [JsonIgnore]
    public bool GuardianForceRequiresMemoriaFormat
    {
        get
        {
            if (GuardianForceOpaqueBaseline || GuardianForceRecoveryProfile is not null || GuardianForceProfile is not null || !MenuDefinitions.Templates.IsDefault && (MenuDefinitions.SourceTemplates.IsDefault || !MenuDefinitions.Templates.SelectMany((row, id) => row.Select(entry => (id, entry))).SequenceEqual(MenuDefinitions.SourceTemplates.SelectMany((row, id) => row.Select(entry => (id, entry))))))
            {
                return true;
            }
            for (int id = 0; id < 16; id++)
            {
                NativeGuardianForceVital gf = GuardianForces[id];
                if (GuardianForceAbilities(id).Count(entry => entry.Learned) > 22)
                {
                    return true;
                }
                if (gf.Progression is { } progression)
                {
                    HashSet<int> active = ResolveGuardianForceTemplates()[id].Select(entry => entry.Ability).ToHashSet();
                    foreach (GuardianForceAbilityProgress entry in progression.Abilities)
                    {
                        int index = Array.FindIndex(MenuDefinitions.Learning[id].ToArray(), rule => rule.Ability == entry.Ability);
                        if (!active.Contains(entry.Ability) && !entry.ItemTaught || entry.Learned != gf.LearnedBank.Has(entry.Ability) || index < 0 && (entry.EarnedPoints != 0 || entry.Forgotten) || index >= 0 && (entry.EarnedPoints != gf.AbilityProgress[index] || entry.Forgotten != ((gf.ForgottenAbilities & (1u << index)) != 0)))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    public ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> ResolveGuardianForceTemplates()
    {
        if (!MenuDefinitions.Templates.IsDefault)
        {
            return MenuDefinitions.Templates;
        }
        return Enumerable.Range(0, 16).Select(id =>
        {
            ImmutableArray<NativeGuardianForceLearningRule> rules = MenuDefinitions.Learning[id];
            List<GuardianForceAbilityTemplate> entries = [];
            HashSet<int> ambiguous = rules.Where(rule => rule.Ability != 0).GroupBy(rule => (int)rule.Ability).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
            for (int index = 0; index < rules.Length; index++)
            {
                NativeGuardianForceLearningRule rule = rules[index];
                if (rule.Ability == 0 || entries.Any(entry => entry.Ability == rule.Ability))
                {
                    continue;
                }
                entries.Add(new(rule.Ability, GuardianForces[id].LearnedBank.Has(rule.Ability), rule.Ability < MenuDefinitions.AbilityPoints.Length ? MenuDefinitions.AbilityPoints[rule.Ability] : 0,
                    rule.Requirement <= 100 ? Math.Max(1, (int)rule.Requirement) : 1, rule.Requirement is >= 101 and <= 121 ? rules[rule.Requirement - 101].Ability : null,
                    rule.Requirement is >= 101 and <= 121 && rule.Exclusion != 255 ? rules[rule.Exclusion].Ability : null, index, rule.Requirement <= 121 && rule.Ability < MenuDefinitions.AbilityPoints.Length && !ambiguous.Contains(rule.Ability)));
            }
            for (int ability = 1; ability < MenuDefinitions.AbilityPoints.Length; ability++)
            {
                if (GuardianForces[id].LearnedBank.Has(ability) && entries.All(entry => entry.Ability != ability))
                {
                    entries.Add(new(ability, true, MenuDefinitions.AbilityPoints[ability], LearningPriority: entries.Count));
                }
            }
            return entries.OrderBy(entry => entry.Ability).ToImmutableArray();
        }).ToImmutableArray();
    }

    public NativeCharacterVitals ApplyGuardianForceTemplates(ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates)
    {
        NativeGuardianForceMenuDefinitions menu = MenuDefinitions with
        {
            Templates = templates
        };
        menu.Validate();
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> previous = ResolveGuardianForceTemplates();
        NativeCharacterVitals result = DeepCopy() with
        {
            MenuDefinitions = menu
        };
        for (int id = 0; id < 16; id++)
        {
            NativeGuardianForceVital gf = GuardianForces[id];
            GuardianForceProgression progression = gf.Progression ?? ImportGuardianForceProgression(id, previous[id]);
            Dictionary<int, GuardianForceAbilityProgress> ledger = progression.Abilities.ToDictionary(entry => entry.Ability);
            bool initialize = progression.Initialized || (gf.Flags & 1) != 0;
            if ((gf.Flags & 1) != 0)
            {
                foreach (GuardianForceAbilityTemplate entry in templates[id])
                {
                    if (entry.Ability < MenuDefinitions.AbilityPoints.Length)
                    {
                        ledger.TryAdd(entry.Ability, new(entry.Ability, entry.InitialLearned));
                    }
                }
            }
            gf = gf with
            {
                Progression = new(initialize, ledger.Values.Select(entry => entry with { Active = templates[id].Any(template => template.Ability == entry.Ability) || entry.ItemTaught }).OrderBy(entry => entry.Ability).ToImmutableArray())
            };
            result.GuardianForces[id] = gf;
            result.GuardianForces[id] = result.SynchronizeGuardianForce(id);
        }
        return result.ReconcileGuardianForceJunctions();
    }

    private GuardianForceProgression ImportGuardianForceProgression(int id, ImmutableArray<GuardianForceAbilityTemplate> templates)
    {
        NativeGuardianForceVital gf = GuardianForces[id];
        bool exceptional;
        if (!MenuDefinitions.InitialStates.IsDefault)
        {
            NativeGuardianForceInitialState initial = MenuDefinitions.InitialStates[id];
            exceptional = gf.Experience != 0 || gf.LearnedBank != initial.LearnedBank || gf.Learning != initial.Learning || gf.ForgottenAbilities != initial.ForgottenAbilities || !gf.AbilityProgress.SequenceEqual(initial.AbilityProgress);
        }
        else
        {
            // Without a complete initialization baseline an imported record is ambiguous.
            exceptional = true;
        }
        bool initialized = (gf.Flags & 1) != 0 || exceptional;
        List<GuardianForceAbilityProgress> entries = [];
        if (initialized)
        {
            for (int ability = 1; ability < MenuDefinitions.AbilityPoints.Length; ability++)
            {
                int index = Array.FindIndex(MenuDefinitions.Learning[id].ToArray(), rule => rule.Ability == ability);
                if (gf.LearnedBank.Has(ability) || index >= 0)
                {
                    entries.Add(new(ability, gf.LearnedBank.Has(ability), index >= 0 ? gf.AbilityProgress[index] : 0, index >= 0 && (gf.ForgottenAbilities & (1u << index)) != 0, !templates.Any(entry => entry.Ability == ability)));
                }
            }
        }
        return new(initialized, entries.ToImmutableArray());
    }

    private NativeGuardianForceVital SynchronizeGuardianForce(int id)
    {
        NativeGuardianForceVital gf = GuardianForces[id];
        if (gf.Progression is null)
        {
            return gf;
        }
        HashSet<int> active = ResolveGuardianForceTemplates()[id].Select(entry => entry.Ability).ToHashSet();
        NativeGuardianForceLearnedBank bank = new(gf.LearnedBank.Low & 1UL, gf.LearnedBank.High & (0xFFFUL << 52));
        foreach (GuardianForceAbilityProgress entry in gf.Progression.Abilities.Where(entry => !entry.Forgotten && entry.Learned && (active.Contains(entry.Ability) || entry.ItemTaught)))
        {
            bank = entry.Ability < 64 ? bank with
            {
                Low = bank.Low | (1UL << entry.Ability)
            } : bank with
            {
                High = bank.High | (1UL << (entry.Ability - 64))
            };
        }
        NativeCharacterVitals view = this with
        {
            GuardianForces = GuardianForces.ToArray()
        };
        view.GuardianForces[id] = gf with
        {
            LearnedBank = bank
        };
        HashSet<int> available = view.GuardianForceAbilities(id).Where(entry => !entry.Learned).Select(entry => entry.Ability).ToHashSet();
        int target = available.Contains(gf.Learning) ? gf.Learning : ResolveGuardianForceTemplates()[id].OrderBy(entry => entry.LearningPriority).FirstOrDefault(entry => available.Contains(entry.Ability))?.Ability ?? 0;
        ImmutableArray<byte> nativeProgress = gf.AbilityProgress;
        uint forgotten = gf.ForgottenAbilities & 0xE00000u;
        for (int index = 0; index < MenuDefinitions.Learning[id].Length; index++)
        {
            GuardianForceAbilityProgress? entry = gf.Progression.Abilities.FirstOrDefault(entry => entry.Ability == MenuDefinitions.Learning[id][index].Ability);
            if (entry is not null)
            {
                nativeProgress = nativeProgress.SetItem(index, checked((byte)Math.Min(255, entry.EarnedPoints)));
            }
            if (entry?.Forgotten == true)
            {
                forgotten |= 1u << index;
            }
        }
        return gf with
        {
            LearnedBank = bank,
            Learning = checked((byte)target),
            AbilityProgress = nativeProgress,
            ForgottenAbilities = forgotten,
            LastProjection = new(bank, nativeProgress, checked((byte)target), forgotten, MenuDefinitions.Learning[id])
        };
    }

    public NativeCharacterVitals TeachGuardianForceAbility(int id, int ability)
    {
        NativeCharacterVitals result = ApplyGuardianForceTemplates(ResolveGuardianForceTemplates());
        NativeGuardianForceVital gf = result.GuardianForces[id];
        if ((gf.Flags & 1) == 0 || ability <= 0 || ability >= MenuDefinitions.AbilityPoints.Length)
        {
            throw new InvalidOperationException("Teaching requires an acquired GF and a supported ability.");
        }
        GuardianForceAbilityProgress? previous = gf.Progression!.Abilities.FirstOrDefault(entry => entry.Ability == ability);
        ImmutableArray<GuardianForceAbilityProgress> ledger = gf.Progression.Abilities.Where(entry => entry.Ability != ability).Append(new GuardianForceAbilityProgress(ability, true, previous?.EarnedPoints ?? 0, ItemTaught: true)).OrderBy(entry => entry.Ability).ToImmutableArray();
        result.GuardianForces[id] = gf with
        {
            Progression = new(true, ledger)
        };
        result.GuardianForces[id] = result.SynchronizeGuardianForce(id);
        return result.RebuildParty();
    }

    public NativeCharacterVitals ReconcileGuardianForceJunctions()
    {
        NativeCharacterVitals result = DeepCopy();
        for (int row = 0; row < Records.Length; row++)
        {
            IReadOnlySet<int> learned = result.JunctionLearnedAbilities(row);
            NativeCharacterJunction junction = result.Records[row].Junction;
            uint commands = junction.Commands;
            uint passives = junction.Abilities;
            byte[] magic = junction.MagicJunctions.ToArray();
            for (int slot = 0; slot < 4; slot++)
            {
                if (!learned.Contains((byte)(commands >> (8 * slot))))
                {
                    commands &= ~(255u << (8 * slot));
                }
                if (slot >= result.JunctionAbilitySlots(row) || !learned.Contains((byte)(passives >> (8 * slot))))
                {
                    passives &= ~(255u << (8 * slot));
                }
            }
            for (int slot = 0; slot < magic.Length; slot++)
            {
                if (!result.JunctionMagicSlotAvailable(row, slot))
                {
                    magic[slot] = 0;
                }
            }
            result.Records[row] = result.Records[row] with
            {
                Junction = junction with
                {
                    Commands = commands,
                    Abilities = passives,
                    MagicJunctions = magic
                }
            };
            result = result.RebuildMagicMenuRow(row);
        }
        return result.RebuildParty();
    }
}
