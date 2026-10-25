using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

public sealed record GuardianForceCompatibilityProjection(NativeGuardianForceLearnedBank LearnedBank, ImmutableArray<byte> AbilityProgress, byte Learning, uint ForgottenAbilities, ImmutableArray<NativeGuardianForceLearningRule> LearningRules);

public sealed partial record NativeCharacterVitals
{
    public GuardianForceAbilityProfile? GuardianForceRecoveryProfile
    {
        get; init;
    }
    public ImmutableArray<GuardianForceProgression?> GuardianForceRecoveryProgression { get; init; } = [];
    public ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> GuardianForceRecoveryTemplates { get; init; } = [];
    public bool GuardianForceOpaqueBaseline
    {
        get; init;
    }
    public ImmutableArray<string> GuardianForceCompatibilityDiagnostics { get; init; } = [];

    public NativeCharacterVitals NormalizeGuardianForceCompatibility()
    {
        if (GuardianForces.All(gf => gf.Progression is null))
        {
            return this;
        }
        Validate();
        NativeCharacterVitals result = DeepCopy();
        bool changed = false;
        List<string> diagnostics = GuardianForceCompatibilityDiagnostics.ToList();
        for (int id = 0; id < 16; id++)
        {
            NativeGuardianForceVital gf = result.GuardianForces[id];
            if (gf.Progression is null)
            {
                continue;
            }
            GuardianForceCompatibilityProjection? projection = gf.LastProjection;
            bool observed = projection is not null && (projection.LearnedBank != gf.LearnedBank || projection.Learning != gf.Learning || projection.ForgottenAbilities != gf.ForgottenAbilities || !projection.AbilityProgress.SequenceEqual(gf.AbilityProgress) || !projection.LearningRules.SequenceEqual(MenuDefinitions.Learning[id]));
            if (observed && (!projection!.LearningRules.SequenceEqual(MenuDefinitions.Learning[id]) || projection.LearningRules.Where(rule => rule.Ability != 0).GroupBy(rule => rule.Ability).Any(group => group.Count() > 1)))
            {
                return RecoverOpaqueGuardianForceBaseline($"GF {id}: external compatibility structure is ambiguous; previous mod attribution is baked into the observed recovery baseline.");
            }
            if (observed)
            {
                Dictionary<int, GuardianForceAbilityProgress> ledger = gf.Progression.Abilities.ToDictionary(entry => entry.Ability);
                for (int ability = 1; ability < MenuDefinitions.AbilityPoints.Length; ability++)
                {
                    bool before = projection!.LearnedBank.Has(ability);
                    bool after = gf.LearnedBank.Has(ability);
                    if (before != after)
                    {
                        GuardianForceAbilityProgress entry = ledger.GetValueOrDefault(ability) ?? new(ability, false);
                        ledger[ability] = entry with
                        {
                            Learned = after,
                            Forgotten = after ? false : entry.Forgotten,
                            ItemTaught = entry.ItemTaught || after && !ResolveGuardianForceTemplates()[id].Any(template => template.Ability == ability)
                        };
                    }
                }
                for (int index = 0; index < projection!.LearningRules.Length; index++)
                {
                    int ability = projection.LearningRules[index].Ability;
                    if (ability <= 0 || ability >= MenuDefinitions.AbilityPoints.Length)
                    {
                        continue;
                    }
                    bool oldForgotten = (projection.ForgottenAbilities & (1u << index)) != 0;
                    bool newForgotten = (gf.ForgottenAbilities & (1u << index)) != 0;
                    bool apChanged = projection.AbilityProgress[index] != gf.AbilityProgress[index];
                    if (apChanged || oldForgotten != newForgotten)
                    {
                        GuardianForceAbilityProgress entry = ledger.GetValueOrDefault(ability) ?? new(ability, gf.LearnedBank.Has(ability));
                        ledger[ability] = entry with
                        {
                            EarnedPoints = apChanged ? gf.AbilityProgress[index] : entry.EarnedPoints,
                            Forgotten = newForgotten,
                            Learned = newForgotten ? false : entry.Learned
                        };
                    }
                }
                result.GuardianForces[id] = gf with
                {
                    MoveFindDisabled = gf.MoveFindDisabled && (projection!.LearnedBank.Has(79) || !gf.LearnedBank.Has(79)),
                    Progression = new(true, ledger.Values.OrderBy(entry => entry.Ability).ToImmutableArray())
                };
                diagnostics.Add($"GF {id}: adopted external learned/AP/forgetting/learning changes by saved ability identity.");
                changed = true;
            }
            if (projection is null)
            {
                NativeCharacterVitals expected = result.ApplyGuardianForceTemplates(result.ResolveGuardianForceTemplates());
                NativeGuardianForceVital mapped = expected.GuardianForces[id];
                if (gf.LearnedBank != mapped.LearnedBank || !gf.AbilityProgress.SequenceEqual(mapped.AbilityProgress) || gf.ForgottenAbilities != mapped.ForgottenAbilities)
                {
                    return RecoverOpaqueGuardianForceBaseline($"GF {id}: compatibility metadata has no saved projection and observed progress differs; prior attribution is unavailable.");
                }
                result.GuardianForces[id] = gf with
                {
                    LastProjection = mapped.LastProjection
                };
                changed = true;
            }
            if ((gf.Flags & 1) != 0 && !gf.Progression.Initialized)
            {
                changed = true;
            }
        }
        return changed ? (result with
        {
            GuardianForceCompatibilityDiagnostics = diagnostics.Distinct().TakeLast(32).ToImmutableArray()
        }).ApplyGuardianForceTemplates(ResolveGuardianForceTemplates()) : this;
    }

    private NativeCharacterVitals RecoverOpaqueGuardianForceBaseline(string diagnostic)
    {
        NativeCharacterVitals observed = DeepCopy() with
        {
            MenuDefinitions = MenuDefinitions with
            {
                Templates = default,
                SourceTemplates = default
            },
            GuardianForceRecoveryProfile = GuardianForceProfile ?? GuardianForceRecoveryProfile,
            GuardianForceRecoveryProgression = GuardianForces.Select(gf => gf.Progression).ToImmutableArray(),
            GuardianForceRecoveryTemplates = ResolveGuardianForceTemplates(),
            GuardianForceProfile = null,
            GuardianForceOpaqueBaseline = true,
            GuardianForceCompatibilityDiagnostics = GuardianForceCompatibilityDiagnostics.Append(diagnostic).Distinct().TakeLast(32).ToImmutableArray()
        };
        for (int id = 0; id < 16; id++)
        {
            observed.GuardianForces[id] = observed.GuardianForces[id] with
            {
                Progression = null,
                LastProjection = null
            };
        }
        var baseline = observed.ResolveGuardianForceTemplates();
        observed = observed with
        {
            MenuDefinitions = observed.MenuDefinitions with
            {
                Templates = baseline,
                SourceTemplates = baseline
            }
        };
        NativeCharacterVitals recovered = observed.ApplyGuardianForceTemplates(baseline);
        for (int id = 0; id < 16; id++)
        {
            GuardianForceProgression? previous = GuardianForces[id].Progression;
            if (previous is null)
            {
                continue;
            }
            NativeGuardianForceVital gf = recovered.GuardianForces[id];
            HashSet<int> present = gf.Progression!.Abilities.Select(entry => entry.Ability).ToHashSet();
            recovered.GuardianForces[id] = gf with
            {
                Progression = gf.Progression with
                {
                    Abilities = gf.Progression.Abilities.AddRange(previous.Abilities.Where(entry => !present.Contains(entry.Ability)).Select(entry => entry with { Active = false, ItemTaught = false }))
                }
            };
        }
        return recovered.ApplyGuardianForceTemplates(baseline);
    }
}

public sealed partial class GameSession
{
    private static GameState NormalizeGuardianForceCompatibilityState(GameState state)
    {
        if (state.CharacterVitals is not { } source)
        {
            return state;
        }
        NativeCharacterVitals normalized = source.NormalizeGuardianForceCompatibility();
        if (ReferenceEquals(source, normalized))
        {
            return state;
        }
        if (state.Battle is not null || state.PendingBattleEncounter is not null)
        {
            throw new InvalidDataException("Externally changed GF progression must be recovered at a standalone checkpoint outside battle.");
        }
        static JsonElement? Update(JsonElement? checkpoint, NativeCharacterVitals vitals)
        {
            if (checkpoint is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty("CharacterVitals", out _))
            {
                return checkpoint;
            }
            JsonObject json = JsonNode.Parse(element.GetRawText())!.AsObject();
            json["CharacterVitals"] = JsonSerializer.SerializeToNode(vitals);
            return JsonSerializer.SerializeToElement(json);
        }
        return state with
        {
            CharacterVitals = normalized,
            EventState = Update(state.EventState, normalized),
            DepartingFieldState = Update(state.DepartingFieldState, normalized)
        };
    }
}
