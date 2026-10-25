using System.Collections.Immutable;

namespace Memoria.FF8.Resources;

/// <summary>The sole encoded-resource and decoded-texture entry point. The host owns publication timing.</summary>
public sealed class ResourceService : IResourceChangeFeed
{
    private sealed record Publication(long Generation, ImmutableDictionary<TextureKey, TextureValue> Values, ImmutableDictionary<ResourceKey, StaticTextResource> Texts);
    private readonly IResourceSource source;
    private readonly Dictionary<TextureKey, TextureBuildContext> contexts = [];
    private readonly Dictionary<TextureKey, TextureTransformContext> transforms = [];
    private readonly Dictionary<TextureKey, AssetHandle<TextureValue>> handles = [];
    private readonly Dictionary<TextureKey, int> owners = [];
    private readonly List<Action<ResourceChangeSet>> subscribers = [];
    private Publication published = new(0, ImmutableDictionary<TextureKey, TextureValue>.Empty, ImmutableDictionary<ResourceKey, StaticTextResource>.Empty);
    private ITextureReplacementResolver? resolver;
    private bool notifying;
    private long mutationVersion;

    public ResourceService(IResourceSource source)
    {
        this.source = source;
    }

    public ResourceSourceDescriptor Source => source.Descriptor;
    public T? GetSource<T>() where T : class => source as T;
    public long Generation => Volatile.Read(ref published).Generation;
    public int ResidentTextureCount => handles.Count;
    public event Action<TextureKey, Exception>? ReplacementFailed;

    public StaticTextResource? FindText(ResourceKey key) => Volatile.Read(ref published).Texts.GetValueOrDefault(key);

    public string? ReadText(ResourceKey key, int index, string? original = null) => FindText(key)?.At(index) ?? original;

    public ResourceContent Get(ResourceKey key) => source.Find(key) ?? throw new FileNotFoundException($"Resource {key} is unavailable from {Source.Id}.");

    public ResourcePrefix ReadPrefix(ResourceKey key, int maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        if (source is IResourcePrefixSource prefixSource && prefixSource.ReadPrefix(key, maximumBytes) is ResourcePrefix prefix)
        {
            if (prefix.Bytes is null || prefix.Bytes.Length > maximumBytes || prefix.Length is long length && (length < 0 || prefix.Bytes.Length > length))
            {
                throw new InvalidDataException("Resource prefix differs from its declared bound or length.");
            }
            return prefix;
        }
        using Stream stream = Get(key).OpenRead();
        long? size = stream.CanSeek ? stream.Length - stream.Position : null;
        using MemoryStream bytes = new();
        byte[] buffer = new byte[Math.Min(8192, maximumBytes)];
        while (bytes.Length < maximumBytes)
        {
            int count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, maximumBytes - bytes.Length));
            if (count == 0)
            {
                size ??= bytes.Length;
                break;
            }
            bytes.Write(buffer, 0, count);
        }
        return new ResourcePrefix(bytes.ToArray(), size);
    }

    public byte[] Read(ResourceKey key)
    {
        using Stream stream = Get(key).OpenRead();
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    public AssetHandle<TextureValue> GetTexture(TextureBuildContext context, bool retain = false)
    {
        long expectedVersion = mutationVersion;
        if (notifying)
        {
            throw new InvalidOperationException("Resource mutations must be queued until after change notification.");
        }
        if (transforms.ContainsKey(context.Key))
        {
            throw new InvalidOperationException("A prepared texture cannot also be registered as an original source texture.");
        }
        bool exists = contexts.TryGetValue(context.Key, out TextureBuildContext? previous);
        if (exists && previous!.Revision == context.Revision && previous.Layout == context.Layout)
        {
            if (retain)
            {
                owners[context.Key]++;
            }
            return handles[context.Key];
        }
        TextureValue value;
        try
        {
            value = Resolve(context, resolver);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            value = Resolve(context, null);
            foreach (Action<TextureKey, Exception> callback in ReplacementFailed?.GetInvocationList() ?? [])
            {
                try
                {
                    callback(context.Key, exception);
                }
                catch
                {
                    // Failure reporting cannot prevent the valid source texture from loading.
                }
            }
        }
        Publication current = published;
        ImmutableDictionary<TextureKey, TextureValue>.Builder candidate = current.Values.ToBuilder();
        candidate[context.Key] = value;
        if (exists)
        {
            BuildTransforms(candidate);
        }
        if (mutationVersion != expectedVersion)
        {
            throw new InvalidOperationException("The source or preparation graph changed while building the texture.");
        }
        contexts[context.Key] = context;
        mutationVersion++;
        Volatile.Write(ref published, current with { Generation = exists ? current.Generation + 1 : current.Generation, Values = candidate.ToImmutable() });
        if (!handles.TryGetValue(context.Key, out AssetHandle<TextureValue>? handle))
        {
            TextureKey key = context.Key;
            handle = new AssetHandle<TextureValue>(() => Volatile.Read(ref published).Values[key], () => Volatile.Read(ref published).Generation);
            handles.Add(key, handle);
            owners.Add(key, 1);
        }
        else if (retain)
        {
            owners[context.Key]++;
        }
        if (exists)
        {
            Notify(new ResourceChangeSet(current.Generation, current.Generation + 1, [context.Key, .. transforms.Keys], "Original texture source changed"));
        }
        return handle;
    }

    public AssetHandle<TextureValue> GetTransformedTexture(TextureTransformContext context, bool retain = false)
    {
        if (notifying || contexts.ContainsKey(context.Key))
        {
            throw new InvalidOperationException("Register native texture preparation outside notifications, under its own identity.");
        }
        if (transforms.TryGetValue(context.Key, out TextureTransformContext? previous))
        {
            if (!previous.Dependencies.SequenceEqual(context.Dependencies))
            {
                throw new InvalidOperationException("A prepared texture identity cannot change its source dependencies while resident.");
            }
            if (retain)
            {
                owners[context.Key]++;
            }
            return handles[context.Key];
        }
        long expectedVersion = mutationVersion;
        TextureValue value = context.Build(context.Dependencies.Select(key => published.Values.TryGetValue(key, out TextureValue? dependency) ? dependency : throw new InvalidOperationException($"Missing texture preparation dependency {key}.")).ToArray());
        if (mutationVersion != expectedVersion)
        {
            throw new InvalidOperationException("The source or preparation graph changed while building the derived texture.");
        }
        transforms.Add(context.Key, context);
        foreach (TextureKey dependency in context.Dependencies)
        {
            owners[dependency]++;
        }
        mutationVersion++;
        Volatile.Write(ref published, published with { Values = published.Values.Add(context.Key, value) });
        TextureKey identity = context.Key;
        AssetHandle<TextureValue> handle = new(() => Volatile.Read(ref published).Values[identity], () => Volatile.Read(ref published).Generation);
        handles.Add(context.Key, handle);
        owners.Add(context.Key, 1);
        return handle;
    }

    /// <summary>Builds every resident binding before replacing the generation, including prior no-matches.</summary>
    public PreparedResources Prepare(ITextureReplacementResolver? nextResolver, IEnumerable<StaticTextResource>? textResources = null)
    {
        long version = mutationVersion;
        ImmutableDictionary<TextureKey, TextureValue>.Builder next = ImmutableDictionary.CreateBuilder<TextureKey, TextureValue>();
        foreach ((TextureKey key, TextureBuildContext context) in contexts.ToArray())
        {
            next.Add(key, Resolve(context, nextResolver));
        }
        BuildTransforms(next);
        return new PreparedResources(this, version, nextResolver, next.ToImmutable(), textResources?.ToImmutableDictionary(text => text.Key) ?? published.Texts);
    }

    private void BuildTransforms(ImmutableDictionary<TextureKey, TextureValue>.Builder next)
    {
        Dictionary<TextureKey, TextureTransformContext> preparation = new(transforms);
        foreach (TextureKey key in preparation.Keys)
        {
            next.Remove(key);
        }
        HashSet<TextureKey> visiting = [];
        TextureValue Build(TextureKey key)
        {
            if (next.TryGetValue(key, out TextureValue? value))
            {
                return value;
            }
            if (!preparation.TryGetValue(key, out TextureTransformContext? transform) || !visiting.Add(key))
            {
                throw new InvalidOperationException($"Missing or cyclic texture preparation dependency {key}.");
            }
            value = transform.Build(transform.Dependencies.Select(Build).ToArray());
            visiting.Remove(key);
            next.Add(key, value);
            return value;
        }
        foreach (TextureKey key in preparation.Keys)
        {
            Build(key);
        }
    }

    public void Release(IEnumerable<TextureKey> keys)
    {
        if (notifying)
        {
            throw new InvalidOperationException("Resource mutations must be queued until after change notification.");
        }
        foreach (TextureKey key in keys)
        {
            if (!owners.TryGetValue(key, out int count))
            {
                continue;
            }
            if (count > 1)
            {
                owners[key] = count - 1;
                continue;
            }
            handles[key].Freeze(published.Values[key]);
            TextureTransformContext? preparation = transforms.GetValueOrDefault(key);
            contexts.Remove(key);
            transforms.Remove(key);
            handles.Remove(key);
            owners.Remove(key);
            Volatile.Write(ref published, published with { Values = published.Values.Remove(key) });
            mutationVersion++;
            if (preparation is not null)
            {
                Release(preparation.Dependencies);
            }
        }
    }

    public IDisposable Subscribe(Action<ResourceChangeSet> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        subscribers.Add(callback);
        return new Subscription(() => subscribers.Remove(callback));
    }

    private TextureValue Resolve(TextureBuildContext context, ITextureReplacementResolver? replacementResolver)
    {
        TextureReplacement? replacement = replacementResolver?.Resolve(context) ?? (source as ITextureReplacementResolver)?.Resolve(context);
        return replacement is null ? new TextureValue(context.Original, context.LogicalWidth, context.LogicalHeight, context.Source.Id) : new TextureValue(replacement.Image, replacement.LogicalWidth, replacement.LogicalHeight, replacement.Provenance, replacement.MipLevels, replacement.Variants);
    }

    private void Notify(ResourceChangeSet notification)
    {
        notifying = true;
        try
        {
            foreach (Action<ResourceChangeSet> callback in subscribers.ToArray())
            {
                try
                {
                    callback(notification);
                }
                catch
                {
                    // Consumers cannot roll back a committed group or starve other consumers.
                }
            }
        }
        finally
        {
            notifying = false;
        }
    }

    public sealed class PreparedResources
    {
        private readonly ResourceService owner;
        private readonly long expectedVersion;
        private readonly ITextureReplacementResolver? resolver;
        private readonly ImmutableDictionary<TextureKey, TextureValue> values;
        private readonly ImmutableDictionary<ResourceKey, StaticTextResource> texts;
        private bool committed;
        private ResourceChangeSet? change;

        internal PreparedResources(ResourceService owner, long expectedVersion, ITextureReplacementResolver? resolver, ImmutableDictionary<TextureKey, TextureValue> values, ImmutableDictionary<ResourceKey, StaticTextResource> texts)
        {
            this.owner = owner;
            this.expectedVersion = expectedVersion;
            this.resolver = resolver;
            this.values = values;
            this.texts = texts;
        }

        public void Commit(string reason = "Texture catalog changed", bool notify = true)
        {
            if (committed || owner.notifying || owner.mutationVersion != expectedVersion)
            {
                throw new InvalidOperationException("Discard stale resource preparation and prepare the current resident set again.");
            }
            committed = true;
            owner.mutationVersion++;
            Publication previous = owner.published;
            owner.resolver = resolver;
            Volatile.Write(ref owner.published, new Publication(previous.Generation + 1, previous.Values.SetItems(values), texts));
            change = new ResourceChangeSet(previous.Generation, previous.Generation + 1, values.Keys.ToImmutableArray(), reason) { Texts = previous.Texts.Keys.Union(texts.Keys).ToImmutableArray() };
            if (notify)
            {
                Notify();
            }
        }

        public void Notify()
        {
            if (!committed || change is null)
            {
                return;
            }
            ResourceChangeSet notification = change;
            change = null;
            owner.Notify(notification);
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? action = unsubscribe;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}
