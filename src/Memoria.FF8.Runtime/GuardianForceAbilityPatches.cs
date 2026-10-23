using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

[JsonConverter(typeof(JsonStringEnumConverter<GuardianForceAbilityOperationKind>))]
public enum GuardianForceAbilityOperationKind
{
    EnsureAbility,
    ReplaceAbility,
    SetInitialState,
    OrderSubset
}

public sealed record GuardianForceAbilityProducerReference(string DocumentId, string OperationId, int Ability);

public sealed record GuardianForceAbilityPatchOperation
{
    public required string Id
    {
        get; init;
    }
    public required int GuardianForce
    {
        get; init;
    }
    public required GuardianForceAbilityOperationKind Kind
    {
        get; init;
    }
    public int Ability
    {
        get; init;
    }
    public int? Replacement
    {
        get; init;
    }
    public bool InitialLearned
    {
        get; init;
    }
    public bool? ExpectedInitialLearned
    {
        get; init;
    }
    public ImmutableArray<int> Order { get; init; } = [];
    public ImmutableArray<int> ExpectedOrder { get; init; } = [];
    public ImmutableArray<GuardianForceAbilityProducerReference> Producers { get; init; } = [];
    public string? ProducerOperationId
    {
        get; init;
    }
    public string? ProducerDocumentId
    {
        get; init;
    }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Metadata
    {
        get; init;
    }
}

public sealed record GuardianForceAbilityPatchDocument
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    public required string DocumentId
    {
        get; init;
    }
    public string? SourceFingerprint
    {
        get; init;
    }
    public ImmutableArray<GuardianForceAbilityPatchOperation> Operations { get; init; } = [];
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Metadata
    {
        get; init;
    }

    public void Validate()
    {
        if (SchemaVersion != 1 || string.IsNullOrWhiteSpace(DocumentId) || DocumentId.Length > 200 || Operations.IsDefault || Operations.Length > 4096)
        {
            throw new InvalidDataException("Unsupported GF document schema, identity or operation count.");
        }
        HashSet<string> identities = new(StringComparer.Ordinal);
        foreach (GuardianForceAbilityPatchOperation operation in Operations)
        {
            if (operation is null || string.IsNullOrWhiteSpace(operation.Id) || !identities.Add(operation.Id) || operation.GuardianForce is < 0 or >= 16 || !Enum.IsDefined(operation.Kind) || operation.Kind != GuardianForceAbilityOperationKind.OrderSubset && operation.Ability is <= 0 or >= 116 || operation.Replacement is <= 0 or >= 116 || operation.Order.IsDefault || operation.ExpectedOrder.IsDefault || operation.Producers.IsDefault || operation.Producers.Any(producer => producer is null || string.IsNullOrWhiteSpace(producer.DocumentId) || string.IsNullOrWhiteSpace(producer.OperationId) || producer.Ability is <= 0 or >= 116) || operation.Order.Any(id => id is <= 0 or >= 116) || operation.Order.Distinct().Count() != operation.Order.Length)
            {
                throw new InvalidDataException("GF operations require unique stable identities, supported GF/ability IDs and a distinct ordering subset.");
            }
            if (operation.Kind == GuardianForceAbilityOperationKind.ReplaceAbility && (operation.Replacement is null || operation.Replacement == operation.Ability) || operation.Kind == GuardianForceAbilityOperationKind.OrderSubset && (operation.Order.Length < 2 || operation.ExpectedOrder.Length > 0 && !operation.ExpectedOrder.Order().SequenceEqual(operation.Order.Order())))
            {
                throw new InvalidDataException("A replacement needs a different destination; an ordering operation needs a permutation of its expected subset.");
            }
        }
    }
}

public sealed record GuardianForceAbilityPatchLayer(string PackageId, GuardianForceAbilityPatchDocument Document);
public sealed record GuardianForceAbilityOperationOutcome(string PackageId, string DocumentId, string OperationId, int GuardianForce, int Ability, string Status, string Message);
public sealed record GuardianForceAbilityOrigin(int GuardianForce, int Ability, string PackageId, string DocumentId, string OperationId, string Aspect);
public sealed record GuardianForceAbilityComposition(ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> Templates, ImmutableArray<GuardianForceAbilityOperationOutcome> Outcomes, ImmutableArray<GuardianForceAbilityOrigin> Origins);

/// <summary>Reconstructs templates from source and ordered independent contributions. Never mutates source or progression.</summary>
public static class GuardianForceAbilityComposer
{
    public static GuardianForceAbilityComposition Compose(ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> source, IEnumerable<GuardianForceAbilityPatchLayer> layers, ImmutableArray<byte> abilityPoints = default)
    {
        if (source.IsDefault || source.Length != 16 || source.Any(row => row.IsDefault || row.Select(entry => entry.Ability).Distinct().Count() != row.Length))
        {
            throw new InvalidDataException("Composition requires sixteen unambiguous source templates.");
        }
        int Points(int ability) => !abilityPoints.IsDefault && ability < abilityPoints.Length ? abilityPoints[ability] : source.SelectMany(row => row).FirstOrDefault(entry => entry.Ability == ability)?.RequiredPoints ?? 0;
        List<GuardianForceAbilityTemplate>[] rows = source.Select(row => row.ToList()).ToArray();
        List<GuardianForceAbilityOperationOutcome> outcomes = [];
        List<GuardianForceAbilityOrigin> origins = [];
        HashSet<string> documents = new(StringComparer.Ordinal);
        Dictionary<(string Document, string Operation), (int Gf, int Ability)> producers = [];
        Dictionary<(int Gf, int Ability), (string Document, string Operation)> creators = [];
        foreach (GuardianForceAbilityPatchLayer layer in layers)
        {
            layer.Document.Validate();
            if (!documents.Add(layer.Document.DocumentId))
            {
                throw new InvalidDataException("Duplicate GF document identity: " + layer.Document.DocumentId);
            }
            foreach (GuardianForceAbilityPatchOperation operation in layer.Document.Operations)
            {
                List<GuardianForceAbilityTemplate> row = rows[operation.GuardianForce];
                string status = "Applied";
                string message = "Applied authored contribution.";
                int index = row.FindIndex(entry => entry.Ability == operation.Ability);
                if (operation.Producers.Any(reference => !producers.TryGetValue((reference.DocumentId, reference.OperationId), out var produced) || produced.Gf != operation.GuardianForce || produced.Ability != reference.Ability || !creators.TryGetValue((operation.GuardianForce, reference.Ability), out var creator) || creator != (reference.DocumentId, reference.OperationId)) || operation.ProducerOperationId is string dependency && (!producers.TryGetValue((operation.ProducerDocumentId ?? layer.Document.DocumentId, dependency), out var producer) || producer.Gf != operation.GuardianForce || !creators.TryGetValue((operation.GuardianForce, producer.Ability), out var creator) || creator != (operation.ProducerDocumentId ?? layer.Document.DocumentId, dependency) || operation.Kind != GuardianForceAbilityOperationKind.OrderSubset && producer.Ability != operation.Ability))
                {
                    status = "MissingProducer";
                    message = "The producing operation did not create this row.";
                }
                else if (operation.Kind == GuardianForceAbilityOperationKind.EnsureAbility)
                {
                    if (index >= 0)
                    {
                        status = row[index].InitialLearned == operation.InitialLearned ? "AlreadySatisfied" : "ConflictingValue";
                        message = "The existing row retains its creator and starting state.";
                    }
                    else
                    {
                        row.Add(new(operation.Ability, operation.InitialLearned, Points(operation.Ability), LearningPriority: row.Count == 0 ? 0 : row.Max(entry => entry.LearningPriority) + 1));
                        producers[(layer.Document.DocumentId, operation.Id)] = (operation.GuardianForce, operation.Ability);
                        creators[(operation.GuardianForce, operation.Ability)] = (layer.Document.DocumentId, operation.Id);
                    }
                }
                else if (operation.Kind == GuardianForceAbilityOperationKind.OrderSubset)
                {
                    int[] positions = row.Select((entry, position) => (entry, position)).Where(pair => operation.Order.Contains(pair.entry.Ability)).Select(pair => pair.position).ToArray();
                    if (positions.Length != operation.Order.Length)
                    {
                        status = "MissingTarget";
                        message = "An ordering participant is absent; the entire permutation was skipped.";
                    }
                    else if (positions.Select(position => row[position].Ability).SequenceEqual(operation.Order))
                    {
                        status = "AlreadySatisfied";
                    }
                    else
                    {
                        Dictionary<int, GuardianForceAbilityTemplate> subset = positions.ToDictionary(position => row[position].Ability, position => row[position]);
                        for (int i = 0; i < positions.Length; i++)
                        {
                            row[positions[i]] = subset[operation.Order[i]];
                        }
                        message = "Applied sequential ordering; earlier overlapping order may be overridden.";
                    }
                }
                else if (index < 0)
                {
                    status = "MissingTarget";
                    message = "The expected ability is absent; no other row was changed.";
                }
                else if (!row[index].SupportedCondition)
                {
                    status = "UnsupportedCondition";
                    message = "This source row has an unsupported preserved native condition and is read-only.";
                }
                else if (operation.Kind == GuardianForceAbilityOperationKind.SetInitialState)
                {
                    if (row[index].InitialLearned == operation.InitialLearned)
                    {
                        status = "AlreadySatisfied";
                    }
                    else if (operation.ExpectedInitialLearned is bool expected && row[index].InitialLearned != expected)
                    {
                        status = "ConflictingValue";
                        message = "The inherited starting state differs from the authored guard.";
                    }
                    else
                    {
                        row[index] = row[index] with
                        {
                            InitialLearned = operation.InitialLearned
                        };
                    }
                }
                else if (operation.Kind == GuardianForceAbilityOperationKind.ReplaceAbility)
                {
                    int replacement = operation.Replacement!.Value;
                    if (row.Any(entry => entry.Ability == replacement))
                    {
                        status = "ConflictingValue";
                        message = "The replacement already exists; neither row was removed.";
                    }
                    else
                    {
                        List<GuardianForceAbilityTemplate> candidate = row.Select(entry => entry with { Ability = entry.Ability == operation.Ability ? replacement : entry.Ability, RequiredPoints = entry.Ability == operation.Ability ? Points(replacement) : entry.RequiredPoints, PrerequisiteAbility = entry.PrerequisiteAbility == operation.Ability ? replacement : entry.PrerequisiteAbility, ExcludedAbility = entry.ExcludedAbility == operation.Ability ? replacement : entry.ExcludedAbility }).ToList();
                        if (HasCycle(candidate))
                        {
                            status = "UnsupportedCondition";
                            message = "Retargeted conditions would introduce a dependency cycle.";
                        }
                        else
                        {
                            foreach (GuardianForceAbilityTemplate dependent in row.Where(entry => entry.Ability != operation.Ability && (entry.PrerequisiteAbility == operation.Ability || entry.ExcludedAbility == operation.Ability)))
                            {
                                origins.Add(new(operation.GuardianForce, dependent.Ability, layer.PackageId, layer.Document.DocumentId, operation.Id, "RetargetConditions"));
                            }
                            rows[operation.GuardianForce] = candidate;
                            producers[(layer.Document.DocumentId, operation.Id)] = (operation.GuardianForce, replacement);
                            creators.Remove((operation.GuardianForce, operation.Ability));
                            creators[(operation.GuardianForce, replacement)] = (layer.Document.DocumentId, operation.Id);
                        }
                    }
                }
                outcomes.Add(new(layer.PackageId, layer.Document.DocumentId, operation.Id, operation.GuardianForce, operation.Ability, status, message));
                if (status == "Applied")
                {
                    IEnumerable<int> affected = operation.Kind == GuardianForceAbilityOperationKind.OrderSubset ? operation.Order : [operation.Replacement ?? operation.Ability];
                    foreach (int ability in affected)
                    {
                        origins.Add(new(operation.GuardianForce, ability, layer.PackageId, layer.Document.DocumentId, operation.Id, operation.Kind.ToString()));
                    }
                }
            }
        }
        return new(rows.Select(row => row.ToImmutableArray()).ToImmutableArray(), outcomes.ToImmutableArray(), origins.ToImmutableArray());
    }

    private static bool HasCycle(List<GuardianForceAbilityTemplate> entries)
    {
        Dictionary<int, int?> dependencies = entries.ToDictionary(entry => entry.Ability, entry => entry.PrerequisiteAbility);
        foreach (int ability in dependencies.Keys)
        {
            HashSet<int> seen = [];
            int? current = ability;
            while (current is int id && dependencies.TryGetValue(id, out int? next))
            {
                if (!seen.Add(id))
                {
                    return true;
                }
                current = next;
            }
        }
        return false;
    }
}
