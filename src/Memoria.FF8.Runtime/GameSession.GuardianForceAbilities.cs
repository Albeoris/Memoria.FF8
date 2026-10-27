using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

public sealed record GuardianForceAbilityProfile(ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> Source, string SourceFingerprint, ImmutableArray<GuardianForceAbilityPatchLayer> AppliedLayers, ImmutableArray<GuardianForceAbilityPatchDocument> LocalDocuments, ImmutableArray<string> ExcludedDocuments)
{
    public int SchemaVersion { get; init; } = 1;
    public ImmutableDictionary<string, string> LocalOwnershipAliases { get; init; } = ImmutableDictionary<string, string>.Empty;

    public void Validate()
    {
        if (SchemaVersion != 1 || Source.IsDefault || Source.Length != 16 || Source.Any(row => row.IsDefault || row.Any(entry => entry is null) || row.Select(entry => entry.Ability).Distinct().Count() != row.Length) || string.IsNullOrWhiteSpace(SourceFingerprint) || AppliedLayers.IsDefault || LocalDocuments.IsDefault || ExcludedDocuments.IsDefault || AppliedLayers.Any(layer => layer is null || string.IsNullOrWhiteSpace(layer.PackageId) || layer.Document is null) || LocalDocuments.Any(document => document is null) || ExcludedDocuments.Any(string.IsNullOrWhiteSpace) || ExcludedDocuments.Distinct(StringComparer.Ordinal).Count() != ExcludedDocuments.Length)
        {
            throw new InvalidDataException("Unsupported or incomplete GF applied profile; preserve this save and use a compatible Memoria version.");
        }
        if (LocalOwnershipAliases is null || LocalOwnershipAliases.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
        {
            throw new InvalidDataException("GF local ownership aliases require explicit document identities.");
        }
        HashSet<string> installed = new(StringComparer.Ordinal);
        foreach (GuardianForceAbilityPatchDocument document in AppliedLayers.Select(layer => layer.Document))
        {
            document.Validate();
            if (!installed.Add(document.DocumentId))
            {
                throw new InvalidDataException("The GF applied profile contains a duplicate installed document identity.");
            }
        }
        HashSet<string> local = new(StringComparer.Ordinal);
        foreach (GuardianForceAbilityPatchDocument document in LocalDocuments)
        {
            document.Validate();
            if (!local.Add(document.DocumentId) || installed.Contains(document.DocumentId) && !ExcludedDocuments.Contains(document.DocumentId, StringComparer.Ordinal))
            {
                throw new InvalidDataException("An installed GF document may become save-local only when its installed contribution is excluded.");
            }
        }
    }
}

public sealed record GuardianForceAbilityReset(int GuardianForce, int Ability, bool Learned);
public sealed record GuardianForceAbilityApplyPreview(NativeCharacterVitals Source, NativeCharacterVitals Vitals, string SourceRevision, long ModGeneration, ImmutableArray<string> Consequences)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public ImmutableArray<GuardianForceAbilityText> ConsequenceTexts { get; init; } = [];

    public ImmutableArray<string> FormatConsequences(Func<string, string, string>? localize = null) => Consequences.Select((original, index) => index < ConsequenceTexts.Length ? ConsequenceTexts[index].Format(localize) : original).ToImmutableArray();
}

public sealed class GuardianForceAbilityPreparedApply
{
    internal GuardianForceAbilityPreparedApply(GameState state)
    {
        State = state;
    }

    internal GameState State
    {
        get;
    }
}

public sealed partial class GameSession
{
    public GuardianForceAbilityApplyPreview PreviewGuardianForceAbilities(ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates, GuardianForceAbilityProfile? profile = null, IEnumerable<GuardianForceAbilityReset>? resets = null, string sourceRevision = "", long modGeneration = 0)
    {
        NativeCharacterVitals source = State.CharacterVitals ?? throw new InvalidOperationException("GF editing requires an initialized playthrough.");
        NativeCharacterVitals result = source.ApplyGuardianForceTemplates(templates) with
        {
            GuardianForceProfile = profile ?? source.GuardianForceProfile
        };
        GuardianForceAbilityReset[] resetEntries = (resets ?? []).ToArray();
        foreach (GuardianForceAbilityReset reset in resetEntries)
        {
            if ((uint)reset.GuardianForce >= 16 || !templates[reset.GuardianForce].Any(entry => entry.Ability == reset.Ability))
            {
                throw new InvalidOperationException("A reset must name an active GF template ability.");
            }
            NativeGuardianForceVital gf = result.GuardianForces[reset.GuardianForce];
            if ((gf.Flags & 1) == 0)
            {
                continue;
            }
            result.GuardianForces[reset.GuardianForce] = gf with
            {
                Progression = gf.Progression! with
                {
                    Abilities = gf.Progression.Abilities.Select(entry => entry.Ability == reset.Ability ? entry with { Learned = reset.Learned, EarnedPoints = 0, Forgotten = false } : entry).ToImmutableArray()
                }
            };
        }
        result = result.ApplyGuardianForceTemplates(templates);
        NativeCharacterVitals priorProgress = source.ApplyGuardianForceTemplates(source.ResolveGuardianForceTemplates());
        List<GuardianForceAbilityText> consequences = [];
        for (int id = 0; id < 16; id++)
        {
            NativeGuardianForceVital before = source.GuardianForces[id];
            NativeGuardianForceVital after = result.GuardianForces[id];
            if (before.Learning != after.Learning)
            {
                consequences.Add(GuardianForceAbilityText.Create("guardian.editor.preview.learning", id, before.Learning, after.Learning));
            }
            foreach (NativeGuardianForceAbility ability in source.GuardianForceAbilities(id).Where(entry => entry.Learned))
            {
                if (!result.GuardianForceAbilities(id).Any(entry => entry.Ability == ability.Ability && entry.Learned))
                {
                    consequences.Add(GuardianForceAbilityText.Create("guardian.editor.preview.inactive", id, ability.Ability));
                }
            }
        }
        for (int id = 0; id < 16; id++)
        {
            HashSet<int> knownLearned = source.GuardianForceAbilities(id).Where(entry => entry.Learned).Select(entry => entry.Ability).ToHashSet();
            foreach (NativeGuardianForceAbility ability in result.GuardianForceAbilities(id).Where(entry => entry.Learned && !knownLearned.Contains(entry.Ability)))
            {
                consequences.Add(GuardianForceAbilityText.Create("guardian.editor.preview.first-learned", id, ability.Ability));
            }
            foreach (GuardianForceAbilityProgress progress in priorProgress.GuardianForces[id].Progression?.Abilities ?? [])
            {
                if (progress.EarnedPoints > 0 && source.ResolveGuardianForceTemplates()[id].Any(entry => entry.Ability == progress.Ability) && !templates[id].Any(entry => entry.Ability == progress.Ability))
                {
                    consequences.Add(GuardianForceAbilityText.Create("guardian.editor.preview.dormant-ap", id, progress.Ability, progress.EarnedPoints));
                }
            }
        }
        for (int row = 0; row < 8; row++)
        {
            NativeCharacterJunction before = source.Records[row].Junction;
            NativeCharacterJunction after = result.Records[row].Junction;
            for (int slot = 0; slot < 4; slot++)
            {
                byte oldCommand = (byte)(before.Commands >> (8 * slot));
                byte newCommand = (byte)(after.Commands >> (8 * slot));
                byte oldPassive = (byte)(before.Abilities >> (8 * slot));
                byte newPassive = (byte)(after.Abilities >> (8 * slot));
                if (oldCommand != newCommand)
                {
                    consequences.Add(GuardianForceAbilityText.Create("guardian.editor.preview.command", row, slot + 1, oldCommand, newCommand));
                }
                if (oldPassive != newPassive)
                {
                    consequences.Add(GuardianForceAbilityText.Create("guardian.editor.preview.passive", row, slot + 1, oldPassive, newPassive));
                }
            }
            for (int slot = 0; slot < before.MagicJunctions.Length; slot++)
            {
                if (before.MagicJunctions[slot] != after.MagicJunctions[slot])
                {
                    consequences.Add(GuardianForceAbilityText.Create("guardian.editor.preview.junction", row, slot, before.MagicJunctions[slot], after.MagicJunctions[slot]));
                }
            }
            if (source.Records[row].HitPoints != result.Records[row].HitPoints)
            {
                consequences.Add(GuardianForceAbilityText.Create("guardian.editor.preview.hp", row, source.Records[row].HitPoints.Minimum, source.Records[row].HitPoints.Maximum, result.Records[row].HitPoints.Minimum, result.Records[row].HitPoints.Maximum));
            }
        }
        foreach (GuardianForceAbilityReset reset in resetEntries)
        {
            NativeGuardianForceAbility? previous = source.GuardianForceAbilities(reset.GuardianForce).FirstOrDefault(entry => entry.Ability == reset.Ability);
            GuardianForceAbilityProgress? progress = priorProgress.GuardianForces[reset.GuardianForce].Progression?.Abilities.FirstOrDefault(entry => entry.Ability == reset.Ability);
            bool learned = progress?.Learned ?? previous?.Learned ?? false;
            bool forgotten = progress?.Forgotten ?? false;
            int points = progress?.EarnedPoints ?? previous?.Progress ?? 0;
            consequences.Add(GuardianForceAbilityText.Create("guardian.editor.preview.reset", reset.GuardianForce, reset.Ability, GuardianForceAbilityText.Boolean(learned), GuardianForceAbilityText.Boolean(forgotten), points, GuardianForceAbilityText.Boolean(reset.Learned), GuardianForceAbilityText.Boolean(false)));
        }
        result.Validate();
        return new(source.DeepCopy(), result, sourceRevision, modGeneration, consequences.Select(text => text.Format()).ToImmutableArray()) { ConsequenceTexts = consequences.ToImmutableArray() };
    }

    public bool CanApplyGuardianForceAbilities(out string? reason, Func<string, string, string>? localize = null)
    {
        if (HasActiveBattle || State.PendingBattleEncounter is not null)
        {
            reason = GuardianForceAbilityText.Create("guardian.editor.boundary.battle").Format(localize);
            return false;
        }
        if (ActiveFieldMutationBoundary != FieldMutationBoundary.None)
        {
            reason = GuardianForceAbilityText.Create("guardian.editor.boundary.transition").Format(localize);
            return false;
        }
        reason = null;
        return true;
    }

    public GuardianForceAbilityPreparedApply PrepareGuardianForceAbilities(GuardianForceAbilityApplyPreview preview, string sourceRevision = "", long modGeneration = 0)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!CanApplyGuardianForceAbilities(out string? reason))
        {
            throw new InvalidOperationException(reason);
        }
        if (sourceRevision != preview.SourceRevision || modGeneration != preview.ModGeneration || State.CharacterVitals is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(State.CharacterVitals), JsonSerializer.SerializeToElement(preview.Source)))
        {
            throw new InvalidOperationException("The GF review is stale. Recompute and review the current playthrough before applying.");
        }
        NativeCharacterVitals vitals = preview.Vitals.DeepCopy();
        vitals.Validate();
        static JsonElement? Update(JsonElement? checkpoint, NativeCharacterVitals current)
        {
            if (checkpoint is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty("CharacterVitals", out _))
            {
                return checkpoint;
            }
            JsonObject json = JsonNode.Parse(element.GetRawText())!.AsObject();
            json["CharacterVitals"] = JsonSerializer.SerializeToNode(current);
            return JsonSerializer.SerializeToElement(json);
        }
        GameState prepared = State with
        {
            CharacterVitals = vitals,
            EventState = Update(State.EventState, vitals),
            DepartingFieldState = Update(State.DepartingFieldState, vitals),
            SavedBattleFlags = SynchronizeSavedBattleFlags(vitals)
        };
        return new(prepared);
    }

    /// <summary>Publishes a prepared owner-thread transaction synchronously, without intervening session mutations.</summary>
    public void CommitPreparedGuardianForceAbilities(GuardianForceAbilityPreparedApply prepared)
    {
        State = prepared.State;
    }

    public void ApplyGuardianForceAbilities(GuardianForceAbilityApplyPreview preview, string sourceRevision = "", long modGeneration = 0)
    {
        CommitPreparedGuardianForceAbilities(PrepareGuardianForceAbilities(preview, sourceRevision, modGeneration));
    }
}
