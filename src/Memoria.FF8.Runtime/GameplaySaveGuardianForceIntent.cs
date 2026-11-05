using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Save-local GF editing choices without compiled templates, source fingerprints or extension metadata.</summary>
public sealed record GameplaySaveGuardianForceIntent([property: JsonRequired] int Version, [property: JsonRequired] ImmutableArray<GameplaySaveGuardianForceDocument> LocalDocuments, [property: JsonRequired] ImmutableArray<string> ExcludedDocuments, [property: JsonRequired] ImmutableDictionary<string, string> LocalOwnershipAliases)
{
    internal static GameplaySaveGuardianForceIntent? Capture(NativeCharacterVitals source)
    {
        GuardianForceAbilityProfile? profile = source.GuardianForceProfile;
        if (profile is null || profile.LocalDocuments.IsEmpty && profile.ExcludedDocuments.IsEmpty && profile.LocalOwnershipAliases.IsEmpty)
        {
            return null;
        }
        GameplaySaveGuardianForceIntent result = new(1, profile.LocalDocuments.Select(GameplaySaveGuardianForceDocument.Capture).ToImmutableArray(), profile.ExcludedDocuments, profile.LocalOwnershipAliases);
        result.Validate();
        GuardianForceAbilityComposition composition = result.Compose(source).Composition;
        if (!source.ResolveGuardianForceTemplates().SelectMany((row, id) => row.Select(entry => (id, entry))).SequenceEqual(composition.Templates.SelectMany((row, id) => row.Select(entry => (id, entry)))))
        {
            throw new NotSupportedException("Gameplay GF authoring intent is not reconciled with the current learning definitions; preserve the execution snapshot.");
        }
        return result;
    }

    internal NativeCharacterVitals RestoreDefinitions(NativeCharacterVitals installed)
    {
        Validate();
        (GuardianForceAbilityProfile profile, GuardianForceAbilityComposition composition) = Compose(installed);
        NativeCharacterVitals result = installed with { GuardianForceProfile = profile, MenuDefinitions = installed.MenuDefinitions with { Templates = composition.Templates } };
        result.Validate();
        return result;
    }

    private (GuardianForceAbilityProfile Profile, GuardianForceAbilityComposition Composition) Compose(NativeCharacterVitals installed)
    {
        GuardianForceAbilityProfile? selected = installed.GuardianForceProfile;
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> source = selected?.Source ?? (installed.MenuDefinitions.SourceTemplates.IsDefault ? installed.ResolveGuardianForceTemplates() : installed.MenuDefinitions.SourceTemplates);
        ImmutableArray<GuardianForceAbilityPatchLayer> layers = selected?.AppliedLayers ?? [];
        ImmutableArray<GuardianForceAbilityPatchDocument> locals = LocalDocuments.Select(document => document.Restore()).ToImmutableArray();
        HashSet<string> localIds = locals.Select(document => document.DocumentId).ToHashSet(StringComparer.Ordinal);
        ImmutableArray<string> exclusions = ExcludedDocuments.Concat(layers.Where(layer => localIds.Contains(layer.Document.DocumentId)).Select(layer => layer.Document.DocumentId)).Distinct(StringComparer.Ordinal).ToImmutableArray();
        GuardianForceAbilityProfile profile = new(source, selected?.SourceFingerprint ?? installed.KernelSha256, layers, locals, exclusions) { LocalOwnershipAliases = LocalOwnershipAliases };
        profile.Validate();
        GuardianForceAbilityComposition composition = GuardianForceAbilityComposer.Compose(profile.Source, layers.Where(layer => !exclusions.Contains(layer.Document.DocumentId, StringComparer.Ordinal)).Concat(locals.Select(document => new GuardianForceAbilityPatchLayer("Playthrough", document))), installed.MenuDefinitions.AbilityPoints);
        GuardianForceAbilityOperationOutcome? conflict = composition.Outcomes.FirstOrDefault(outcome => localIds.Contains(outcome.DocumentId) && outcome.Status is not ("Applied" or "AlreadySatisfied"));
        if (conflict is not null)
        {
            throw new NotSupportedException($"Gameplay GF authoring operation '{conflict.DocumentId}/{conflict.OperationId}' requires reconciliation ({conflict.Status}); preserve the execution snapshot.");
        }
        return (profile, composition);
    }

    public void Validate()
    {
        if (Version != 1 || LocalDocuments.IsDefault || ExcludedDocuments.IsDefault || LocalOwnershipAliases is null || LocalDocuments.Any(document => document is null) || LocalDocuments.Select(document => document.DocumentId).Distinct(StringComparer.Ordinal).Count() != LocalDocuments.Length || ExcludedDocuments.Distinct(StringComparer.Ordinal).Count() != ExcludedDocuments.Length)
        {
            throw new InvalidDataException("Gameplay GF authoring intent requires a supported version and distinct local/excluded document identities.");
        }
        foreach (GameplaySaveGuardianForceDocument document in LocalDocuments)
        {
            document.Validate();
        }
        foreach (string identity in ExcludedDocuments.Concat(LocalOwnershipAliases.Keys).Concat(LocalOwnershipAliases.Values))
        {
            ValidateIdentity(identity);
        }
    }

    internal static void ValidateIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            throw new InvalidDataException("Gameplay GF authoring references require stable nonempty identities.");
        }
        if (value[0] is '/' or '\\' || value.Length > 1 && value[1] == ':' || value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Gameplay GF authoring references must use stable document/operation identities instead of source paths or provenance links.");
        }
    }
}

public sealed record GameplaySaveGuardianForceDocument([property: JsonRequired] string DocumentId, [property: JsonRequired] ImmutableArray<GameplaySaveGuardianForceOperation> Operations)
{
    internal static GameplaySaveGuardianForceDocument Capture(GuardianForceAbilityPatchDocument source)
    {
        return new(source.DocumentId, source.Operations.Select(GameplaySaveGuardianForceOperation.Capture).ToImmutableArray());
    }

    internal GuardianForceAbilityPatchDocument Restore()
    {
        return new() { DocumentId = DocumentId, Operations = Operations.Select(operation => operation.Restore()).ToImmutableArray() };
    }

    public void Validate()
    {
        GameplaySaveGuardianForceIntent.ValidateIdentity(DocumentId);
        if (Operations.IsDefault || Operations.Any(operation => operation is null))
        {
            throw new InvalidDataException("Gameplay GF authoring documents require explicit semantic operations.");
        }
        foreach (GameplaySaveGuardianForceOperation operation in Operations)
        {
            operation.Validate();
        }
        Restore().Validate();
    }
}

public sealed record GameplaySaveGuardianForceOperation([property: JsonRequired] string Id, [property: JsonRequired] int GuardianForce, [property: JsonRequired] GuardianForceAbilityOperationKind Kind, [property: JsonRequired] int Ability, [property: JsonRequired] int? Replacement, [property: JsonRequired] bool InitialLearned, [property: JsonRequired] bool? ExpectedInitialLearned, [property: JsonRequired] ImmutableArray<int> Order, [property: JsonRequired] ImmutableArray<int> ExpectedOrder, [property: JsonRequired] ImmutableArray<GameplaySaveGuardianForceProducer> Producers, [property: JsonRequired] string? ProducerOperationId, [property: JsonRequired] string? ProducerDocumentId)
{
    internal static GameplaySaveGuardianForceOperation Capture(GuardianForceAbilityPatchOperation source)
    {
        return new(source.Id, source.GuardianForce, source.Kind, source.Ability, source.Replacement, source.InitialLearned, source.ExpectedInitialLearned, source.Order, source.ExpectedOrder, source.Producers.Select(reference => new GameplaySaveGuardianForceProducer(reference.DocumentId, reference.OperationId, reference.Ability)).ToImmutableArray(), source.ProducerOperationId, source.ProducerDocumentId);
    }

    internal GuardianForceAbilityPatchOperation Restore()
    {
        return new() { Id = Id, GuardianForce = GuardianForce, Kind = Kind, Ability = Ability, Replacement = Replacement, InitialLearned = InitialLearned, ExpectedInitialLearned = ExpectedInitialLearned, Order = Order, ExpectedOrder = ExpectedOrder, Producers = Producers.Select(reference => new GuardianForceAbilityProducerReference(reference.DocumentId, reference.OperationId, reference.Ability)).ToImmutableArray(), ProducerOperationId = ProducerOperationId, ProducerDocumentId = ProducerDocumentId };
    }

    public void Validate()
    {
        GameplaySaveGuardianForceIntent.ValidateIdentity(Id);
        if (Producers.IsDefault || Producers.Any(producer => producer is null))
        {
            throw new InvalidDataException("Gameplay GF authoring operations require explicit semantic producer references.");
        }
        foreach (GameplaySaveGuardianForceProducer producer in Producers)
        {
            GameplaySaveGuardianForceIntent.ValidateIdentity(producer.DocumentId);
            GameplaySaveGuardianForceIntent.ValidateIdentity(producer.OperationId);
        }
        if (ProducerOperationId is not null)
        {
            GameplaySaveGuardianForceIntent.ValidateIdentity(ProducerOperationId);
        }
        if (ProducerDocumentId is not null)
        {
            GameplaySaveGuardianForceIntent.ValidateIdentity(ProducerDocumentId);
        }
    }
}

public sealed record GameplaySaveGuardianForceProducer([property: JsonRequired] string DocumentId, [property: JsonRequired] string OperationId, [property: JsonRequired] int Ability);
