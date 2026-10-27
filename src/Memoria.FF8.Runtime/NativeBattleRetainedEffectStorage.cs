using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

internal sealed class NativeBattleRetainedStorageAuthority
{
}

internal sealed class NativeBattleRetainedAuthorityReceipt
{
}

/// <summary>Metadata-only admission at cold ownership and explicit restore boundaries.</summary>
internal static class NativeBattleRetainedEffectStorage
{
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> fields = new();

    internal static NativeBattleEffectMemory Admit(NativeBattleEffectMemory memory, NativeBattleRetainedStorageAuthority? authority = null)
    {
        if (memory.RetainedAuthority is not null)
        {
            return memory;
        }
        authority ??= new();
        AdmitStorage(memory, authority);
        memory.RetainedAuthority = authority;
        return memory;
    }

    internal static NativeBattleOriginalEffectState AdmitEffect(NativeBattleOriginalEffectState effect, NativeBattleRetainedStorageAuthority? authority = null)
    {
        if (effect.RetainedAuthority is not null)
        {
            return effect;
        }
        authority ??= new();
        List<object> targets = [];
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        foreach (FieldInfo field in Fields(effect.GetType()))
        {
            if (field.FieldType != typeof(NativeBattleEffectBinding) && field.FieldType != typeof(System.Collections.Immutable.ImmutableArray<NativeBattleEffectTaskPool>) && field.FieldType != typeof(NativeBattleRetainedStorageAuthority))
            {
                Visit(field.GetValue(effect), authority, visited, targets, true);
            }
        }
        ApplyTargets(targets, authority);
        effect.RetainedAuthority = authority;
        return effect;
    }

    internal static void AdmitStorage(object value, NativeBattleRetainedStorageAuthority authority, bool preservePhysicalAuthority = false)
    {
        List<object> targets = [];
        Visit(value, authority, new(ReferenceEqualityComparer.Instance), targets, preservePhysicalAuthority);
        ApplyTargets(targets, authority);
    }

    private static void ApplyTargets(List<object> targets, NativeBattleRetainedStorageAuthority authority)
    {
        foreach (object target in targets)
        {
            if (target is NativeBattleEffectArena arena)
            {
                arena.AdmitRetained(authority);
            }
            else
            {
                ((NativeBattleWorkingMemory)target).AdmitRetained(authority);
            }
        }
    }

    private static void Visit(object? value, NativeBattleRetainedStorageAuthority authority, HashSet<object> visited, List<object> targets, bool preservePhysicalAuthority)
    {
        if (value is null || value is NativeBattleRetainedStorageAuthority || !visited.Add(value))
        {
            return;
        }
        if (value is NativeBattleEffectArena arena)
        {
            if (preservePhysicalAuthority && arena.HasRetainedAuthority)
            {
                return;
            }
            arena.ValidateRetainedAdmission(authority);
            targets.Add(arena);
            return;
        }
        if (value is NativeBattleWorkingMemory kernel)
        {
            if (preservePhysicalAuthority && kernel.RetainedAuthority is not null)
            {
                return;
            }
            kernel.ValidateRetainedAdmission(authority);
            targets.Add(kernel);
            return;
        }
        // Task banks and temporal staging have their own publication and ownership journals.
        if (value is NativeBattleEffectTaskPool or NativeBattleModulePoolTask or NativeBattleTaskBankOwner or NativeBattleFileStaging or NativeBattleOriginalEffectState or NativeBattleEffectBinding || value is INativeBattleStorageNode { IsSnapshot: true })
        {
            return;
        }
        Type type = value.GetType();
        if (IsLeaf(type) || value is Delegate || type.IsArray && IsLeaf(type.GetElementType()!))
        {
            return;
        }
        if (value is IEnumerable collection)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Immutable.ImmutableArray<>) && (bool)type.GetProperty("IsDefault")!.GetValue(value)!)
            {
                return;
            }
            if (type.IsGenericType && type.GenericTypeArguments.All(IsLeaf))
            {
                return;
            }
            foreach (object? item in collection)
            {
                Visit(item, authority, visited, targets, preservePhysicalAuthority);
            }
            return;
        }
        if (type.Namespace != typeof(NativeBattleEffectMemory).Namespace && !type.IsValueType)
        {
            return;
        }
        foreach (FieldInfo field in Fields(type))
        {
            Visit(field.GetValue(value), authority, visited, targets, preservePhysicalAuthority);
        }
    }

    private static FieldInfo[] Fields(Type type) => fields.GetOrAdd(type, static type => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(field => !IsLeaf(field.FieldType)).ToArray());

    private static bool IsLeaf(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Type) || typeof(Delegate).IsAssignableFrom(type);
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore]
    internal NativeBattleRetainedStorageAuthority? RetainedAuthority { get; set; }
    [JsonIgnore]
    internal NativeBattleRetainedAuthorityReceipt? RetainedReceipt { get; set; }
}

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore]
    internal NativeBattleRetainedStorageAuthority? RetainedAuthority { get; set; }
    [JsonIgnore]
    internal NativeBattleRetainedAuthorityReceipt? RetainedReceipt { get; set; }
}

public partial record NativeBattleEffectArena
{
    private NativeBattleRetainedStorageAuthority? retainedAuthority;

    internal bool HasRetainedAuthority => retainedAuthority is not null || working?.RetainedAuthority is not null;

    internal void AdmitRetained(NativeBattleRetainedStorageAuthority authority)
    {
        if (snapshot || working?.IsPrivateTaskAuthority == true)
        {
            return;
        }
        ValidateRetainedAdmission(authority);
        working?.AdmitRetained(authority);
        retainedAuthority = authority;
    }

    internal void ValidateRetainedAdmission(NativeBattleRetainedStorageAuthority authority)
    {
        if (snapshot || working?.IsPrivateTaskAuthority == true)
        {
            return;
        }
        if (retainedAuthority is not null && !ReferenceEquals(retainedAuthority, authority))
        {
            throw new InvalidOperationException("Native retained arena belongs to another effect memory authority.");
        }
        working?.ValidateRetainedAdmission(authority);
    }

    private NativeBattleWorkingMemory RetainedWorking()
    {
        working ??= Hydrate();
        if (retainedAuthority is not null)
        {
            working.AdmitRetained(retainedAuthority);
        }
        return working;
    }
}

internal sealed partial class NativeBattleWorkingMemory
{
    internal NativeBattleRetainedStorageAuthority? RetainedAuthority { get; private set; }
    internal bool IsPrivateTaskAuthority { get; set; }

    internal void AdmitRetained(NativeBattleRetainedStorageAuthority authority)
    {
        if (IsPrivateTaskAuthority)
        {
            return;
        }
        ValidateRetainedAdmission(authority);
        RetainedAuthority = authority;
    }

    internal void ValidateRetainedAdmission(NativeBattleRetainedStorageAuthority authority)
    {
        if (IsPrivateTaskAuthority)
        {
            return;
        }
        if (RetainedAuthority is not null && !ReferenceEquals(RetainedAuthority, authority))
        {
            throw new InvalidOperationException("Native retained kernel belongs to another effect memory authority.");
        }
    }
}
