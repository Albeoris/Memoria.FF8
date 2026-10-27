using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleStorageGraphViews
{
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> Fields = new();

    internal static IEnumerable<NativeBattleEffectArena> Enumerate(object graph)
    {
        List<NativeBattleEffectArena> result = [];
        Visit(graph, result, new HashSet<object>(ReferenceEqualityComparer.Instance));
        return result;
    }

    private static void Visit(object? value, List<NativeBattleEffectArena> result, HashSet<object> path)
    {
        if (value is null)
        {
            return;
        }
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string || value is decimal || value is Type || value is NativeBattleStorageGraph)
        {
            return;
        }
        if (value is NativeBattleEffectArena arena)
        {
            result.Add(arena);
            return;
        }
        if (value is INativeBattleArenaStorageNode carrier)
        {
            if (carrier.SnapshotArena is { } storage)
            {
                result.Add(storage);
            }
            return;
        }
        if (value is INativeBattleStorageNode)
        {
            return;
        }
        if (!type.IsValueType && !path.Add(value))
        {
            throw new InvalidOperationException("Serialized battle ownership graphs cannot contain cycles.");
        }
        try
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>) && (bool)type.GetProperty("IsDefault")!.GetValue(value)!)
            {
                return;
            }
            if (value is IEnumerable sequence)
            {
                Type? element = type.IsArray ? type.GetElementType() : type.IsGenericType && type.GenericTypeArguments.Length == 1 ? type.GenericTypeArguments[0] : null;
                if (element is not null && (element.IsPrimitive || element.IsEnum))
                {
                    return;
                }
                foreach (object? item in sequence)
                {
                    Visit(item, result, path);
                }
                return;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                Visit(type.GetProperty("Value")!.GetValue(value), result, path);
                return;
            }
            foreach (FieldInfo field in Fields.GetOrAdd(type, FindSerializedFields))
            {
                if (value is BattleSnapshot && field.Name == "<Definition>k__BackingField")
                {
                    continue;
                }
                Visit(field.GetValue(value), result, path);
            }
        }
        finally
        {
            if (!type.IsValueType)
            {
                path.Remove(value);
            }
        }
    }

    private static FieldInfo[] FindSerializedFields(Type type)
    {
        List<FieldInfo> fields = [];
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!field.Name.StartsWith('<') || !field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal))
                {
                    continue;
                }
                string name = field.Name[1..field.Name.IndexOf('>')];
                PropertyInfo? property = current.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (property?.GetMethod is null || property.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always })
                {
                    continue;
                }
                fields.Add(field);
            }
        }
        return fields.ToArray();
    }
}

internal sealed partial class NativeBattleStorageCaptureContext
{
    private readonly HashSet<NativeBattleEffectArena> capturedArenas = new(ReferenceEqualityComparer.Instance);

    internal void RegisterCapturedArena(NativeBattleEffectArena arena) => capturedArenas.Add(arena);

    internal BattleSnapshot AttachStorageGraph(BattleSnapshot snapshot) => capturedArenas.Count == 0 ? snapshot : snapshot with { StorageGraph = BuildStorageGraph(snapshot) };
    internal NativeBattleRetainedMemory AttachStorageGraph(NativeBattleRetainedMemory snapshot) => capturedArenas.Count == 0 ? snapshot : snapshot with { StorageGraph = BuildStorageGraph(snapshot) };

    private NativeBattleStorageGraph? BuildStorageGraph(object snapshot)
    {
        Dictionary<NativeBattleWorkingMemorySnapshot, List<NativeBattleEffectArena>> groups = new(ReferenceEqualityComparer.Instance);
        foreach (NativeBattleEffectArena arena in NativeBattleStorageGraphViews.Enumerate(snapshot))
        {
            if (!capturedArenas.Contains(arena) || arena.CapturedAuthority is not { } owner)
            {
                continue;
            }
            if (!groups.TryGetValue(owner, out List<NativeBattleEffectArena>? views))
            {
                views = [];
                groups.Add(owner, views);
            }
            views.Add(arena);
        }
        ImmutableArray<NativeBattleStorageAuthority>.Builder authorities = ImmutableArray.CreateBuilder<NativeBattleStorageAuthority>();
        foreach ((NativeBattleWorkingMemorySnapshot owner, List<NativeBattleEffectArena> views) in groups)
        {
            if (views.Count < 2)
            {
                continue;
            }
            int id = authorities.Count + 1;
            NativeBattleEffectArena? definition = views.FirstOrDefault(view => view.CapturedOffset == 0 && view.LogicalLength == owner.LogicalLength && !view.CapturedBorrowed);
            NativeBattleStorageAuthority authority = new(id, owner.LogicalLength, owner.InitialKnownZero, owner.StoredLength, owner.DataFootprint, owner.ClassifiedRanges);
            if (definition is null)
            {
                authority = authority with { Content = new(owner.Regions, owner.UnknownRegions, owner.ResourcePointers) };
            }
            authorities.Add(authority);
            foreach (NativeBattleEffectArena view in views)
            {
                view.AttachStorageReference(new(id, view.CapturedOffset, view.LogicalLength, ReferenceEquals(view, definition), view.CapturedBorrowed));
            }
        }
        return authorities.Count == 0 ? null : new(authorities.ToImmutable());
    }
}
