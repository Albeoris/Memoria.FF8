using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Projects GF progress by ability identity while keeping selected profile definitions outside the gameplay document.</summary>
internal static class GameplaySaveGuardianForceAdapter
{
    internal static NativeCharacterVitals PrepareCapture(NativeCharacterVitals source, out GameplaySaveGuardianForceRecovery? recovery)
    {
        recovery = GameplaySaveGuardianForceRecovery.Capture(source);
        NativeCharacterVitals converted = ImportOrphanedBindings(source);
        NativeCharacterVitals normalized = converted.NormalizeGuardianForceCompatibility();
        RequireResolved(normalized);
        NativeCharacterVitals mapped = normalized.ApplyGuardianForceTemplates(normalized.ResolveGuardianForceTemplates());
        NativeCharacterVitals result = source.DeepCopy();
        if (!normalized.GuardianForceRecoveryProgression.IsDefaultOrEmpty && normalized.GuardianForceRecoveryProgression.Length != 16)
        {
            throw new InvalidDataException("Gameplay GF projection requires complete semantic recovery history.");
        }
        for (int id = 0; id < result.GuardianForces.Length; id++)
        {
            GuardianForceProgression progression = mapped.GuardianForces[id].Progression!;
            if (!normalized.GuardianForceRecoveryProgression.IsDefaultOrEmpty && normalized.GuardianForceRecoveryProgression[id] is { } historical)
            {
                ValidateHistory(historical, normalized.MenuDefinitions.AbilityPoints.Length);
                HashSet<int> represented = progression.Abilities.Select(entry => entry.Ability).ToHashSet();
                progression = progression with
                {
                    Abilities = progression.Abilities.AddRange(historical.Abilities.Where(entry => !entry.ItemTaught && !represented.Contains(entry.Ability)).Select(entry => entry with { Active = false })).OrderBy(entry => entry.Ability).ToImmutableArray()
                };
            }
            NativeGuardianForceVital gf = normalized.GuardianForces[id] with { HitPoints = source.GuardianForces[id].HitPoints, Progression = progression };
            result.GuardianForces[id] = PreserveUnmappedFlags(gf, source.GuardianForces[id], source.MenuDefinitions.ResolveLearning(id));
        }
        return result;
    }

    private static NativeCharacterVitals ImportOrphanedBindings(NativeCharacterVitals source)
    {
        NativeCharacterVitals result = source.DeepCopy();
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = source.ResolveGuardianForceTemplates();
        for (int id = 0; id < source.GuardianForces.Length; id++)
        {
            NativeGuardianForceVital gf = source.GuardianForces[id];
            ImmutableArray<NativeGuardianForceLearningRule> current = source.MenuDefinitions.ResolveLearning(id);
            if (gf.Progression is not null || gf.LastProjection is not { } projection || projection.LearningRules.SequenceEqual(current))
            {
                continue;
            }
            ImmutableArray<NativeGuardianForceLearningRule> previous = projection.LearningRules;
            if (previous.IsDefault || previous.Length != 21 || previous.Any(rule => rule is null || rule.Ability >= source.MenuDefinitions.AbilityPoints.Length) || previous.Where(rule => rule.Ability != 0).Select(rule => rule.Ability).Distinct().Count() != previous.Count(rule => rule.Ability != 0))
            {
                throw new NotSupportedException("Gameplay GF orphan recovery requires a complete unambiguous retained ability mapping; preserve the execution snapshot.");
            }
            GuardianForceProgression progression = source.ImportGuardianForceProgression(id, templates[id], previous);
            GameplaySaveGuardianForce saved = GameplaySaveGuardianForce.Capture(gf) with { Progression = progression, AbilityBindings = previous.Select(rule => rule.Ability).ToImmutableArray() };
            NativeCharacterVitals isolated = source.DeepCopy();
            isolated.GuardianForces[id] = RebindSavedSlots(gf with { Progression = progression }, saved, current);
            NativeGuardianForceVital mapped = isolated.ApplyGuardianForceTemplates(templates).GuardianForces[id];
            result.GuardianForces[id] = mapped with { HitPoints = gf.HitPoints };
        }
        return result;
    }

    internal static NativeCharacterVitals Restore(NativeCharacterVitals restored, NativeCharacterVitals installed, GameplaySaveGuardianForce[] saved)
    {
        RequireResolved(installed);
        NativeCharacterVitals prepared = restored.DeepCopy() with
        {
            GuardianForceRecoveryProfile = null,
            GuardianForceRecoveryProgression = [],
            GuardianForceRecoveryTemplates = [],
            GuardianForceOpaqueBaseline = false,
            GuardianForceCompatibilityDiagnostics = []
        };
        for (int id = 0; id < prepared.GuardianForces.Length; id++)
        {
            prepared.GuardianForces[id] = RebindSavedSlots(prepared.GuardianForces[id], saved[id], installed.MenuDefinitions.ResolveLearning(id));
        }
        NativeCharacterVitals mapped = prepared.ApplyGuardianForceTemplates(installed.ResolveGuardianForceTemplates());
        NativeCharacterVitals result = prepared with { GuardianForces = mapped.GuardianForces };
        for (int id = 0; id < result.GuardianForces.Length; id++)
        {
            NativeGuardianForceVital gf = result.GuardianForces[id] with { MoveFindDisabled = restored.GuardianForces[id].MoveFindDisabled, HitPoints = restored.GuardianForces[id].HitPoints };
            gf = PreserveUnmappedFlags(gf, prepared.GuardianForces[id], installed.MenuDefinitions.ResolveLearning(id));
            result.GuardianForces[id] = gf with { MaximumHitPoints = installed.Definitions.GuardianForceMaximum(id, gf) };
        }
        result.Validate();
        return result;
    }

    private static NativeGuardianForceVital PreserveUnmappedFlags(NativeGuardianForceVital gf, NativeGuardianForceVital saved, ImmutableArray<NativeGuardianForceLearningRule> rules)
    {
        uint mask = 0;
        for (int slot = 0; slot < rules.Length; slot++)
        {
            if (rules[slot].Ability == 0)
            {
                mask |= 1u << slot;
            }
        }
        uint forgotten = gf.ForgottenAbilities | (saved.ForgottenAbilities & mask);
        return gf with { ForgottenAbilities = forgotten, LastProjection = gf.LastProjection is null ? null : gf.LastProjection with { ForgottenAbilities = forgotten } };
    }

    private static NativeGuardianForceVital RebindSavedSlots(NativeGuardianForceVital gf, GameplaySaveGuardianForce saved, ImmutableArray<NativeGuardianForceLearningRule> selected)
    {
        if (saved.AbilityBindings.IsDefault)
        {
            return gf;
        }
        byte[] points = gf.AbilityProgress.ToArray();
        uint forgotten = gf.ForgottenAbilities & 0xE00000u;
        Dictionary<byte, int> occurrences = [];
        for (int slot = 0; slot < selected.Length; slot++)
        {
            byte ability = selected[slot].Ability;
            if (ability == 0)
            {
                forgotten |= gf.ForgottenAbilities & (1u << slot);
                continue;
            }
            int occurrence = occurrences.GetValueOrDefault(ability);
            occurrences[ability] = occurrence + 1;
            int[] previous = saved.AbilityBindings.Select((id, index) => (id, index)).Where(entry => entry.id == ability).Select(entry => entry.index).ToArray();
            if (occurrence < previous.Length)
            {
                int original = previous[occurrence];
                points[slot] = saved.AbilityProgress[original];
                if ((saved.ForgottenAbilities & (1u << original)) != 0)
                {
                    forgotten |= 1u << slot;
                }
            }
            else
            {
                GuardianForceAbilityProgress? progress = saved.Progression?.Abilities.FirstOrDefault(entry => entry.Ability == ability);
                points[slot] = checked((byte)(progress?.EarnedPoints ?? 0));
                if (progress?.Forgotten == true)
                {
                    forgotten |= 1u << slot;
                }
            }
        }
        return gf with { AbilityProgress = points.ToImmutableArray(), ForgottenAbilities = forgotten };
    }

    private static void RequireResolved(NativeCharacterVitals source)
    {
        if (source.GuardianForceProfile is null && (source.GuardianForceOpaqueBaseline || source.GuardianForceRecoveryProfile is not null || !source.GuardianForceRecoveryTemplates.IsDefaultOrEmpty))
        {
            throw new NotSupportedException("Gameplay GF projection requires resolving the observed compatibility baseline before replacing its pending profile attribution; preserve the execution snapshot.");
        }
    }

    private static void ValidateHistory(GuardianForceProgression history, int abilityCount)
    {
        if (history.Abilities.IsDefault || history.Abilities.Any(entry => entry is null || entry.Ability <= 0 || entry.Ability >= abilityCount || entry.EarnedPoints is < 0 or > 255 || entry.Learned && entry.Forgotten) || history.Abilities.Select(entry => entry.Ability).Distinct().Count() != history.Abilities.Length)
        {
            throw new InvalidDataException("Gameplay GF projection requires distinct supported recovery abilities and bounded earned AP.");
        }
    }
}
