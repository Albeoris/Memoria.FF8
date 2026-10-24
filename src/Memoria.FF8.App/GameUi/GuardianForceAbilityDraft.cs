using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>Owns one authoring layer. Session commands and stable logical row handles are never exported.</summary>
internal sealed class GuardianForceAbilityDraft
{
    private sealed record Command(GuardianForceAbilityPatchOperation? Operation, ImmutableArray<string> Rows, ImmutableHashSet<string>? Suppressed = null);
    private ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> input;
    private readonly ImmutableArray<byte> costs;
    private readonly List<Command> commands = [];
    private GuardianForceAbilityPatchDocument checkpoint;
    private GuardianForceAbilityPatchDocument? cachedDocument;
    private bool? cachedDirty;
    private GuardianForceAbilityComposition? cachedPreview;
    private Dictionary<(int Gf, int Ability), string>? cachedHandles;
    private ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> checkpointTemplates;

    public GuardianForceAbilityDraft(ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> input, ImmutableArray<byte> costs, GuardianForceAbilityPatchDocument document)
    {
        this.input = input;
        this.costs = costs;
        checkpoint = document;
        checkpointTemplates = Compose(document).Templates;
    }

    public GuardianForceAbilityPatchDocument Document => cachedDocument ??= Canonicalize(RawDocument());
    public GuardianForceAbilityPatchDocument ExportDocument => Document;
    public GuardianForceAbilityComposition Preview => cachedPreview ??= Compose(Document);
    public ImmutableArray<GuardianForceAbilityReset> EditedStartingStates
    {
        get
        {
            HashSet<string> edited = commands.Where(command => command.Operation?.Kind == GuardianForceAbilityOperationKind.SetInitialState).SelectMany(command => command.Rows).ToHashSet();
            var baselineHandles = ResolveHandles(checkpoint);
            var currentHandles = CurrentHandles();
            Dictionary<(int Gf, string Handle), bool> initial = baselineHandles.ToDictionary(pair => (pair.Key.Gf, pair.Value), pair => checkpointTemplates[pair.Key.Gf].Single(entry => entry.Ability == pair.Key.Ability).InitialLearned);
            List<GuardianForceAbilityReset> resets = [];
            for (int gf = 0; gf < Preview.Templates.Length; gf++)
            {
                foreach (var entry in Preview.Templates[gf])
                {
                    string handle = currentHandles[(gf, entry.Ability)];
                    if (edited.Contains(handle) && initial.GetValueOrDefault((gf, handle), false) != entry.InitialLearned)
                    {
                        resets.Add(new(gf, entry.Ability, entry.InitialLearned));
                    }
                }
            }
            return resets.ToImmutableArray();
        }
    }
    public bool Dirty => cachedDirty ??= JsonSerializer.Serialize(Document) != JsonSerializer.Serialize(checkpoint);
    public string Handle(int gf, int ability) => CurrentHandles().GetValueOrDefault((gf, ability), $"source-{gf}-{ability}");

    public bool RowDirty(int gf, int ability)
    {
        ImmutableArray<GuardianForceAbilityTemplate> before = checkpointTemplates[gf];
        ImmutableArray<GuardianForceAbilityTemplate> after = Preview.Templates[gf];
        int oldIndex = before.ToList().FindIndex(entry => entry.Ability == ability);
        int newIndex = after.ToList().FindIndex(entry => entry.Ability == ability);
        return oldIndex != newIndex || oldIndex < 0 || newIndex < 0 || before[oldIndex] != after[newIndex];
    }

    public void Append(int gf, int ability)
    {
        if (Preview.Templates[gf].Any(entry => entry.Ability == ability))
        {
            throw new InvalidOperationException("This GF already has that ability.");
        }
        string id = Guid.NewGuid().ToString("N");
        Add(new()
        {
            Id = id,
            GuardianForce = gf,
            Kind = GuardianForceAbilityOperationKind.EnsureAbility,
            Ability = ability
        }, [id]);
    }

    public void Replace(int gf, int target, int replacement)
    {
        if (Preview.Templates[gf].Any(entry => entry.Ability == replacement))
        {
            throw new InvalidOperationException("The replacement is already present. Neither entry was changed.");
        }
        Add(Dependent(new()
        {
            Id = Guid.NewGuid().ToString("N"),
            GuardianForce = gf,
            Kind = GuardianForceAbilityOperationKind.ReplaceAbility,
            Ability = target,
            Replacement = replacement
        }, gf, [target]), [Handle(gf, target)]);
    }

    public void Toggle(int gf, int ability)
    {
        GuardianForceAbilityTemplate entry = Preview.Templates[gf].Single(entry => entry.Ability == ability);
        Add(Dependent(new()
        {
            Id = Guid.NewGuid().ToString("N"),
            GuardianForce = gf,
            Kind = GuardianForceAbilityOperationKind.SetInitialState,
            Ability = ability,
            InitialLearned = !entry.InitialLearned,
            ExpectedInitialLearned = entry.InitialLearned
        }, gf, [ability]), [Handle(gf, ability)]);
    }

    public void Swap(int gf, int first, int second)
    {
        if (first == second)
        {
            throw new InvalidOperationException("Select two different entries.");
        }
        int[] existing = Preview.Templates[gf].Where(entry => entry.Ability == first || entry.Ability == second).Select(entry => entry.Ability).ToArray();
        if (existing.Length != 2)
        {
            throw new InvalidOperationException("Both swap participants must exist.");
        }
        Add(Dependent(new()
        {
            Id = Guid.NewGuid().ToString("N"),
            GuardianForce = gf,
            Kind = GuardianForceAbilityOperationKind.OrderSubset,
            Order = existing.Reverse().ToImmutableArray(),
            ExpectedOrder = existing.ToImmutableArray()
        }, gf, existing), [Handle(gf, first), Handle(gf, second)]);
    }

    private List<Command> UndoGroup(int gf, int ability)
    {
        string handle = Handle(gf, ability);
        int index = commands.FindLastIndex(command => command.Rows.Contains(handle));
        if (index < 0)
        {
            return [];
        }
        List<Command> group = [commands[index]];
        HashSet<string> removed = [];
        if (commands[index].Operation is { } operation)
        {
            removed.Add(operation.Id);
        }
        foreach (Command command in commands.Skip(index + 1))
        {
            if (command.Operation is { } dependent && Dependencies(dependent).Any(removed.Contains))
            {
                removed.Add(dependent.Id);
                group.Add(command);
            }
        }
        return group;
    }

    public bool UndoRequiresReview(int gf, int ability) => UndoGroup(gf, ability).Count > 1;

    public string UndoDescription(int gf, int ability, Func<int, string> name)
    {
        Command? command = UndoGroup(gf, ability).FirstOrDefault();
        return command?.Operation is not { } operation ? "Undo" : operation.Kind switch
        {
            GuardianForceAbilityOperationKind.OrderSubset => "Undo swap with " + name(operation.Order.FirstOrDefault(id => id != ability)),
            GuardianForceAbilityOperationKind.EnsureAbility => "Undo add",
            GuardianForceAbilityOperationKind.ReplaceAbility => "Undo replace",
            _ => "Undo starting state"
        };
    }

    public IReadOnlyList<string> UndoReview(int gf, int ability, Func<int, string> name) => UndoGroup(gf, ability).Select(command => command.Operation is { } operation ? operation.Kind + ": " + string.Join(", ", operation.Kind == GuardianForceAbilityOperationKind.OrderSubset ? operation.Order.Select(name) : new[] { name(operation.Ability), name(operation.Replacement ?? operation.Ability) }.Distinct()) : "Restore inherited contribution").ToArray();

    public void Undo(int gf, int ability)
    {
        string handle = Handle(gf, ability);
        int index = commands.FindLastIndex(command => command.Rows.Contains(handle));
        if (index < 0)
        {
            return;
        }
        HashSet<string> removed = [];
        if (commands[index].Operation is { } operation)
        {
            removed.Add(operation.Id);
        }
        commands.RemoveAt(index);
        for (int next = index; next < commands.Count;)
        {
            Command command = commands[next];
            if (command.Operation is { } dependent && Dependencies(dependent).Any(removed.Contains))
            {
                removed.Add(dependent.Id);
                commands.RemoveAt(next);
            }
            else
            {
                next++;
            }
        }
        Invalidate();
    }

    public IReadOnlyList<string> RestoreInheritedReview(int gf, int ability, Func<int, string> name)
    {
        GuardianForceAbilityDraft proposed = new(input, costs, RawDocument());
        proposed.RestoreInherited(gf, ability);
        var before = Preview.Templates[gf];
        var after = proposed.Preview.Templates[gf];
        List<string> result = ["Restore inherited contribution for " + name(ability) + ". Current learned/AP progress is preserved."];
        foreach (int id in before.Select(entry => entry.Ability).Union(after.Select(entry => entry.Ability)))
        {
            int oldIndex = before.ToList().FindIndex(entry => entry.Ability == id);
            int newIndex = after.ToList().FindIndex(entry => entry.Ability == id);
            if (oldIndex != newIndex || oldIndex < 0 || newIndex < 0 || before[oldIndex] != after[newIndex])
            {
                string describe(ImmutableArray<GuardianForceAbilityTemplate> entries, int index) => index < 0 ? "absent" : "position " + (index + 1) + ", Start learned " + entries[index].InitialLearned + ", prerequisite " + (entries[index].PrerequisiteAbility is int required ? name(required) : "none");
                result.Add(name(id) + ": " + describe(before, oldIndex) + " → " + describe(after, newIndex));
            }
        }
        if (result.Count == 1)
        {
            result.Add("No inherited fields differ from the selected contribution.");
        }
        return result;
    }

    public void RestoreInherited(int gf, int ability)
    {
        GuardianForceAbilityPatchDocument current = RawDocument();
        string handle = Handle(gf, ability);
        HashSet<string> removed = current.Operations.Where(operation => operation.GuardianForce == gf && (operation.Kind == GuardianForceAbilityOperationKind.OrderSubset ? operation.Order.Contains(ability) : operation.Ability == ability || operation.Replacement == ability)).Select(operation => operation.Id).ToHashSet();
        bool changed;
        do
        {
            changed = false;
            foreach (GuardianForceAbilityPatchOperation operation in current.Operations)
            {
                if (Dependencies(operation).Any(removed.Contains))
                {
                    changed |= removed.Add(operation.Id);
                }
            }
        } while (changed);
        ImmutableArray<string> affected = current.Operations.Where(operation => removed.Contains(operation.Id)).SelectMany(operation => operation.Kind == GuardianForceAbilityOperationKind.OrderSubset ? operation.Order.Select(id => Handle(gf, id)) : [Handle(gf, operation.Replacement ?? operation.Ability)]).Append(handle).Distinct().ToImmutableArray();
        commands.Add(new(null, affected, removed.ToImmutableHashSet()));
        Invalidate();
    }

    public void RebaseInput(ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> value)
    {
        input = value;
        checkpointTemplates = Compose(checkpoint).Templates;
        Invalidate();
    }

    public void DiscardChanges()
    {
        commands.Clear();
        Invalidate();
    }

    public void Accept(GuardianForceAbilityPatchDocument saved)
    {
        saved.Validate();
        checkpoint = saved;
        checkpointTemplates = Compose(saved).Templates;
        commands.Clear();
        Invalidate();
    }

    private GuardianForceAbilityPatchDocument RawDocument()
    {
        List<GuardianForceAbilityPatchOperation> operations = checkpoint.Operations.ToList();
        foreach (Command command in commands)
        {
            if (command.Suppressed is { } suppressed)
            {
                operations.RemoveAll(operation => suppressed.Contains(operation.Id));
            }
            else if (command.Operation is { } operation)
            {
                operations.Add(operation);
            }
        }
        return checkpoint with
        {
            Operations = operations.ToImmutableArray()
        };
    }

    private void Add(GuardianForceAbilityPatchOperation operation, ImmutableArray<string> rows)
    {
        GuardianForceAbilityComposition preview = Compose(RawDocument() with
        {
            Operations = RawDocument().Operations.Add(operation)
        });
        GuardianForceAbilityOperationOutcome outcome = preview.Outcomes[^1];
        if (outcome.Status is not ("Applied" or "AlreadySatisfied"))
        {
            throw new InvalidOperationException(outcome.Message);
        }
        int gf = operation.GuardianForce;
        var before = Preview.Templates[gf];
        var after = preview.Templates[gf];
        ImmutableArray<string> affected = rows.Concat(before.Where(entry => after.FirstOrDefault(candidate => candidate.Ability == entry.Ability) is { } changed && changed != entry).Select(entry => Handle(gf, entry.Ability))).Distinct().ToImmutableArray();
        commands.Add(new(operation, affected));
        Invalidate();
    }

    private GuardianForceAbilityPatchOperation Dependent(GuardianForceAbilityPatchOperation operation, int gf, IEnumerable<int> abilities)
    {
        GuardianForceAbilityPatchDocument raw = RawDocument();
        var composition = Compose(raw);
        HashSet<string> applied = composition.Outcomes.Where(outcome => outcome.Status == "Applied").Select(outcome => outcome.OperationId).ToHashSet();
        List<GuardianForceAbilityProducerReference> references = [];
        foreach (int ability in abilities)
        {
            GuardianForceAbilityPatchOperation? producer = raw.Operations.LastOrDefault(candidate => applied.Contains(candidate.Id) && candidate.GuardianForce == gf && (candidate.Kind == GuardianForceAbilityOperationKind.EnsureAbility && candidate.Ability == ability || candidate.Kind == GuardianForceAbilityOperationKind.ReplaceAbility && candidate.Replacement == ability));
            if (producer is not null)
            {
                references.Add(new(raw.DocumentId, producer.Id, ability));
            }
        }
        return operation with
        {
            Producers = references.ToImmutableArray()
        };
    }

    private Dictionary<(int Gf, int Ability), string> CurrentHandles() => cachedHandles ??= ResolveHandles(RawDocument());

    private Dictionary<(int Gf, int Ability), string> ResolveHandles(GuardianForceAbilityPatchDocument raw)
    {
        Dictionary<(int Gf, int Ability), string> handles = [];
        for (int gf = 0; gf < input.Length; gf++)
        {
            foreach (var entry in input[gf])
            {
                handles[(gf, entry.Ability)] = $"source-{gf}-{entry.Ability}";
            }
        }
        HashSet<string> applied = Compose(raw).Outcomes.Where(outcome => outcome.Status == "Applied").Select(outcome => outcome.OperationId).ToHashSet();
        foreach (var operation in raw.Operations.Where(operation => applied.Contains(operation.Id)))
        {
            if (operation.Kind == GuardianForceAbilityOperationKind.EnsureAbility)
            {
                handles[(operation.GuardianForce, operation.Ability)] = operation.Id;
            }
            else if (operation.Kind == GuardianForceAbilityOperationKind.ReplaceAbility && handles.Remove((operation.GuardianForce, operation.Ability), out string? handle))
            {
                handles[(operation.GuardianForce, operation.Replacement!.Value)] = handle;
            }
        }
        return handles;
    }

    private void Invalidate()
    {
        cachedDocument = null;
        cachedDirty = null;
        cachedPreview = null;
        cachedHandles = null;
    }

    private IEnumerable<string> Dependencies(GuardianForceAbilityPatchOperation operation) => operation.Producers.Where(reference => reference.DocumentId == checkpoint.DocumentId).Select(reference => reference.OperationId).Concat(operation.ProducerOperationId is { } id && (operation.ProducerDocumentId is null || operation.ProducerDocumentId == checkpoint.DocumentId) ? [id] : []);

    private GuardianForceAbilityPatchDocument Canonicalize(GuardianForceAbilityPatchDocument raw)
    {
        List<GuardianForceAbilityPatchOperation> operations = raw.Operations.ToList();
        HashSet<string> session = commands.Where(command => command.Operation is not null).Select(command => command.Operation!.Id).ToHashSet();
        // Saved guarded contributions are preserved. Only this session's valid transformations are reduced.
        for (int index = 0; index < operations.Count; index++)
        {
            GuardianForceAbilityPatchOperation operation = operations[index];
            if (!session.Contains(operation.Id))
            {
                continue;
            }
            if (operation.Kind == GuardianForceAbilityOperationKind.SetInitialState)
            {
                int prior = index == 0 ? -1 : operations.FindLastIndex(index - 1, index, candidate => candidate.GuardianForce == operation.GuardianForce && (candidate.Kind == GuardianForceAbilityOperationKind.EnsureAbility && candidate.Ability == operation.Ability || candidate.Kind == GuardianForceAbilityOperationKind.SetInitialState && candidate.Ability == operation.Ability || candidate.Kind == GuardianForceAbilityOperationKind.ReplaceAbility && (candidate.Ability == operation.Ability || candidate.Replacement == operation.Ability)));
                if (prior >= 0 && operations[prior].Kind is GuardianForceAbilityOperationKind.EnsureAbility or GuardianForceAbilityOperationKind.SetInitialState)
                {
                    var previous = operations[prior];
                    operations[prior] = previous with
                    {
                        InitialLearned = operation.InitialLearned
                    };
                    operations.RemoveAt(index--);
                    if (previous.Kind == GuardianForceAbilityOperationKind.SetInitialState && previous.ExpectedInitialLearned == operation.InitialLearned)
                    {
                        operations.RemoveAt(prior);
                        index--;
                    }
                }
                else if (operation.ExpectedInitialLearned == operation.InitialLearned)
                {
                    operations.RemoveAt(index--);
                }
            }
            else if (operation.Kind == GuardianForceAbilityOperationKind.ReplaceAbility)
            {
                int prior = index == 0 ? -1 : operations.FindLastIndex(index - 1, index, candidate => candidate.GuardianForce == operation.GuardianForce && (candidate.Kind == GuardianForceAbilityOperationKind.OrderSubset ? candidate.Order.Contains(operation.Ability) : candidate.Ability == operation.Ability || candidate.Replacement == operation.Ability));
                if (prior >= 0 && (operations[prior].Kind == GuardianForceAbilityOperationKind.EnsureAbility && operations[prior].Ability == operation.Ability || operations[prior].Kind == GuardianForceAbilityOperationKind.ReplaceAbility && operations[prior].Replacement == operation.Ability))
                {
                    var previous = operations[prior];
                    bool returnsToInput = previous.Kind == GuardianForceAbilityOperationKind.ReplaceAbility && previous.Ability == operation.Replacement;
                    operations[prior] = previous.Kind == GuardianForceAbilityOperationKind.EnsureAbility ? previous with
                    {
                        Ability = operation.Replacement!.Value
                    } : previous with
                    {
                        Replacement = operation.Replacement
                    };
                    operations.RemoveAt(index--);
                    for (int next = index + 1; next < operations.Count; next++)
                    {
                        var later = operations[next];
                        operations[next] = later with
                        {
                            ProducerOperationId = later.ProducerOperationId == operation.Id ? returnsToInput ? null : previous.Id : later.ProducerOperationId,
                            Producers = later.Producers.Where(reference => !returnsToInput || reference.OperationId != operation.Id && reference.OperationId != previous.Id || reference.DocumentId != raw.DocumentId).Select(reference => reference.DocumentId == raw.DocumentId && reference.OperationId == operation.Id ? reference with { OperationId = previous.Id } : reference).ToImmutableArray()
                        };
                    }
                    if (returnsToInput)
                    {
                        operations.RemoveAt(prior);
                        index--;
                    }
                }
            }
            else if (operation.Kind == GuardianForceAbilityOperationKind.OrderSubset)
            {
                int prior = index == 0 ? -1 : operations.FindLastIndex(index - 1, index, candidate => candidate.GuardianForce == operation.GuardianForce && (candidate.Kind == GuardianForceAbilityOperationKind.OrderSubset ? candidate.Order.Intersect(operation.Order).Any() : operation.Order.Contains(candidate.Ability) || candidate.Replacement is int replacement && operation.Order.Contains(replacement)));
                if (prior >= 0 && operations[prior].Kind == GuardianForceAbilityOperationKind.OrderSubset && operations[prior].Order.Order().SequenceEqual(operation.Order.Order()))
                {
                    var previous = operations[prior];
                    operations[prior] = previous with
                    {
                        Order = operation.Order
                    };
                    operations.RemoveAt(index--);
                    if (previous.ExpectedOrder.SequenceEqual(operation.Order))
                    {
                        operations.RemoveAt(prior);
                        index--;
                    }
                }
            }
        }
        GuardianForceAbilityPatchDocument candidate = raw with
        {
            Operations = operations.ToImmutableArray()
        };
        // A reduction may not turn a guarded failed operation into a successful edit.
        return Equal(Compose(candidate).Templates, Compose(raw).Templates) ? candidate : raw;
    }

    private GuardianForceAbilityComposition Compose(GuardianForceAbilityPatchDocument document) => GuardianForceAbilityComposer.Compose(input, [new("Selected layer", document)], costs);
    private static bool Equal(ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> left, ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> right) => left.Zip(right).All(pair => pair.First.SequenceEqual(pair.Second));
}
