using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime;

internal interface INativeBattleStorageNode
{
    bool IsSnapshot { get; }
    object CaptureDetached(NativeBattleStorageCaptureContext context);
    object RestoreOwned(NativeBattleStorageRestoreContext context);
}

internal enum NativeBattleStorageRestorePurpose
{
    PrivateContinuation,
    NewBattle
}

internal static class NativeBattleStorageSnapshots
{
    public static T Capture<T>(T value)
    {
        NativeBattleStorageCaptureContext context = new();
        T result = context.Map(value);
        if (result is BattleSnapshot snapshot)
        {
            return (T)(object)context.AttachStorageGraph(snapshot);
        }
        return result is NativeBattleRetainedMemory retained ? (T)(object)context.AttachStorageGraph(retained) : result;
    }

    public static T Restore<T>(T value) => Restore(value, NativeBattleStorageRestorePurpose.PrivateContinuation);

    internal static NativeBattleRetainedMemory RestoreForNewBattle(NativeBattleRetainedMemory value) => Restore(value, NativeBattleStorageRestorePurpose.NewBattle);

    private static T Restore<T>(T value, NativeBattleStorageRestorePurpose purpose)
    {
        NativeBattleStorageRestoreContext context = new(purpose);
        if (value is BattleSnapshot snapshot)
        {
            NativeBattleRetainedSourceAliases.Validate(snapshot);
            context.PrepareStorageGraph(snapshot);
        }
        else if (value is NativeBattleRetainedMemory retained)
        {
            NativeBattleRetainedSourceAliases.Validate(retained);
            context.PrepareStorageGraph(retained);
        }
        T result = context.Map(value);
        if (result is BattleSnapshot restored)
        {
            return (T)(object)NativeBattleRetainedSourceAliases.Rebind(restored);
        }
        return result is NativeBattleRetainedMemory restoredRetained ? (T)(object)NativeBattleRetainedSourceAliases.Rebind(restoredRetained) : result;
    }
}

internal sealed partial class NativeBattleStorageCaptureContext : NativeBattleStorageContext
{
    protected override object MapStorage(INativeBattleStorageNode node)
    {
        if (node.IsSnapshot)
        {
            return node;
        }
        object captured = node.CaptureDetached(this);
        if (captured is not INativeBattleStorageNode { IsSnapshot: true })
        {
            throw new InvalidOperationException("Battle storage capture must return detached snapshot storage.");
        }
        return captured;
    }
}

internal sealed partial class NativeBattleStorageRestoreContext : NativeBattleStorageContext
{
    private readonly List<NativeBattleEffectMemory> retainedMemories = [];
    private readonly List<NativeBattleOriginalEffectState> retainedEffects = [];

    internal NativeBattleStorageRestoreContext(NativeBattleStorageRestorePurpose purpose) => Purpose = purpose;
    internal NativeBattleStorageRestorePurpose Purpose { get; }

    internal ImmutableDictionary<TKey, BattleExecutionReferenceEntry> RetainExecutionReferences<TKey>(ImmutableDictionary<TKey, BattleExecutionReferenceEntry> references) where TKey : notnull
        => Purpose == NativeBattleStorageRestorePurpose.NewBattle ? references.Clear() : references;

    internal NativeBattleWorkingMemory RestoreWorkingMemory(NativeBattleWorkingMemorySnapshot snapshot)
    {
        var references = RetainExecutionReferences(snapshot.ExecutionReferences);
        return NativeBattleWorkingMemory.Restore(ReferenceEquals(references, snapshot.ExecutionReferences) ? snapshot : snapshot with { ExecutionReferences = references });
    }

    internal void RegisterRetained(NativeBattleEffectMemory memory) => retainedMemories.Add(memory);
    internal void RegisterRetained(NativeBattleOriginalEffectState effect) => retainedEffects.Add(effect);

    protected override void CompleteMapping()
    {
        foreach (NativeBattleEffectMemory memory in retainedMemories)
        {
            NativeBattleRetainedStorageAuthority authority = GetOrAddOwner(memory.RetainedReceipt ?? (object)memory, static () => new NativeBattleRetainedStorageAuthority());
            NativeBattleRetainedEffectStorage.Admit(memory, authority);
            memory.RetainedReceipt = null;
        }
        foreach (NativeBattleOriginalEffectState effect in retainedEffects)
        {
            NativeBattleRetainedStorageAuthority authority = GetOrAddOwner(effect.RetainedReceipt ?? (object)effect, static () => new NativeBattleRetainedStorageAuthority());
            NativeBattleRetainedEffectStorage.AdmitEffect(effect, authority);
            effect.RetainedReceipt = null;
        }
        retainedMemories.Clear();
        retainedEffects.Clear();
    }

    protected override object MapStorage(INativeBattleStorageNode node)
    {
        if (!node.IsSnapshot)
        {
            throw new InvalidOperationException("Battle storage restore requires detached snapshot storage.");
        }
        object restored = node is NativeBattleEffectArena arena && TryRestoreGraphView(arena, out NativeBattleEffectArena? view) ? view! : node.RestoreOwned(this);
        if (restored is not INativeBattleStorageNode { IsSnapshot: false })
        {
            throw new InvalidOperationException("Battle storage restore must create owned working storage.");
        }
        return restored;
    }
}

/// <summary>Explicit persistence/isolation graph mapping; ordinary execution never calls this utility.</summary>
internal abstract class NativeBattleStorageContext
{
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> Fields = new();
    private static readonly MethodInfo Clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo ImmutableArrayMapper = typeof(NativeBattleStorageContext).GetMethod(nameof(MapImmutableArray), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo ImmutableDictionaryMapper = typeof(NativeBattleStorageContext).GetMethod(nameof(MapImmutableDictionary), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private readonly Dictionary<object, object> nodes = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, object> owners = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> mapping = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> creatingOwners = new(ReferenceEqualityComparer.Instance);

    private int mapDepth;

    public T Map<T>(T value)
    {
        mapDepth++;
        try
        {
            T result = (T)RequireAssignable(MapObject(value), typeof(T))!;
            if (mapDepth == 1)
            {
                CompleteMapping();
            }
            return result;
        }
        finally
        {
            mapDepth--;
        }
    }

    protected virtual void CompleteMapping()
    {
    }

    public T GetOrAddOwner<T>(object backingOwner, Func<T> create) where T : class
    {
        ArgumentNullException.ThrowIfNull(backingOwner);
        ArgumentNullException.ThrowIfNull(create);
        if (owners.TryGetValue(backingOwner, out object? existing))
        {
            return (T)RequireAssignable(existing, typeof(T))!;
        }
        if (!creatingOwners.Add(backingOwner))
        {
            throw new InvalidOperationException("Battle storage ownership construction cannot recursively acquire the same authority.");
        }
        try
        {
            T owner = create() ?? throw new InvalidOperationException("Battle storage ownership construction returned no owner.");
            owners.Add(backingOwner, owner);
            return owner;
        }
        finally
        {
            creatingOwners.Remove(backingOwner);
        }
    }

    protected abstract object MapStorage(INativeBattleStorageNode node);

    private object? MapObject(object? value)
    {
        if (value is null)
        {
            return null;
        }
        Type type = value.GetType();
        if (IsLeaf(type))
        {
            return value;
        }
        if (nodes.TryGetValue(value, out object? existing))
        {
            return existing;
        }
        if (!mapping.Add(value))
        {
            throw new InvalidOperationException("Cyclic battle storage graphs require an explicit ownership boundary.");
        }
        try
        {
            object result;
            if (value is INativeBattleStorageNode node)
            {
                result = MapStorage(node);
            }
            else if (value is Array array)
            {
                result = MapArray(array);
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
            {
                result = InvokeMapper(ImmutableArrayMapper, type.GenericTypeArguments, value);
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>))
            {
                result = InvokeMapper(ImmutableDictionaryMapper, type.GenericTypeArguments, value);
            }
            else if (IsRecord(type))
            {
                result = MapRecord(value, type);
            }
            else if (ContainsStorage(value, new(ReferenceEqualityComparer.Instance)))
            {
                throw new NotSupportedException($"Battle storage capture/restore does not support container {type.FullName}.");
            }
            else
            {
                result = value;
            }
            nodes.Add(value, result);
            return result;
        }
        finally
        {
            mapping.Remove(value);
        }
    }

    private object MapArray(Array source)
    {
        Array result = (Array)source.Clone();
        Type elementType = source.GetType().GetElementType()!;
        if (IsLeaf(elementType))
        {
            return result;
        }
        int[] indices = new int[source.Rank];
        for (int flat = 0; flat < source.Length; flat++)
        {
            int remaining = flat;
            for (int dimension = source.Rank - 1; dimension >= 0; dimension--)
            {
                indices[dimension] = source.GetLowerBound(dimension) + remaining % source.GetLength(dimension);
                remaining /= source.GetLength(dimension);
            }
            object? original = source.GetValue(indices);
            object? mapped = RequireAssignable(MapObject(original), elementType);
            if (!ReferenceEquals(original, mapped))
            {
                result.SetValue(mapped, indices);
            }
        }
        return result;
    }

    private object MapRecord(object source, Type type)
    {
        object? result = null;
        foreach (FieldInfo field in GetFields(type))
        {
            object? original = field.GetValue(source);
            object? mapped = field.FieldType == typeof(NativeBattleRetainedStorageAuthority) ? null : RequireAssignable(MapObject(original), field.FieldType);
            if (!ReferenceEquals(original, mapped))
            {
                result ??= Clone.Invoke(source, null)!;
                field.SetValue(result, mapped);
            }
        }
        if (source is NativeBattleEffectMemory)
        {
            result ??= Clone.Invoke(source, null)!;
            NativeBattleEffectMemory memory = (NativeBattleEffectMemory)result;
            memory.RetainedAuthority = null;
            NativeBattleEffectMemory originalMemory = (NativeBattleEffectMemory)source;
            memory.RetainedReceipt = this is NativeBattleStorageCaptureContext && originalMemory.RetainedAuthority is { } authority ? GetOrAddOwner(authority, static () => new NativeBattleRetainedAuthorityReceipt()) : originalMemory.RetainedReceipt;
            if (this is NativeBattleStorageRestoreContext restore)
            {
                restore.RegisterRetained(memory);
            }
        }
        if (source is NativeBattleOriginalEffectState)
        {
            result ??= Clone.Invoke(source, null)!;
            NativeBattleOriginalEffectState effect = (NativeBattleOriginalEffectState)result;
            if (this is NativeBattleStorageRestoreContext { Purpose: NativeBattleStorageRestorePurpose.NewBattle })
            {
                effect = effect with { Parameters = null, EntryFirstRecipient = null, FireModelCompletion = null, SavedActorTransforms = effect.SavedActorTransforms.Clear(), NoMercyBusyTargets = effect.NoMercyBusyTargets.Clear(), UltimaSideFadeOwners = ImmutableHashSet<BattleModelOwner>.Empty, TornadoLiftRows = null, Effect312SavedTransforms = effect.Effect312SavedTransforms.Clear(), Effect301SavedPartyFlags = effect.Effect301SavedPartyFlags.Clear(), ZantetsukenTransforms = effect.ZantetsukenTransforms.Clear(), TheEndSavedPartyFlags = effect.TheEndSavedPartyFlags.Clear(), ShockwaveSavedFlags = effect.ShockwaveSavedFlags.Clear(), MoogleSavedEnemyFlags = effect.MoogleSavedEnemyFlags.Clear(), SirenSavedEnemyFlags = effect.SirenSavedEnemyFlags.Clear(), Rinoa88SavedFlags = effect.Rinoa88SavedFlags.Clear(), Rinoa89SavedFlags = effect.Rinoa89SavedFlags.Clear(), Duel212SavedFlags = effect.Duel212SavedFlags.Clear(), LionHeartSavedModels = effect.LionHeartSavedModels.Clear(), Duel274SavedFlags = effect.Duel274SavedFlags.Clear(), Duel274SavedPartyZ = effect.Duel274SavedPartyZ.Clear(), ScanSelectedModel = null, ScanSelectedTargets = BattleModelTargets.Empty, ScanSavedModels = effect.ScanSavedModels.Clear() };
                result = effect;
            }
            effect.RetainedAuthority = null;
            NativeBattleOriginalEffectState originalEffect = (NativeBattleOriginalEffectState)source;
            effect.RetainedReceipt = this is NativeBattleStorageCaptureContext && originalEffect.RetainedAuthority is { } authority ? GetOrAddOwner(authority, static () => new NativeBattleRetainedAuthorityReceipt()) : originalEffect.RetainedReceipt;
            if (this is NativeBattleStorageRestoreContext restore)
            {
                restore.RegisterRetained(effect);
            }
        }
        if (this is NativeBattleStorageRestoreContext { Purpose: NativeBattleStorageRestorePurpose.NewBattle })
        {
            if (source is NativeBattleModuleState)
            {
                return ((NativeBattleModuleState)(result ?? source)).ClearRinoaPhysicalHitStorage();
            }
            if (source is NativeBattleDescriptorPacket)
            {
                NativeBattleDescriptorPacket packet = (NativeBattleDescriptorPacket)(result ?? source);
                return packet with { ModelSubject = null, Header = null };
            }
            if (source is NativeBattleDescriptorTask)
            {
                NativeBattleDescriptorTask task = (NativeBattleDescriptorTask)(result ?? source);
                return task with { ModelSubject = null, Header = null };
            }
        }
        if (source is NativeBattleScanResultMemory && this is NativeBattleStorageRestoreContext { Purpose: NativeBattleStorageRestorePurpose.NewBattle })
        {
            NativeBattleScanResultMemory results = (NativeBattleScanResultMemory)(result ?? source);
            return results.ExtraTextBindings.IsEmpty ? results : results with { ExtraTextBindings = [] };
        }
        if (source is NativeBattleScriptedEffectMemory && this is NativeBattleStorageRestoreContext { Purpose: NativeBattleStorageRestorePurpose.NewBattle })
        {
            NativeBattleScriptedEffectMemory scripted = (NativeBattleScriptedEffectMemory)(result ?? source);
            return scripted.PresentationActors is null ? scripted : scripted with { PresentationActors = null };
        }
        if (source is NativeBattleCameraState && this is NativeBattleStorageRestoreContext { Purpose: NativeBattleStorageRestorePurpose.NewBattle })
        {
            NativeBattleCameraState camera = (NativeBattleCameraState)(result ?? source);
            return camera.ControlValues.IsEmpty ? camera : camera with { ControlValues = camera.ControlValues.Clear() };
        }
        if (source is NativeBattleSequenceState && this is NativeBattleStorageRestoreContext { Purpose: NativeBattleStorageRestorePurpose.NewBattle })
        {
            NativeBattleSequenceState sequence = (NativeBattleSequenceState)(result ?? source);
            return sequence.ScriptWords.IsEmpty ? sequence : sequence with { ScriptWords = sequence.ScriptWords.Clear() };
        }
        if (source is NativeBattleCameraAliases && this is NativeBattleStorageRestoreContext { Purpose: NativeBattleStorageRestorePurpose.NewBattle })
        {
            NativeBattleCameraAliases aliases = (NativeBattleCameraAliases)(result ?? source);
            return aliases.Source is null && aliases.Target is null ? aliases : aliases with { Source = null, Target = null };
        }
        if (source is NativeBattleAuxiliaryTask && this is NativeBattleStorageRestoreContext { Purpose: NativeBattleStorageRestorePurpose.NewBattle })
        {
            NativeBattleAuxiliaryTask task = (NativeBattleAuxiliaryTask)(result ?? source);
            return task.ModelOwner is null && task.TargetOwner is null ? task : task with { ModelOwner = null, TargetOwner = null };
        }
        if (this is NativeBattleStorageRestoreContext { Purpose: NativeBattleStorageRestorePurpose.NewBattle })
        {
            switch (result ?? source)
            {
                case NativeBattleModuleTask task:
                    return task.ModelOwner is null ? task : task with { ModelOwner = null };
                case NativeBattleModelLoader loader:
                    return loader.Task is null && loader.PartyPlan is null ? loader : loader with { Task = null, PartyPlan = null };
                case NativeBattleModelCpuBanks banks:
                    return banks with { PartyBankOwners = banks.PartyBankOwners.Clear(), PresentationRoster = null };
                case NativeBattleModelCpuResources cpu:
                    return cpu with { PresentationPalettes = null, OwnedAnimationSources = cpu.OwnedAnimationSources.Clear() };
                case NativeBattleModelTextureCache cache:
                    return cache with { PresentationActors = null, PresentationRoster = null };
            }
        }
        return result ?? source;
    }

    private object MapImmutableArray<T>(object boxed)
    {
        ImmutableArray<T> source = (ImmutableArray<T>)boxed;
        if (source.IsDefault || IsLeaf(typeof(T)))
        {
            return boxed;
        }
        ImmutableArray<T>.Builder? result = null;
        for (int index = 0; index < source.Length; index++)
        {
            object? original = source[index];
            object? mapped = RequireAssignable(MapObject(original), typeof(T));
            if (!ReferenceEquals(original, mapped))
            {
                result ??= source.ToBuilder();
                result[index] = (T)mapped!;
            }
        }
        return result is null ? boxed : result.ToImmutable();
    }

    private object MapImmutableDictionary<TKey, TValue>(ImmutableDictionary<TKey, TValue> source) where TKey : notnull
    {
        ImmutableDictionary<TKey, TValue>.Builder? result = null;
        foreach ((TKey key, TValue value) in source)
        {
            object? originalKey = key;
            object? originalValue = value;
            object? mappedKey = RequireAssignable(MapObject(originalKey), typeof(TKey));
            object? mappedValue = RequireAssignable(MapObject(originalValue), typeof(TValue));
            if (!ReferenceEquals(originalKey, mappedKey) || !ReferenceEquals(originalValue, mappedValue))
            {
                result ??= source.ToBuilder();
                result.Remove(key);
                result.Add((TKey)mappedKey!, (TValue)mappedValue!);
            }
        }
        return result is null ? source : result.ToImmutable();
    }

    private object InvokeMapper(MethodInfo method, Type[] arguments, object source)
    {
        try
        {
            return method.MakeGenericMethod(arguments).Invoke(this, [source])!;
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static object? RequireAssignable(object? value, Type declaredType)
    {
        if (value is null ? declaredType.IsValueType && Nullable.GetUnderlyingType(declaredType) is null : !declaredType.IsInstanceOfType(value))
        {
            throw new InvalidOperationException($"Battle storage mapping is incompatible with declared type {declaredType.FullName}.");
        }
        return value;
    }

    private static bool IsLeaf(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid) || typeof(Type).IsAssignableFrom(type);

    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public) is not null || type.IsValueType && type.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(System.Text.StringBuilder)]) is not null;

    private static FieldInfo[] GetFields(Type type) => Fields.GetOrAdd(type, static current =>
    {
        List<FieldInfo> fields = [];
        for (Type? ancestor = current; ancestor is not null && ancestor != typeof(object); ancestor = ancestor.BaseType)
        {
            fields.AddRange(ancestor.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        }
        return fields.ToArray();
    });

    private static bool ContainsStorage(object? value, HashSet<object> visited)
    {
        if (value is null || IsLeaf(value.GetType()) || !visited.Add(value))
        {
            return false;
        }
        if (value is INativeBattleStorageNode)
        {
            return true;
        }
        if (value is IEnumerable sequence)
        {
            foreach (object? item in sequence)
            {
                if (ContainsStorage(item, visited))
                {
                    return true;
                }
            }
            return false;
        }
        return GetFields(value.GetType()).Any(field => ContainsStorage(field.GetValue(value), visited));
    }
}
