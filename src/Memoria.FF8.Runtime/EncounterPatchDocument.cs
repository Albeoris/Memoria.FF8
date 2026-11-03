using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Memoria.FF8.Runtime;

public enum EncounterPatchOperationKind
{
    AddActorTemplate,
    EnsureGroup,
    AddActionPolicy,
    AddRule,
    SetRuleProperty,
    RemoveRule,
    SetNormalActionPolicy,
    SetActorProperty,
    AddDamageRoute,
    SetDamageRouteProperty,
    RemoveDamageRoute,
    AddPresentation,
    SetPresentationProperty,
    GateNativeEvent,
    SetCameraProperty
}

public sealed record EncounterPatchProducer(string DocumentId, string OperationId);
public sealed record EncounterPatchCondition(int? OwnTurn = null, int? OwnTurnAtLeast = null, int? OwnTurnAtMost = null);
public sealed record EncounterPatchTarget(string Group, string Select);
public sealed record EncounterPatchSpawn([property: JsonRequired] string Template, [property: JsonRequired] string Anchor);
public sealed record EncounterPatchOpacity([property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] double From, [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] double To, [property: JsonRequired] string Curve);
public sealed record EncounterPatchInheritance(string FromActor, string At);
public sealed record EncounterPatchPlacement([property: JsonRequired] string Id, [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] float X, [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] float Y, [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] float Z);

public sealed record EncounterPatchAction
{
    public required string Kind { get; init; }
    public string? Spell { get; init; }
    public ImmutableArray<string> Spells { get; init; } = [];
    public ImmutableDictionary<string, int> SpellWeights { get; init; } = ImmutableDictionary<string, int>.Empty;
    public JsonElement Target { get; init; }
    public string? TargetPolicy { get; init; }
    public string? Selection { get; init; }
    public JsonElement Casts { get; init; }
    public string? CasterPresentation { get; init; }
    public bool? Simultaneous { get; init; }
    public bool? Atomic { get; init; }
    public ImmutableArray<EncounterPatchSpawn> Actors { get; init; } = [];
    public EncounterPatchReflection? ReflectionPolicy { get; init; }
}

public sealed record EncounterPatchReflection(bool PreferUnreflectedOpponents, string WhenAllOpponentsReflect, string AlliedTargets, string BounceSpellSelection, EncounterPatchAction WithoutReflectedAlly);
public sealed record EncounterPatchRule(string Id, EncounterPatchCondition When, EncounterPatchAction Action, int? Priority = null);

/// <summary>Bounded declarative operations; arbitrary expressions and executable extension fields are rejected.</summary>
public sealed record EncounterPatchOperation
{
    public required string Id { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required EncounterPatchOperationKind Kind { get; init; }
    public EncounterPatchProducer? Producer { get; init; }
    public string? ActorTemplate { get; init; }
    public string? InheritFrom { get; init; }
    public ImmutableArray<string> Inherit { get; init; } = [];
    public EncounterPatchInheritance? Level { get; init; }
    public EncounterPatchInheritance? MaximumHp { get; init; }
    public string? InitialHp { get; init; }
    public int? InitialAtb { get; init; }
    public string? TransientState { get; init; }
    public string? Group { get; init; }
    public ImmutableArray<string> Members { get; init; } = [];
    public string? NormalActionPolicy { get; init; }
    public string? NativeLifecycle { get; init; }
    public string? RewardPolicy { get; init; }
    public string? Arrival { get; init; }
    public string? Policy { get; init; }
    public ImmutableArray<EncounterPatchRule> Rules { get; init; } = [];
    public string? Rule { get; init; }
    public EncounterPatchRule? RuleDefinition { get; init; }
    public string? Actor { get; init; }
    public string? ExpectedOwner { get; init; }
    public bool? PreserveNativeLifecycle { get; init; }
    public string? Property { get; init; }
    public JsonElement ExpectedValue { get; init; }
    public JsonElement Value { get; init; }
    public string? Route { get; init; }
    public string? SourceActor { get; init; }
    public ImmutableArray<string> Recipients { get; init; } = [];
    public string? RecipientSelection { get; init; }
    public string? Mode { get; init; }
    public string? DamageKinds { get; init; }
    public string? Mitigation { get; init; }
    public string? Remainder { get; init; }
    public string? Overkill { get; init; }
    public string? WithoutRecipients { get; init; }
    public string? Presentation { get; init; }
    public string? Clip { get; init; }
    public string? FrameNumbering { get; init; }
    public int? StartFrame { get; init; }
    public int? EndFrame { get; init; }
    public EncounterPatchOpacity? Opacity { get; init; }
    public bool? Loop { get; init; }
    public string? After { get; init; }
    public string? Event { get; init; }
    public int? OwnTurnAtLeast { get; init; }
    public bool? PreserveOrder { get; init; }
    public int? MaximumQueuedMessagesPerTurn { get; init; }
    public Dictionary<string, JsonElement>? Metadata { get; init; }
}

public sealed record EncounterPatchDocument
{
    public required int SchemaVersion { get; init; }
    public required string DocumentId { get; init; }
    public required string Encounter { get; init; }
    public required bool Atomic { get; init; }
    public string? Title { get; init; }
    public string? SourceProfile { get; init; }
    public int? RequiredActorCapacity { get; init; }
    public ImmutableArray<EncounterPatchPlacement> Placements { get; init; } = [];
    public required ImmutableArray<EncounterPatchOperation> Operations { get; init; }
    public Dictionary<string, JsonElement>? Metadata { get; init; }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        MaxDepth = 48,
        TypeInfoResolver = CreateTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter<EncounterPatchOperationKind>(allowIntegerValues: false) }
    };

    private static DefaultJsonTypeInfoResolver CreateTypeInfoResolver()
    {
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(EncounterPatchOperation) && info.Type != typeof(EncounterPatchAction))
            {
                return;
            }
            foreach (JsonPropertyInfo property in info.Properties)
            {
                if (property.PropertyType == typeof(ImmutableArray<string>))
                {
                    property.ShouldSerialize = (_, value) => value is ImmutableArray<string> array && !array.IsDefaultOrEmpty;
                }
                else if (property.PropertyType == typeof(ImmutableArray<EncounterPatchRule>))
                {
                    property.ShouldSerialize = (_, value) => value is ImmutableArray<EncounterPatchRule> array && !array.IsDefaultOrEmpty;
                }
                else if (property.PropertyType == typeof(ImmutableArray<EncounterPatchSpawn>))
                {
                    property.ShouldSerialize = (_, value) => value is ImmutableArray<EncounterPatchSpawn> array && !array.IsDefaultOrEmpty;
                }
                else if (property.PropertyType == typeof(ImmutableDictionary<string, int>))
                {
                    property.ShouldSerialize = (_, value) => value is ImmutableDictionary<string, int> values && !values.IsEmpty;
                }
            }
        });
        return resolver;
    }
    public static EncounterPatchDocument Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > 2 * 1024 * 1024)
        {
            throw new InvalidDataException("Encounter payload exceeds the 2 MiB limit.");
        }
        try
        {
            using JsonDocument raw = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 48 });
            RejectDuplicateProperties(raw.RootElement, "$");
            EncounterPatchDocument document = (JsonSerializer.Deserialize<EncounterPatchDocument>(json, JsonOptions) ?? throw new InvalidDataException("Encounter payload must be an object.")).NormalizeAliases();
            document.Validate();
            int operationIndex = 0;
            foreach (JsonElement rawOperation in raw.RootElement.GetProperty("operations").EnumerateArray())
            {
                EncounterPatchOperation operation = document.Operations[operationIndex++];
                HashSet<string> allowed = new(("id kind producer metadata " + OperationFields(operation.Kind)).Split(' '), StringComparer.Ordinal);
                foreach (JsonProperty field in rawOperation.EnumerateObject())
                {
                    Require(allowed.Contains(field.Name), $"Document '{document.DocumentId}', operation '{operation.Id}': field '{field.Name}' is not executable for {operation.Kind}.");
                }
            }
            return document;
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Invalid encounter JSON: " + error.Message, error);
        }
    }

    public void Validate()
    {
        Require(SchemaVersion == 1 && Atomic, "schemaVersion must be 1 and atomic must be true.");
        Identity(DocumentId, "documentId");
        Identity(Encounter, "encounter");
        Require(!Operations.IsDefaultOrEmpty && Operations.Length <= 1024, "operations must contain 1–1024 entries.");
        Require(RequiredActorCapacity is null or >= 1 and <= 4, "requiredActorCapacity must be between 1 and 4.");
        Require(!Placements.IsDefault && Placements.Length <= 16, "placements must be a bounded array.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (EncounterPatchPlacement placement in Placements)
        {
            Require(placement is not null && ids.Add(placement.Id), "Duplicate or null placement.");
            Identity(placement.Id, "placement");
            Require(float.IsFinite(placement.X) && float.IsFinite(placement.Y) && float.IsFinite(placement.Z) && Math.Abs(placement.X) <= 100000 && Math.Abs(placement.Y) <= 100000 && Math.Abs(placement.Z) <= 100000, "Placement coordinates must be finite and bounded.");
        }
        ids.Clear();
        foreach (EncounterPatchOperation original in Operations)
        {
            Require(original is not null, "Null operation.");
            EncounterPatchOperation operation = original;
            Identity(operation.Id, "operation");
            Require(ids.Add(operation.Id), "Duplicate operation ID: " + operation.Id);
            try
            {
                operation = NormalizeOperation(original);
                ValidateOperation(operation);
            }
            catch (Exception error) when (error is InvalidDataException or JsonException)
            {
                throw new InvalidDataException($"Document '{DocumentId}', operation '{operation.Id}': {error.Message}", error);
            }
        }
    }

    internal EncounterPatchDocument NormalizeAliases()
    {
        Require(!Operations.IsDefault && Operations.Length <= 1024, "operations must be a bounded array.");
        ImmutableArray<EncounterPatchOperation>.Builder operations = ImmutableArray.CreateBuilder<EncounterPatchOperation>(Operations.Length);
        foreach (EncounterPatchOperation operation in Operations)
        {
            Require(operation is not null, "Null encounter operation.");
            try
            {
                operations.Add(NormalizeOperation(operation));
            }
            catch (Exception error) when (error is InvalidDataException or JsonException)
            {
                throw new InvalidDataException($"Document '{DocumentId}', operation '{operation.Id}': {error.Message}", error);
            }
        }
        return this with { Operations = operations.MoveToImmutable() };
    }

    private static string CanonicalSpell(string name) => name switch
    {
        "Blizaga" => "Blizzaga",
        "Darkness" => "Blind",
        "Reflection" => "Reflect",
        _ => name
    };

    private static EncounterPatchOperation NormalizeOperation(EncounterPatchOperation operation)
    {
        string? property = operation.Property;
        const string weightPrefix = "action.spellWeights.";
        if (property?.StartsWith(weightPrefix, StringComparison.Ordinal) == true)
        {
            property = weightPrefix + CanonicalSpell(property[weightPrefix.Length..]);
        }
        JsonElement NormalizeValue(JsonElement value)
        {
            if (property == "action.spell" && value.ValueKind == JsonValueKind.String)
            {
                return JsonSerializer.SerializeToElement(CanonicalSpell(value.GetString()!));
            }
            if (property == "action.spells" && value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(entry => entry.ValueKind == JsonValueKind.String))
            {
                return JsonSerializer.SerializeToElement(value.EnumerateArray().Select(entry => CanonicalSpell(entry.GetString()!)).ToArray());
            }
            if (operation.Kind == EncounterPatchOperationKind.RemoveRule && value.ValueKind == JsonValueKind.Object)
            {
                EncounterPatchRule rule = value.Deserialize<EncounterPatchRule>(JsonOptions) ?? throw new InvalidDataException("Rule removal requires its full expected definition.");
                return JsonSerializer.SerializeToElement(NormalizeRule(rule), JsonOptions);
            }
            return value;
        }
        return operation with
        {
            Property = property,
            ExpectedValue = NormalizeValue(operation.ExpectedValue),
            Value = NormalizeValue(operation.Value),
            Rules = operation.Rules.IsDefault ? operation.Rules : operation.Rules.Select(NormalizeRule).ToImmutableArray(),
            RuleDefinition = operation.RuleDefinition is null ? null : NormalizeRule(operation.RuleDefinition)
        };
    }

    private static EncounterPatchRule NormalizeRule(EncounterPatchRule rule)
    {
        Require(rule is not null && rule.Action is not null, "Null rule or action.");
        return rule with { Action = NormalizeAction(rule.Action) };
    }

    private static EncounterPatchAction NormalizeAction(EncounterPatchAction action)
    {
        Require(action is not null, "Null action.");
        ImmutableDictionary<string, int>.Builder weights = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        Require(action.SpellWeights is not null, "Spell weights cannot be null.");
        foreach ((string spell, int weight) in action.SpellWeights)
        {
            string canonical = CanonicalSpell(spell);
            Require(weights.TryAdd(canonical, weight), "Duplicate spell-weight identity after canonicalization: " + canonical);
        }
        return action with
        {
            Spell = action.Spell is null ? null : CanonicalSpell(action.Spell),
            Spells = action.Spells.IsDefault ? action.Spells : action.Spells.Select(CanonicalSpell).ToImmutableArray(),
            SpellWeights = weights.ToImmutable(),
            ReflectionPolicy = action.ReflectionPolicy is { } reflection ? reflection with { WithoutReflectedAlly = NormalizeAction(reflection.WithoutReflectedAlly) } : null
        };
    }

    private static string OperationFields(EncounterPatchOperationKind kind) => kind switch
        {
            EncounterPatchOperationKind.AddActorTemplate => "actorTemplate inheritFrom inherit level maximumHp initialHp initialAtb transientState group normalActionPolicy nativeLifecycle rewardPolicy arrival",
            EncounterPatchOperationKind.EnsureGroup => "group members",
            EncounterPatchOperationKind.AddActionPolicy => "policy rules",
            EncounterPatchOperationKind.AddRule => "policy ruleDefinition",
            EncounterPatchOperationKind.SetRuleProperty => "policy rule property expectedValue value",
            EncounterPatchOperationKind.RemoveRule => "policy rule expectedValue",
            EncounterPatchOperationKind.SetNormalActionPolicy => "actor policy expectedOwner preserveNativeLifecycle",
            EncounterPatchOperationKind.SetActorProperty => "actor property expectedValue value",
            EncounterPatchOperationKind.SetCameraProperty => "property expectedValue value",
            EncounterPatchOperationKind.AddDamageRoute => "route sourceActor recipients recipientSelection mode damageKinds mitigation remainder overkill withoutRecipients",
            EncounterPatchOperationKind.SetDamageRouteProperty => "route property expectedValue value",
            EncounterPatchOperationKind.RemoveDamageRoute => "route expectedValue",
            EncounterPatchOperationKind.AddPresentation => "presentation clip frameNumbering startFrame endFrame opacity loop after",
            EncounterPatchOperationKind.SetPresentationProperty => "presentation property expectedValue value",
            EncounterPatchOperationKind.GateNativeEvent => "actor event ownTurnAtLeast preserveOrder maximumQueuedMessagesPerTurn",
            _ => throw new InvalidDataException("Unknown operation kind.")
    };

    internal static void ValidateOperation(EncounterPatchOperation op)
    {
        Require(Enum.IsDefined(op.Kind), "Unknown operation kind.");
        if (op.Producer is { } producer)
        {
            Identity(producer.DocumentId, "producer.documentId");
            Identity(producer.OperationId, "producer.operationId");
        }
        string fields = OperationFields(op.Kind);
        HashSet<string> allowed = new(("id kind producer metadata " + fields).Split(' '), StringComparer.Ordinal);
        JsonElement serialized = JsonSerializer.SerializeToElement(op, JsonOptions);
        foreach (JsonProperty field in serialized.EnumerateObject())
        {
            if (field.Value.ValueKind == JsonValueKind.Undefined || field.Value.ValueKind == JsonValueKind.Array && field.Value.GetArrayLength() == 0)
            {
                continue;
            }
            Require(allowed.Contains(field.Name), $"Field '{field.Name}' is not executable for {op.Kind}.");
        }
        switch (op.Kind)
        {
            case EncounterPatchOperationKind.AddActorTemplate:
                Identity(op.ActorTemplate, "actorTemplate");
                Identity(op.InheritFrom, "inheritFrom");
                Identity(op.Group, "group");
                Identity(op.NormalActionPolicy, "normalActionPolicy");
                Identity(op.Arrival, "arrival");
                Require(!op.Inherit.IsDefaultOrEmpty && op.Inherit.Distinct().Count() == op.Inherit.Length && op.Inherit.All(value => value is "model" or "resolvedCombatStats" or "drawDefinitions"), "Unsupported inheritance contract.");
                Require(op.Level is { At: "spawn" } && op.Level.FromActor == op.InheritFrom && op.MaximumHp is { At: "spawn" } && op.MaximumHp.FromActor == op.InheritFrom, "Level and maximumHp must inherit from the resolved source actor at spawn.");
                Require(op.InitialHp == "maximum" && op.InitialAtb == 0 && op.TransientState == "fresh" && op.NativeLifecycle == "ordinarySummonedActor" && op.RewardPolicy == "none", "Templates require fresh full-HP, zero-ATB summoned actors without rewards.");
                break;
            case EncounterPatchOperationKind.EnsureGroup:
                Identity(op.Group, "group");
                Strings(op.Members, "members", 4);
                break;
            case EncounterPatchOperationKind.AddActionPolicy:
                Identity(op.Policy, "policy");
                ValidateRules(op.Rules);
                break;
            case EncounterPatchOperationKind.AddRule:
                Identity(op.Policy, "policy");
                Require(op.RuleDefinition is not null, "ruleDefinition is required.");
                ValidateRules([op.RuleDefinition!]);
                break;
            case EncounterPatchOperationKind.SetRuleProperty:
                Identity(op.Policy, "policy");
                Identity(op.Rule, "rule");
                bool spellWeight = op.Property?.StartsWith("action.spellWeights.", StringComparison.Ordinal) == true;
                Require(spellWeight || op.Property is "action.spell" or "action.spells" or "when.ownTurn" or "when.ownTurnAtLeast" or "when.ownTurnAtMost" or "priority", "Unsupported rule property.");
                Guard(op);
                if (spellWeight)
                {
                    Spell(op.Property!["action.spellWeights.".Length..]);
                    Require(op.ExpectedValue.ValueKind == JsonValueKind.Number && op.ExpectedValue.TryGetInt32(out int expectedWeight) && expectedWeight is >= 1 and <= 255 && op.Value.ValueKind == JsonValueKind.Number && op.Value.TryGetInt32(out int weight) && weight is >= 1 and <= 255, "Spell-weight edits require integer expectedValue/value between 1 and 255.");
                }
                break;
            case EncounterPatchOperationKind.RemoveRule:
                Identity(op.Policy, "policy");
                Identity(op.Rule, "rule");
                Require(op.Producer is not null && op.ExpectedValue.ValueKind == JsonValueKind.Object, "Removal requires producer and full expectedValue.");
                break;
            case EncounterPatchOperationKind.SetCameraProperty:
                Require(op.Property == "elevationDegrees", "Unsupported camera property.");
                Guard(op);
                Require(op.ExpectedValue.ValueKind == JsonValueKind.Number && op.ExpectedValue.TryGetDouble(out double expectedElevation) && double.IsFinite(expectedElevation) && expectedElevation is >= -45 and <= 45 && op.Value.ValueKind == JsonValueKind.Number && op.Value.TryGetDouble(out double elevation) && double.IsFinite(elevation) && elevation is >= -45 and <= 45, "Camera elevationDegrees expectedValue and value must be finite numbers between -45 and 45.");
                break;
            case EncounterPatchOperationKind.SetActorProperty:
                Identity(op.Actor, "actor");
                Require(op.Property is "stats.maximumHpMultiplier" or EncounterActorProperties.ReflectResistanceMultiplier, "Unsupported actor property.");
                Guard(op);
                double minimumMultiplier = op.Property == EncounterActorProperties.ReflectResistanceMultiplier ? 0 : 0.01;
                Require(op.Value.ValueKind == JsonValueKind.Number && op.Value.TryGetDouble(out double multiplier) && double.IsFinite(multiplier) && multiplier >= minimumMultiplier && multiplier <= 100, "Actor multiplier is outside its supported range.");
                break;
            case EncounterPatchOperationKind.SetNormalActionPolicy:
                Identity(op.Actor, "actor");
                Identity(op.Policy, "policy");
                Identity(op.ExpectedOwner, "expectedOwner");
                Require(op.PreserveNativeLifecycle == true, "Normal-action assignment must preserve native lifecycle.");
                break;
            case EncounterPatchOperationKind.AddDamageRoute:
                Identity(op.Route, "route");
                Identity(op.SourceActor, "sourceActor");
                Strings(op.Recipients, "recipients", 3);
                Require(!op.Recipients.Contains(op.SourceActor!) && op.RecipientSelection == "livingActivatedInstances" && op.Mode == "redirectSplitEqually" && op.DamageKinds == "allPositiveHpDamage" && op.Mitigation == "alreadyResolved" && op.Remainder == "discard" && op.Overkill == "discard" && op.WithoutRecipients == "normalDamage", "Unsupported damage-routing contract.");
                break;
            case EncounterPatchOperationKind.SetDamageRouteProperty:
                Identity(op.Route, "route");
                Require(op.Property == "recipients", "Only recipients is an editable damage-route property.");
                Guard(op);
                break;
            case EncounterPatchOperationKind.RemoveDamageRoute:
                Identity(op.Route, "route");
                Require(op.Producer is not null && op.ExpectedValue.ValueKind == JsonValueKind.Object, "Removal requires producer and full expectedValue.");
                break;
            case EncounterPatchOperationKind.AddPresentation:
                Identity(op.Presentation, "presentation");
                Require(op.Clip is { Length: > 0 and <= 32 } && op.FrameNumbering == "zeroBased" && op.StartFrame is >= 0 && op.EndFrame > op.StartFrame && op.EndFrame <= 65535 && op.Opacity is { Curve: "linear" } && double.IsFinite(op.Opacity.From) && double.IsFinite(op.Opacity.To) && op.Opacity.From is >= 0 and <= 1 && op.Opacity.To is >= 0 and <= 1 && op.Loop == false && op.After == "nativeIdle", "Presentation requires a finite zero-based nonlooping clip and linear opacity in [0,1].");
                break;
            case EncounterPatchOperationKind.SetPresentationProperty:
                Identity(op.Presentation, "presentation");
                Require(op.Property is "startFrame" or "endFrame" or "opacity.from" or "opacity.to", "Unsupported presentation property.");
                Guard(op);
                break;
            case EncounterPatchOperationKind.GateNativeEvent:
                Identity(op.Actor, "actor");
                Require(op.Event == "nativeTaunts" && op.OwnTurnAtLeast is >= 1 && op.PreserveOrder == true && op.MaximumQueuedMessagesPerTurn == 1, "Unsupported native event gate.");
                break;
        }
    }

    internal static void ValidateRules(ImmutableArray<EncounterPatchRule> rules)
    {
        Require(!rules.IsDefaultOrEmpty && rules.Length <= 128, "A policy requires 1–128 rules.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (EncounterPatchRule rule in rules)
        {
            Require(rule is not null && rule.When is not null && rule.Action is not null, "Null rule, condition or action.");
            Identity(rule.Id, "rule.id");
            Require(ids.Add(rule.Id), "Duplicate rule ID: " + rule.Id);
            EncounterPatchCondition condition = rule.When;
            Require(condition.OwnTurn is null or >= 1 && condition.OwnTurnAtLeast is null or >= 1 && condition.OwnTurnAtMost is null or >= 1 && (condition.OwnTurn is not null || condition.OwnTurnAtLeast is not null || condition.OwnTurnAtMost is not null), "Own-turn predicates must be positive and nonempty.");
            Require(condition.OwnTurn is null || condition.OwnTurnAtLeast is null && condition.OwnTurnAtMost is null, "ownTurn cannot be combined with ranges.");
            Require(condition.OwnTurnAtLeast is null || condition.OwnTurnAtMost is null || condition.OwnTurnAtLeast <= condition.OwnTurnAtMost, "Empty own-turn range.");
            ValidateAction(rule.Action);
        }
        for (int first = 0; first < rules.Length; first++)
        {
            for (int second = first + 1; second < rules.Length; second++)
            {
                EncounterPatchRule a = rules[first];
                EncounterPatchRule b = rules[second];
                int aMin = a.When.OwnTurn ?? a.When.OwnTurnAtLeast ?? 1;
                int aMax = a.When.OwnTurn ?? a.When.OwnTurnAtMost ?? int.MaxValue;
                int bMin = b.When.OwnTurn ?? b.When.OwnTurnAtLeast ?? 1;
                int bMax = b.When.OwnTurn ?? b.When.OwnTurnAtMost ?? int.MaxValue;
                Require(aMin > bMax || bMin > aMax || a.Priority is not null && b.Priority is not null && a.Priority != b.Priority, $"Exclusive rules '{a.Id}' and '{b.Id}' overlap without distinct explicit priorities.");
            }
        }
    }

    private static void ValidateAction(EncounterPatchAction action)
    {
        Require(!action.Spells.IsDefault && !action.Actors.IsDefault && action.SpellWeights is not null, "Action arrays/weights cannot be null/default.");
        if (action.Kind == "SummonGroup")
        {
            Require(action.CasterPresentation == "nativeCast" && action.Simultaneous == true && action.Atomic == true && action.Actors.Length is >= 1 and <= 3 && action.Spell is null && action.Spells.IsEmpty && action.SpellWeights.IsEmpty && action.Target.ValueKind == JsonValueKind.Undefined && action.TargetPolicy is null && action.Selection is null && action.Casts.ValueKind == JsonValueKind.Undefined && action.ReflectionPolicy is null, "SummonGroup requires an atomic simultaneous native cast and 1–3 templates.");
            foreach (EncounterPatchSpawn spawn in action.Actors)
            {
                Require(spawn is not null, "Null spawn.");
                Identity(spawn.Template, "template");
                Identity(spawn.Anchor, "anchor");
            }
            Require(action.Actors.Select(spawn => spawn.Template).Distinct().Count() == action.Actors.Length, "A summon cannot instantiate a template twice.");
            return;
        }
        Require(action.Kind is "Cast" or "CastFromPool", "Unsupported action kind: " + action.Kind);
        Require(action.CasterPresentation is null && action.Simultaneous is null && action.Atomic is null && action.Actors.IsEmpty, "Cast has summon-only executable fields.");
        if (action.Kind == "Cast")
        {
            Spell(action.Spell);
            Require(action.Spells.IsEmpty && action.SpellWeights.IsEmpty && action.Selection is null, "Cast cannot specify a pool or weights.");
        }
        else
        {
            Strings(action.Spells, "spells", 32);
            foreach (string spell in action.Spells)
            {
                Spell(spell);
            }
            Require(action.Spell is null && action.Selection is null or "uniform", "Pool selection must be uniform.");
            Require(action.SpellWeights.All(pair => action.Spells.Contains(pair.Key, StringComparer.Ordinal) && pair.Value is >= 1 and <= 255) && action.Spells.Sum(spell => action.SpellWeights.GetValueOrDefault(spell, 1)) <= 256, "spellWeights must name pool spells, use integer weights in 1–255, and total at most 256 including omitted defaults of 1.");
        }
        Require(action.Casts.ValueKind == JsonValueKind.Undefined || action.Casts.ValueKind == JsonValueKind.String && action.Casts.GetString() == "nativeDouble" || action.Casts.ValueKind == JsonValueKind.Number && action.Casts.TryGetInt32(out int casts) && casts == 1, "casts must be 1 or nativeDouble.");
        Require(action.TargetPolicy is null or "scanKnownWeakestElseRandom", "Unknown targetPolicy.");
        if (action.TargetPolicy is not null)
        {
            Require(action.Kind == "CastFromPool" && action.Target.ValueKind == JsonValueKind.Undefined, "targetPolicy requires a pool and excludes target.");
        }
        else if (action.Target.ValueKind == JsonValueKind.String)
        {
            Require(action.Target.GetString() is "self" or "randomOpponent" or "randomAlly" or "allOpponents" or "nativeOpponentTargeting" or "randomReflectedOpponent", "Unsupported target selector.");
        }
        else
        {
            Require(action.Target.ValueKind == JsonValueKind.Object, "A cast requires target or targetPolicy.");
            EncounterPatchTarget target = action.Target.Deserialize<EncounterPatchTarget>(JsonOptions) ?? throw new InvalidDataException("Null target.");
            Identity(target.Group, "target.group");
            Require(target.Select == "allLiving", "Unsupported group selection.");
        }
        if (action.ReflectionPolicy is { } reflection)
        {
            Require(action.Kind == "CastFromPool" && action.TargetPolicy is not null && reflection.PreferUnreflectedOpponents && reflection.WhenAllOpponentsReflect == "bounceViaReflectedAlly" && reflection.AlliedTargets == "allAllies" && reflection.BounceSpellSelection == "uniform" && reflection.WithoutReflectedAlly is { Kind: "Cast", Spell: "Dispel", ReflectionPolicy: null } && reflection.WithoutReflectedAlly.Target.ValueKind == JsonValueKind.String && reflection.WithoutReflectedAlly.Target.GetString() == "randomReflectedOpponent", "Unsupported reflection contract.");
            ValidateAction(reflection.WithoutReflectedAlly);
        }
    }

    private static void Spell(string? name)
    {
        Require(name is "Fire" or "Fira" or "Firaga" or "Blizzard" or "Blizzara" or "Blizzaga" or "Thunder" or "Thundara" or "Thundaga" or "Cure" or "Cura" or "Curaga" or "Drain" or "Quake" or "Tornado" or "Aero" or "Water" or "Bio" or "Protect" or "Shell" or "Regen" or "Reflect" or "Blind" or "Silence" or "Confuse" or "Slow" or "Haste" or "Stop" or "Double" or "Triple" or "Scan" or "Dispel", "Unknown canonical spell: " + name);
    }

    private static void Guard(EncounterPatchOperation op)
    {
        Require(op.ExpectedValue.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null && op.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null, "Property edits require expectedValue and value.");
    }

    internal static void Identity(string? value, string field)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 200 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'), field + " requires a stable ASCII identifier (letters, digits, dot, hyphen, underscore).");
    }

    private static void Strings(ImmutableArray<string> values, string field, int maximum)
    {
        Require(!values.IsDefaultOrEmpty && values.Length <= maximum && values.Distinct(StringComparer.Ordinal).Count() == values.Length, field + " requires a bounded nonempty distinct array.");
        foreach (string value in values)
        {
            Identity(value, field);
        }
    }

    internal static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private static void RejectDuplicateProperties(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Require(names.Add(property.Name), "Duplicate JSON property: " + path + "." + property.Name);
                RejectDuplicateProperties(property.Value, path + "." + property.Name);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                RejectDuplicateProperties(item, path + "[]");
            }
        }
    }
}
