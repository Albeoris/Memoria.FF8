using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveRuntimeOwnershipTests
{
    [Fact]
    public void Every_excluded_root_and_every_composite_member_has_a_specific_restoration_authority()
    {
        string[] runtimeRoots = GameplaySaveRootOwnership.Members.Where(pair => pair.Value.Requirement == GameplaySavePersistenceRequirement.Runtime).Select(pair => pair.Key).ToArray();
        Assert.All(runtimeRoots, name => Assert.True(GameplaySaveRuntimeOwnership.Roots.ContainsKey(name), $"Unreviewed excluded root GameState.{name}."));
        foreach ((string name, GameplayRuntimeMemberOwnership authority) in GameplaySaveRuntimeOwnership.Roots)
        {
            PropertyInfo property = typeof(GameState).GetProperty(name) ?? throw new InvalidDataException($"Unknown root {name}.");
            ValidateAuthority(property.PropertyType, authority);
        }
        foreach ((Type type, IReadOnlyDictionary<string, GameplayRuntimeMemberOwnership> members) in GameplaySaveRuntimeOwnership.Members)
        {
            Assert.Equal(SerializedProperties(type).Select(property => property.Name).Order(), members.Keys.Order());
            foreach (PropertyInfo property in SerializedProperties(type))
            {
                ValidateAuthority(property.PropertyType, members[property.Name]);
            }
        }
    }

    [Fact]
    public void Every_nested_serialized_field_is_frozen_including_types_nullability_and_wire_contract_attributes()
    {
        IReadOnlyDictionary<string, string> expected = ReadReviewedSchema();
        IReadOnlyDictionary<string, string> current = DiscoverSchema();
        RequireReviewedSchema(expected, current);
    }

    [Fact]
    public void Schema_check_rejects_an_unreviewed_nested_field_and_a_changed_existing_field_type()
    {
        IReadOnlyDictionary<string, string> reviewed = ReadReviewedSchema();
        Dictionary<string, string> changed = DiscoverSchema().ToDictionary();
        string name = nameof(NativeBattleHeroCrtStackContract);
        Assert.True(reviewed.ContainsKey(name));
        changed[name] += ";NewExternalOwner:System.String";
        Assert.Contains(name, Assert.Throws<InvalidDataException>(() => RequireReviewedSchema(reviewed, changed)).Message);
        changed[name] = reviewed[name].Replace("System.Int32", "System.Int64", StringComparison.Ordinal);
        Assert.Contains(name, Assert.Throws<InvalidDataException>(() => RequireReviewedSchema(reviewed, changed)).Message);
        changed = DiscoverSchema().ToDictionary();
        changed.Add("UnreviewedNestedType", "Value:System.String");
        Assert.Contains("UnreviewedNestedType", Assert.Throws<InvalidDataException>(() => RequireReviewedSchema(reviewed, changed)).Message);
        Assert.Contains(nameof(IncludedField.Value), Assert.Throws<InvalidDataException>(() => SerializedProperties(typeof(IncludedField))).Message);
    }

    [Fact]
    public void External_execution_environment_cannot_hide_under_the_cold_memory_partition()
    {
        IReadOnlyDictionary<string, GameplayRuntimeMemberOwnership> effects = GameplaySaveRuntimeOwnership.Members[typeof(NativeBattleEffectMemory)];
        Assert.Equal(GameplayRuntimeRestoration.SelectedEnvironment, effects[nameof(NativeBattleEffectMemory.ExecutionStack)].Restoration);
        Assert.Equal(GameplayRuntimeRestoration.SelectedEnvironment, effects[nameof(NativeBattleEffectMemory.HeroCrtStack)].Restoration);
        Assert.Equal(GameplayRuntimeRestoration.Composite, GameplaySaveRuntimeOwnership.Members[typeof(NativeBattleRetainedMemory)][nameof(NativeBattleRetainedMemory.EffectMemory)].Restoration);
        Assert.Equal(GameplayRuntimeRestoration.LogicalContinuation, GameplaySaveRuntimeOwnership.Members[typeof(NativeFieldBarsState)][nameof(NativeFieldBarsState.First)].Restoration);
        Assert.Equal(GameplayRuntimeRestoration.LogicalContinuation, GameplaySaveRuntimeOwnership.Members[typeof(NativeFieldShake)][nameof(NativeFieldShake.X)].Restoration);
    }

    [Fact]
    public void Logical_projections_obey_every_declared_member_partition()
    {
        NativeMusicPlaybackState music = new(83, 51, 43, 3) { Mode0 = NativeMusicPlaybackMode.Midi, Mode1 = NativeMusicPlaybackMode.Wave, ConcertMask = 0x1234, BattleFirstLoadB8B7D4 = false };
        NativeMusicPlaybackState initialMusic = NativeMusicPlaybackState.FromColdProcess();
        Check(music, GameplaySaveMusicPlayback.Capture(music)!.Restore(initialMusic), initialMusic);
        NativeSalaryPresentation salary = NativeSalaryPresentation.FromColdProcess().StartPayment(4, 5, 4000, 5000).Advance(21);
        NativeSalaryPresentation initialSalary = NativeSalaryPresentation.FromColdProcess();
        Check(salary, GameplaySaveSalaryPresentation.Capture(salary)!.Restore(initialSalary), initialSalary);
        NativeFieldBarsState bars = NativeFieldBarsState.CreateHidden().DisplayWidth(0, 17, 23, 2, 1, 100, 37, 80) with { AdjacentMirrorWord = 123 };
        NativeFieldBarsState initialBars = NativeFieldBarsState.CreateHidden();
        Check(bars, GameplaySaveFieldPresentation.Capture(bars, null)!.RestoreBars(initialBars), initialBars);
        NativeFieldShake shake = NativeFieldShake.FromColdProcess() with { X = new(true, true, 17, -3, 80, -12, 20, 30, 7) };
        NativeFieldShake initialShake = NativeFieldShake.FromColdProcess();
        Check(shake, GameplaySaveFieldPresentation.Capture(null, shake)!.RestoreShake(initialShake), initialShake);

        static void Check<T>(T source, T restored, T initialized)
        {
            foreach ((string name, GameplayRuntimeMemberOwnership authority) in GameplaySaveRuntimeOwnership.Members[typeof(T)])
            {
                PropertyInfo property = typeof(T).GetProperty(name)!;
                object? expected = property.GetValue(authority.Restoration == GameplayRuntimeRestoration.LogicalContinuation ? source : initialized);
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(property.GetValue(restored))), $"{typeof(T).Name}.{name} violates its {authority.Restoration} contract from {authority.Source}.");
            }
        }
    }

    [Fact]
    public void Fresh_process_memory_has_no_late_producer_or_selected_environment_values()
    {
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        foreach (object owner in new object[] { cold, cold.EffectMemory! })
        {
            foreach ((string name, GameplayRuntimeMemberOwnership authority) in GameplaySaveRuntimeOwnership.Members[owner.GetType()])
            {
                if (authority.Restoration is GameplayRuntimeRestoration.DeferredProducer or GameplayRuntimeRestoration.SelectedEnvironment)
                {
                    Assert.Null(owner.GetType().GetProperty(name)!.GetValue(owner));
                }
            }
        }
    }

    private static void ValidateAuthority(Type type, GameplayRuntimeMemberOwnership authority)
    {
        Assert.False(string.IsNullOrWhiteSpace(authority.Source));
        if (authority.Restoration == GameplayRuntimeRestoration.Composite)
        {
            Assert.True(GameplaySaveRuntimeOwnership.Members.ContainsKey(Nullable.GetUnderlyingType(type) ?? type), $"Composite {type.Name} has no member partition.");
        }
    }

    private static void RequireReviewedSchema(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> current)
    {
        foreach (string type in expected.Keys.Union(current.Keys).Order())
        {
            if (!expected.TryGetValue(type, out string? previous) || !current.TryGetValue(type, out string? actual) || previous != actual)
            {
                throw new InvalidDataException($"Excluded runtime schema changed at {type}. Classify every changed member and its restoration source before updating the reviewed schema. Expected: {expected.GetValueOrDefault(type)}; actual: {current.GetValueOrDefault(type)}");
            }
        }
    }

    private static IReadOnlyDictionary<string, string> ReadReviewedSchema()
    {
        using Stream stream = typeof(GameplaySaveRuntimeOwnershipTests).Assembly.GetManifestResourceStream("GameplaySaveRuntimeSchema.txt") ?? throw new InvalidDataException("The reviewed nested runtime schema is missing.");
        using StreamReader reader = new(stream);
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            int separator = line.IndexOf('|');
            Assert.True(separator > 0);
            result.Add(line[..separator], line[(separator + 1)..]);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> DiscoverSchema()
    {
        Queue<Type> pending = new();
        foreach (string name in GameplaySaveRuntimeOwnership.Roots.Keys)
        {
            Enqueue(typeof(GameState).GetProperty(name)!.PropertyType);
        }
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        Dictionary<string, Type> identities = new(StringComparer.Ordinal);
        while (pending.TryDequeue(out Type? type))
        {
            if (identities.TryGetValue(type.Name, out Type? existing))
            {
                Assert.Equal(existing, type);
                continue;
            }
            identities.Add(type.Name, type);
            PropertyInfo[] properties = SerializedProperties(type);
            result.Add(type.Name, string.Join(';', properties.Select(Signature)));
            foreach (PropertyInfo property in properties)
            {
                Enqueue(property.PropertyType);
            }
        }
        return result;

        void Enqueue(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsArray)
            {
                Enqueue(type.GetElementType()!);
            }
            else if (type.IsGenericType)
            {
                foreach (Type argument in type.GenericTypeArguments)
                {
                    Enqueue(argument);
                }
            }
            else if (!type.IsEnum && type.Namespace?.StartsWith("Memoria.", StringComparison.Ordinal) == true)
            {
                pending.Enqueue(type);
            }
        }
    }

    private static PropertyInfo[] SerializedProperties(Type type)
    {
        FieldInfo? includedField = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(field => field.GetCustomAttribute<JsonIncludeAttribute>() is not null);
        if (includedField is not null)
        {
            throw new InvalidDataException($"Excluded runtime {type.Name}.{includedField.Name} introduces a JsonInclude field; add an explicit field ownership/schema contract before using it.");
        }
        PropertyInfo? includedPrivate = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(property => property.GetCustomAttribute<JsonIncludeAttribute>() is not null);
        if (includedPrivate is not null)
        {
            throw new InvalidDataException($"Excluded runtime {type.Name}.{includedPrivate.Name} introduces a nonpublic JsonInclude property; add an explicit ownership/schema contract before using it.");
        }
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod is not null && property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always).OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
    }

    private static string Signature(PropertyInfo property)
    {
        string signature = property.Name + ":" + property.PropertyType;
        if (property.GetCustomAttribute<JsonPropertyNameAttribute>() is { } named)
        {
            signature += "=>" + named.Name;
        }
        if (property.GetCustomAttribute<JsonRequiredAttribute>() is not null)
        {
            signature += "!required";
        }
        if (property.GetCustomAttribute<JsonIgnoreAttribute>() is { } conditional)
        {
            signature += "?" + conditional.Condition;
        }
        if (property.GetCustomAttribute<JsonConverterAttribute>() is { } converter)
        {
            signature += "@" + converter.ConverterType;
        }
        NullabilityInfo nullable = new NullabilityInfoContext().Create(property);
        return signature + "#" + NullabilitySignature(nullable);
    }

    private static string NullabilitySignature(NullabilityInfo nullable)
    {
        string result = nullable.ReadState + "/" + nullable.WriteState;
        if (nullable.ElementType is { } element)
        {
            result += "[" + NullabilitySignature(element) + "]";
        }
        if (nullable.GenericTypeArguments.Length != 0)
        {
            result += "<" + string.Join(',', nullable.GenericTypeArguments.Select(NullabilitySignature)) + ">";
        }
        return result;
    }

    private sealed class IncludedField
    {
        [JsonInclude]
        public string Value = "";
    }
}
