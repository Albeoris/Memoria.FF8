using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

public sealed record EncounterPatchLayer(string PackageId, string PayloadPath, EncounterPatchDocument Document);
public sealed record EncounterPatchBaseline(string Encounter, string SourceProfile, ImmutableArray<string> Actors, int ActiveActorCapacity = 4);
public sealed record EncounterPatchOrigin(string PackageId, string PayloadPath, string DocumentId, string OperationId, string Target, string Property);
public sealed record EncounterPatchOutcome(EncounterPatchOrigin Origin, string Status);
public sealed record EncounterPatchPolicy(string Id, ImmutableArray<EncounterPatchRule> Rules);

/// <summary>Names introduced by documents are keyed as documentId/localId. Baseline actors retain their semantic names.</summary>
public sealed record EncounterPatchComposition(
    string Encounter,
    string SourceProfile,
    string GenerationIdentity,
    ImmutableDictionary<string, double> ActorProperties,
    ImmutableDictionary<string, EncounterPatchOperation> ActorTemplates,
    ImmutableDictionary<string, ImmutableArray<string>> Groups,
    ImmutableDictionary<string, EncounterPatchPolicy> Policies,
    ImmutableDictionary<string, EncounterPatchOperation> Presentations,
    ImmutableDictionary<string, EncounterPatchOperation> DamageRoutes,
    ImmutableDictionary<string, EncounterPatchOperation> EventGates,
    ImmutableDictionary<string, string> NormalActionPolicies,
    ImmutableDictionary<string, EncounterPatchPlacement> Placements,
    ImmutableArray<EncounterPatchOrigin> Origins,
    ImmutableArray<EncounterPatchOutcome> Outcomes);

/// <summary>Prepares a complete immutable overlay; any invalid operation rejects the entire candidate.</summary>
public static class EncounterPatchComposer
{
    public static EncounterPatchComposition Compose(EncounterPatchBaseline baseline, IEnumerable<EncounterPatchLayer> contributions)
    {
        EncounterPatchDocument.Identity(baseline.Encounter, "baseline.encounter");
        EncounterPatchDocument.Require(!string.IsNullOrWhiteSpace(baseline.SourceProfile) && !baseline.Actors.IsDefaultOrEmpty && baseline.Actors.Distinct(StringComparer.Ordinal).Count() == baseline.Actors.Length && baseline.ActiveActorCapacity is >= 1 and <= 4 && baseline.Actors.Length <= baseline.ActiveActorCapacity, "Invalid selected encounter baseline/profile/capacity.");
        foreach (string actor in baseline.Actors)
        {
            EncounterPatchDocument.Identity(actor, "baseline.actor");
        }
        EncounterPatchLayer[] layers = contributions.ToArray();
        EncounterPatchDocument.Require(layers.Length <= 1024, "Too many encounter documents.");
        Dictionary<string, EncounterPatchLayer> documents = new(StringComparer.Ordinal);
        foreach (EncounterPatchLayer layer in layers)
        {
            try
            {
                EncounterPatchDocument.Require(!string.IsNullOrWhiteSpace(layer.PackageId) && !string.IsNullOrWhiteSpace(layer.PayloadPath), "Missing package/payload identity.");
                layer.Document.Validate();
                EncounterPatchDocument.Require(layer.Document.Encounter == baseline.Encounter, "Document encounter does not match selected baseline.");
                EncounterPatchDocument.Require(layer.Document.SourceProfile is null || layer.Document.SourceProfile == baseline.SourceProfile, $"Expected source profile '{layer.Document.SourceProfile}', observed '{baseline.SourceProfile}'. Select a compatible package/profile.");
                EncounterPatchDocument.Require(layer.Document.RequiredActorCapacity is null || layer.Document.RequiredActorCapacity <= baseline.ActiveActorCapacity, "Required actor capacity exceeds selected native profile.");
                EncounterPatchDocument.Require(documents.TryAdd(layer.Document.DocumentId, layer), "Duplicate document identity: " + layer.Document.DocumentId);
            }
            catch (InvalidDataException error)
            {
                throw Failure(layer, null, error.Message, error);
            }
        }
        EncounterPatchDocument.Require(layers.Sum(layer => layer.Document.Operations.Length) <= 4096, "Encounter generation exceeds the 4096 operation limit.");
        List<(EncounterPatchLayer Layer, EncounterPatchOperation Operation)> ordered = Order(layers);
        Dictionary<string, EncounterPatchOperation> templates = new(StringComparer.Ordinal);
        Dictionary<string, ImmutableArray<string>> groups = new(StringComparer.Ordinal);
        Dictionary<string, EncounterPatchPolicy> policies = new(StringComparer.Ordinal);
        Dictionary<string, EncounterPatchOperation> presentations = new(StringComparer.Ordinal);
        Dictionary<string, EncounterPatchOperation> routes = new(StringComparer.Ordinal);
        Dictionary<string, EncounterPatchOperation> gates = new(StringComparer.Ordinal);
        Dictionary<string, string> assignments = new(StringComparer.Ordinal);
        Dictionary<string, EncounterPatchPlacement> placements = new(StringComparer.Ordinal);
        Dictionary<string, double> properties = new(StringComparer.Ordinal);
        Dictionary<string, EncounterPatchOrigin> owners = new(StringComparer.Ordinal);
        Dictionary<string, EncounterPatchOrigin> writes = new(StringComparer.Ordinal);
        Dictionary<(string Document, string Operation), HashSet<string>> produced = [];
        List<EncounterPatchOrigin> origins = [];
        List<EncounterPatchOutcome> outcomes = [];
        foreach (EncounterPatchLayer layer in layers)
        {
            foreach (EncounterPatchPlacement placement in layer.Document.Placements)
            {
                placements.Add(Scope(layer.Document.DocumentId, placement.Id), placement);
            }
        }
        foreach ((EncounterPatchLayer layer, EncounterPatchOperation op) in ordered)
        {
            string document = layer.Document.DocumentId;
            string ScopeLocal(string value) => baseline.Actors.Contains(value) ? value : Scope(document, value);
            string targetDocument = op.Producer?.DocumentId ?? document;
            string Entity(string value) => Scope(targetDocument, value);
            string target = op.Kind switch
            {
                EncounterPatchOperationKind.AddActorTemplate => Scope(document, op.ActorTemplate!),
                EncounterPatchOperationKind.EnsureGroup => Scope(document, op.Group!),
                EncounterPatchOperationKind.AddActionPolicy => Scope(document, op.Policy!),
                EncounterPatchOperationKind.AddRule or EncounterPatchOperationKind.SetRuleProperty or EncounterPatchOperationKind.RemoveRule => Entity(op.Policy!) + "/" + (op.Rule ?? op.RuleDefinition!.Id),
                EncounterPatchOperationKind.SetActorProperty or EncounterPatchOperationKind.SetNormalActionPolicy => baseline.Actors.Contains(op.Actor!) ? op.Actor! : Entity(op.Actor!),
                EncounterPatchOperationKind.AddDamageRoute => Scope(document, op.Route!),
                EncounterPatchOperationKind.SetDamageRouteProperty or EncounterPatchOperationKind.RemoveDamageRoute => Entity(op.Route!),
                EncounterPatchOperationKind.AddPresentation => Scope(document, op.Presentation!),
                EncounterPatchOperationKind.SetPresentationProperty => Entity(op.Presentation!),
                EncounterPatchOperationKind.GateNativeEvent => ScopeLocal(op.Actor!) + "/" + op.Event,
                _ => throw Failure(layer, op, "Unsupported operation.")
            };
            EncounterPatchOrigin origin = new(layer.PackageId, layer.PayloadPath, document, op.Id, target, op.Property ?? op.Kind.ToString());
            bool changed = true;
            try
            {
                switch (op.Kind)
                {
                    case EncounterPatchOperationKind.AddActorTemplate:
                        changed = Ensure(templates, target, op);
                        break;
                    case EncounterPatchOperationKind.EnsureGroup:
                        ImmutableArray<string> members = op.Members.Select(ScopeLocal).ToImmutableArray();
                        if (groups.TryGetValue(target, out ImmutableArray<string> existingMembers))
                        {
                            ImmutableArray<string> merged = existingMembers.Union(members, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();
                            changed = !merged.SequenceEqual(existingMembers);
                            groups[target] = merged;
                        }
                        else
                        {
                            groups.Add(target, members.Order(StringComparer.Ordinal).ToImmutableArray());
                        }
                        break;
                    case EncounterPatchOperationKind.AddActionPolicy:
                        changed = Ensure(policies, target, new EncounterPatchPolicy(target, op.Rules));
                        if (changed)
                        {
                            foreach (EncounterPatchRule rule in op.Rules)
                            {
                                Own(target + "/" + rule.Id);
                            }
                        }
                        break;
                    case EncounterPatchOperationKind.AddRule:
                    case EncounterPatchOperationKind.SetRuleProperty:
                    case EncounterPatchOperationKind.RemoveRule:
                        string policyKey = Entity(op.Policy!);
                        EncounterPatchDocument.Require(policies.TryGetValue(policyKey, out EncounterPatchPolicy? policy), "Missing policy: " + policyKey);
                        List<EncounterPatchRule> rules = policy.Rules.ToList();
                        int index = rules.FindIndex(rule => rule.Id == (op.Rule ?? op.RuleDefinition!.Id));
                        if (op.Kind == EncounterPatchOperationKind.AddRule)
                        {
                            if (index >= 0)
                            {
                                EncounterPatchDocument.Require(Equal(rules[index], op.RuleDefinition), "Rule identity has incompatible meaning: " + target);
                                changed = false;
                            }
                            else
                            {
                                rules.Add(op.RuleDefinition!);
                            }
                        }
                        else
                        {
                            EncounterPatchDocument.Require(index >= 0, "Missing rule: " + target);
                            VerifyProducer(target);
                            if (op.Kind == EncounterPatchOperationKind.RemoveRule)
                            {
                                GuardValue(JsonSerializer.SerializeToElement(rules[index], EncounterPatchDocument.JsonOptions));
                                rules.RemoveAt(index);
                            }
                            else
                            {
                                rules[index] = Edit(rules[index], op.Property!, op.ExpectedValue, op.Value, target);
                            }
                        }
                        policies[policyKey] = policy with { Rules = rules.ToImmutableArray() };
                        break;
                    case EncounterPatchOperationKind.SetActorProperty:
                        if (op.Producer is not null)
                        {
                            VerifyProducer(target);
                        }
                        string propertyKey = target + "/" + op.Property;
                        double current = properties.GetValueOrDefault(propertyKey, 1.0);
                        double value = op.Value.GetDouble();
                        CheckWrite(propertyKey);
                        GuardValue(JsonSerializer.SerializeToElement(current));
                        properties[propertyKey] = value;
                        writes[propertyKey] = origin;
                        break;
                    case EncounterPatchOperationKind.SetNormalActionPolicy:
                        if (op.Producer is not null)
                        {
                            VerifyProducer(target);
                        }
                        string observed = assignments.GetValueOrDefault(target, "nativeNormalAction");
                        string requested = Scope(document, op.Policy!);
                        CheckWrite(target + "/normalActionPolicy");
                        EncounterPatchDocument.Require(observed == op.ExpectedOwner, $"Expected owner '{op.ExpectedOwner}', observed '{observed}'. Supply a guarded supported override.");
                        assignments[target] = requested;
                        writes[target + "/normalActionPolicy"] = origin;
                        break;
                    case EncounterPatchOperationKind.AddPresentation:
                        changed = Ensure(presentations, target, op);
                        break;
                    case EncounterPatchOperationKind.AddDamageRoute:
                        changed = Ensure(routes, target, op);
                        break;
                    case EncounterPatchOperationKind.SetPresentationProperty:
                        EncounterPatchDocument.Require(presentations.TryGetValue(target, out EncounterPatchOperation? presentation), "Missing presentation: " + target);
                        VerifyProducer(target);
                        presentations[target] = Edit(presentation, op.Property!, op.ExpectedValue, op.Value, target);
                        EncounterPatchDocument.ValidateOperation(presentations[target]);
                        break;
                    case EncounterPatchOperationKind.SetDamageRouteProperty:
                        EncounterPatchDocument.Require(routes.TryGetValue(target, out EncounterPatchOperation? route), "Missing damage route: " + target);
                        VerifyProducer(target);
                        routes[target] = Edit(route, op.Property!, op.ExpectedValue, op.Value, target);
                        EncounterPatchDocument.ValidateOperation(routes[target]);
                        break;
                    case EncounterPatchOperationKind.RemoveDamageRoute:
                        EncounterPatchDocument.Require(routes.TryGetValue(target, out EncounterPatchOperation? removedRoute), "Missing damage route: " + target);
                        VerifyProducer(target);
                        GuardValue(Definition(removedRoute));
                        routes.Remove(target);
                        break;
                    case EncounterPatchOperationKind.GateNativeEvent:
                        changed = Ensure(gates, target, op);
                        break;
                }
                if (changed)
                {
                    Own(target);
                }
                origins.Add(origin);
                outcomes.Add(new(origin, changed ? "Applied" : "AlreadySatisfied"));
            }
            catch (Exception error) when (error is InvalidDataException or JsonException or InvalidOperationException)
            {
                string owner = owners.TryGetValue(target, out EncounterPatchOrigin? previous) ? $" Existing owner: {previous.PackageId}/{previous.PayloadPath}, {previous.DocumentId}/{previous.OperationId}." : "";
                throw Failure(layer, op, error.Message + owner + " Candidate rejected atomically; retain the previous complete generation if available.", error);
            }

            void Own(string entity)
            {
                owners.TryAdd(entity, origin);
                if (!produced.TryGetValue((document, op.Id), out HashSet<string>? entities))
                {
                    entities = new(StringComparer.Ordinal);
                    produced.Add((document, op.Id), entities);
                }
                entities.Add(entity);
            }

            void VerifyProducer(string entity)
            {
                if (op.Producer is null)
                {
                    EncounterPatchDocument.Require(owners.TryGetValue(entity, out EncounterPatchOrigin? creator) && creator.DocumentId == document, "Cross-document edits require the actual producer reference: " + entity);
                    return;
                }
                EncounterPatchDocument.Require(produced.TryGetValue((op.Producer.DocumentId, op.Producer.OperationId), out HashSet<string>? entities) && entities.Contains(entity), $"Producer '{op.Producer.DocumentId}/{op.Producer.OperationId}' did not create or change '{entity}'. A redundant ensure does not own an entity.");
            }

            void CheckWrite(string property)
            {
                if (writes.TryGetValue(property, out EncounterPatchOrigin? writer))
                {
                    EncounterPatchDocument.Require(op.Producer is not null && writer.DocumentId == op.Producer.DocumentId && writer.OperationId == op.Producer.OperationId, $"Conflicting write to '{property}' owned by {writer.PackageId}/{writer.PayloadPath}, {writer.DocumentId}/{writer.OperationId}. Reference that producer and guard its value, or disable the incompatible package.");
                }
            }

            void GuardValue(JsonElement observed)
            {
                EncounterPatchDocument.Require(SemanticEqual(observed, op.ExpectedValue), $"Guard failed for '{target}/{op.Property}': expected {op.ExpectedValue.GetRawText()}, observed {observed.GetRawText()}. Edit expectedValue or satisfy the referenced dependency.");
            }

            T Edit<T>(T current, string property, JsonElement expected, JsonElement value, string entity)
            {
                string writeKey = entity + "/" + property;
                CheckWrite(writeKey);
                JsonObject root = JsonSerializer.SerializeToNode(current, EncounterPatchDocument.JsonOptions)!.AsObject();
                string[] parts = property.Split('.');
                JsonObject parent = root;
                foreach (string part in parts[..^1])
                {
                    EncounterPatchDocument.Require(parent[part] is JsonObject, "Missing property parent: " + property);
                    parent = parent[part]!.AsObject();
                }
                JsonElement observed = JsonSerializer.SerializeToElement(parent[parts[^1]], EncounterPatchDocument.JsonOptions);
                EncounterPatchDocument.Require(SemanticEqual(observed, expected), $"Guard failed for '{entity}/{property}': expected {expected.GetRawText()}, observed {observed.GetRawText()}.");
                parent[parts[^1]] = JsonNode.Parse(value.GetRawText());
                T result = root.Deserialize<T>(EncounterPatchDocument.JsonOptions) ?? throw new InvalidDataException("Null edited definition.");
                writes[writeKey] = origin;
                return result;
            }
        }
        ValidateEffective();
        string identityInput = JsonSerializer.Serialize(baseline, EncounterPatchDocument.JsonOptions) + "\n" + string.Join("\n", layers.Select(layer => JsonSerializer.Serialize(layer.Document, EncounterPatchDocument.JsonOptions)));
        string identity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identityInput)));
        return new(baseline.Encounter, baseline.SourceProfile, identity, properties.ToImmutableDictionary(), templates.ToImmutableDictionary(), groups.ToImmutableDictionary(), policies.ToImmutableDictionary(), presentations.ToImmutableDictionary(), routes.ToImmutableDictionary(), gates.ToImmutableDictionary(), assignments.ToImmutableDictionary(), placements.ToImmutableDictionary(), origins.ToImmutableArray(), outcomes.ToImmutableArray());

        void ValidateEffective()
        {
            HashSet<string> actors = new(baseline.Actors.Concat(templates.Keys), StringComparer.Ordinal);
            if (actors.Count > baseline.ActiveActorCapacity)
            {
                throw EffectiveFailure(templates.Keys.Last(), $"Encounter requires {actors.Count} live actors; selected profile supports {baseline.ActiveActorCapacity}. Reduce reserved templates or disable the incompatible package; no actors were evicted.");
            }
            foreach ((string key, EncounterPatchOperation template) in templates)
            {
                string document = owners[key].DocumentId;
                RequireReference(actors.Contains(template.InheritFrom!) || actors.Contains(Scope(document, template.InheritFrom!)), key, "inheritFrom", template.InheritFrom!);
                RequireReference(groups.ContainsKey(Scope(document, template.Group!)), key, "group", template.Group!);
                RequireReference(policies.ContainsKey(Scope(document, template.NormalActionPolicy!)), key, "normalActionPolicy", template.NormalActionPolicy!);
                RequireReference(presentations.ContainsKey(Scope(document, template.Arrival!)), key, "arrival", template.Arrival!);
            }
            foreach ((string group, ImmutableArray<string> members) in groups)
            {
                foreach (string member in members)
                {
                    RequireReference(actors.Contains(member), group, "members", member);
                }
            }
            foreach ((string actor, string policy) in assignments)
            {
                RequireReference(actors.Contains(actor), actor, "actor", actor);
                RequireReference(policies.ContainsKey(policy), actor, "policy", policy);
            }
            foreach (string key in properties.Keys)
            {
                string actor = key[..key.LastIndexOf('/')];
                RequireReference(actors.Contains(actor), actor, "actor", actor);
            }
            foreach ((string key, EncounterPatchPolicy policy) in policies)
            {
                try
                {
                    EncounterPatchDocument.ValidateRules(policy.Rules);
                }
                catch (InvalidDataException error)
                {
                    throw EffectiveFailure(key, error.Message, error);
                }
                string document = owners[key].DocumentId;
                foreach (EncounterPatchRule rule in policy.Rules)
                {
                    if (rule.Action.Target.ValueKind == JsonValueKind.Object)
                    {
                        EncounterPatchTarget target = rule.Action.Target.Deserialize<EncounterPatchTarget>(EncounterPatchDocument.JsonOptions)!;
                        RequireReference(groups.ContainsKey(Scope(document, target.Group)), key, "target.group", target.Group);
                    }
                    foreach (EncounterPatchSpawn spawn in rule.Action.Actors)
                    {
                        RequireReference(templates.ContainsKey(Scope(document, spawn.Template)), key, "template", spawn.Template);
                        RequireReference(placements.ContainsKey(Scope(document, spawn.Anchor)), key, "anchor", spawn.Anchor);
                    }
                }
            }
            HashSet<string> routedSources = new(StringComparer.Ordinal);
            foreach ((string key, EncounterPatchOperation route) in routes)
            {
                string document = owners[key].DocumentId;
                string Resolve(string actor) => baseline.Actors.Contains(actor) ? actor : Scope(document, actor);
                string source = Resolve(route.SourceActor!);
                RequireReference(actors.Contains(source), key, "sourceActor", source);
                RequireReference(routedSources.Add(source), key, "sourceActor", source + " already has exclusive damage-routing ownership");
                foreach (string recipient in route.Recipients)
                {
                    RequireReference(actors.Contains(Resolve(recipient)), key, "recipient", recipient);
                    RequireReference(!routes.Any(other => (baseline.Actors.Contains(other.Value.SourceActor!) ? other.Value.SourceActor : Scope(owners[other.Key].DocumentId, other.Value.SourceActor!)) == Resolve(recipient)), key, "recipient", recipient + " is itself a damage-route source (route chains are unsupported)");
                }
            }
            foreach ((string key, EncounterPatchOperation gate) in gates)
            {
                string actor = baseline.Actors.Contains(gate.Actor!) ? gate.Actor! : Scope(owners[key].DocumentId, gate.Actor!);
                RequireReference(actors.Contains(actor), key, "actor", actor);
            }
        }

        void RequireReference(bool valid, string target, string property, string observed)
        {
            if (!valid)
            {
                throw EffectiveFailure(target, $"Unresolved or conflicting '{property}': '{observed}'. Supply the referenced definition/anchor or remove the incompatible contribution.");
            }
        }

        InvalidDataException EffectiveFailure(string target, string message, Exception? inner = null)
        {
            string context = owners.TryGetValue(target, out EncounterPatchOrigin? owner) ? $"Package '{owner.PackageId}', payload '{owner.PayloadPath}', document '{owner.DocumentId}', operation '{owner.OperationId}', " : "";
            return new InvalidDataException(context + $"encounter '{baseline.Encounter}', target '{target}': {message} Candidate rejected atomically.", inner);
        }
    }

    private static List<(EncounterPatchLayer Layer, EncounterPatchOperation Operation)> Order(EncounterPatchLayer[] layers)
    {
        List<(EncounterPatchLayer Layer, EncounterPatchOperation Operation)> nodes = layers.SelectMany(layer => layer.Document.Operations.Select(operation => (layer, operation))).ToList();
        Dictionary<(string Document, string Operation), int> indices = nodes.Select((node, index) => (node, index)).ToDictionary(pair => (pair.node.Layer.Document.DocumentId, pair.node.Operation.Id), pair => pair.index);
        List<HashSet<int>> dependencies = nodes.Select(_ => new HashSet<int>()).ToList();
        for (int index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node.Operation.Producer is { } producer)
            {
                if (!indices.TryGetValue((producer.DocumentId, producer.OperationId), out int dependency))
                {
                    throw Failure(node.Layer, node.Operation, $"Missing producer '{producer.DocumentId}/{producer.OperationId}'. Enable the producing package or fix the reference.");
                }
                dependencies[index].Add(dependency);
                if (producer.DocumentId != node.Layer.Document.DocumentId)
                {
                    foreach (var consumer in indices.Where(pair => pair.Key.Document == node.Layer.Document.DocumentId))
                    {
                        foreach (var entry in indices.Where(pair => pair.Key.Document == producer.DocumentId))
                        {
                            dependencies[consumer.Value].Add(entry.Value);
                        }
                    }
                }
            }
            EncounterPatchOperation operation = node.Operation;
            if (operation.Kind is EncounterPatchOperationKind.AddRule or EncounterPatchOperationKind.SetRuleProperty or EncounterPatchOperationKind.RemoveRule or EncounterPatchOperationKind.SetDamageRouteProperty or EncounterPatchOperationKind.RemoveDamageRoute or EncounterPatchOperationKind.SetPresentationProperty)
            {
                string document = operation.Producer?.DocumentId ?? node.Layer.Document.DocumentId;
                for (int other = 0; other < nodes.Count; other++)
                {
                    EncounterPatchOperation candidate = nodes[other].Operation;
                    if (other != index && nodes[other].Layer.Document.DocumentId == document && (operation.Policy is not null && candidate.Kind == EncounterPatchOperationKind.AddActionPolicy && candidate.Policy == operation.Policy || operation.Route is not null && candidate.Kind == EncounterPatchOperationKind.AddDamageRoute && candidate.Route == operation.Route || operation.Presentation is not null && candidate.Kind == EncounterPatchOperationKind.AddPresentation && candidate.Presentation == operation.Presentation))
                    {
                        dependencies[index].Add(other);
                    }
                }
            }
        }
        List<(EncounterPatchLayer Layer, EncounterPatchOperation Operation)> result = [];
        HashSet<int> done = [];
        while (result.Count < nodes.Count)
        {
            int ready = Enumerable.Range(0, nodes.Count).FirstOrDefault(index => !done.Contains(index) && dependencies[index].All(done.Contains), -1);
            if (ready < 0)
            {
                int blocked = Enumerable.Range(0, nodes.Count).First(index => !done.Contains(index));
                throw Failure(nodes[blocked].Layer, nodes[blocked].Operation, "Encounter dependency cycle. Remove a cyclic producer/reference edge.");
            }
            done.Add(ready);
            result.Add(nodes[ready]);
        }
        return result;
    }

    private static string Scope(string document, string id) => document + "/" + id;

    private static bool Ensure<T>(Dictionary<string, T> values, string key, T value)
    {
        if (values.TryGetValue(key, out T? existing))
        {
            EncounterPatchDocument.Require(Equal(existing, value), "Incompatible duplicate identity: " + key);
            return false;
        }
        values.Add(key, value);
        return true;
    }

    private static bool Equal<T>(T left, T right) => SemanticEqual(Definition(left), Definition(right));

    private static JsonElement Definition<T>(T value)
    {
        JsonNode node = JsonSerializer.SerializeToNode(value, EncounterPatchDocument.JsonOptions)!;
        if (value is EncounterPatchOperation && node is JsonObject obj)
        {
            obj.Remove("id");
            obj.Remove("producer");
            obj.Remove("metadata");
        }
        return JsonSerializer.SerializeToElement(node, EncounterPatchDocument.JsonOptions);
    }

    private static bool SemanticEqual(JsonElement left, JsonElement right)
    {
        JsonNode? Normalize(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                foreach (string key in obj.Select(pair => pair.Key).ToArray())
                {
                    if (obj[key] is JsonArray { Count: 0 })
                    {
                        obj.Remove(key);
                    }
                    else
                    {
                        Normalize(obj[key]);
                    }
                }
            }
            else if (node is JsonArray array)
            {
                foreach (JsonNode? item in array)
                {
                    Normalize(item);
                }
            }
            return node;
        }
        return JsonNode.DeepEquals(Normalize(JsonNode.Parse(left.GetRawText())), Normalize(JsonNode.Parse(right.GetRawText())));
    }
    private static InvalidDataException Failure(EncounterPatchLayer layer, EncounterPatchOperation? operation, string message, Exception? inner = null) => new($"Package '{layer.PackageId}', payload '{layer.PayloadPath}', document '{layer.Document.DocumentId}', operation '{operation?.Id ?? "<document>"}', encounter '{layer.Document.Encounter}': {message}", inner);
}



