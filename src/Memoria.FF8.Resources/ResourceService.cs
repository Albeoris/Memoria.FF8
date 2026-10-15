using System.Collections.Immutable;

namespace Memoria.FF8.Resources;

/// <summary>The sole encoded-resource and decoded-texture entry point. The host owns publication timing.</summary>
public sealed class ResourceService : IResourceChangeFeed
{
    private sealed record Publication(long Generation, ImmutableDictionary<TextureKey, TextureValue> Values);
    private readonly IResourceSource source;
    private readonly Dictionary<TextureKey, TextureBuildContext> contexts = [];
    private readonly Dictionary<TextureKey, AssetHandle<TextureValue>> handles = [];
    private readonly Dictionary<TextureKey, int> owners = [];
    private readonly List<Action<ResourceChangeSet>> subscribers = [];
    private Publication published = new(0, ImmutableDictionary<TextureKey, TextureValue>.Empty);
    private ITextureReplacementResolver? resolver;
    private bool notifying;
    private long mutationVersion;

    public ResourceService(IResourceSource source)
    {
        this.source = source;
    }

    public ResourceSourceDescriptor Source => source.Descriptor;
    public long Generation => Volatile.Read(ref published).Generation;
    public int ResidentTextureCount => handles.Count;
    public event Action<TextureKey, Exception>? ReplacementFailed;

    public ResourceContent Get(ResourceKey key) => source.Find(key) ?? throw new FileNotFoundException($"Resource {key} is unavailable from {Source.Id}.");

    public byte[] Read(ResourceKey key)
    {
        using Stream stream = Get(key).OpenRead();
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    public AssetHandle<TextureValue> GetTexture(TextureBuildContext context, bool retain = false)
    {
        if (notifying)
        {
            throw new InvalidOperationException("Resource mutations must be queued until after change notification.");
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
        contexts[context.Key] = context;
        mutationVersion++;
        Publication current = published;
        Volatile.Write(ref published, current with { Generation = exists ? current.Generation + 1 : current.Generation, Values = current.Values.SetItem(context.Key, value) });
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
        return handle;
    }

    /// <summary>Builds every resident binding before replacing the generation, including prior no-matches.</summary>
    public PreparedResources Prepare(ITextureReplacementResolver? nextResolver)
    {
        long version = mutationVersion;
        ImmutableDictionary<TextureKey, TextureValue>.Builder next = ImmutableDictionary.CreateBuilder<TextureKey, TextureValue>();
        foreach ((TextureKey key, TextureBuildContext context) in contexts.ToArray())
        {
            next.Add(key, Resolve(context, nextResolver));
        }
        return new PreparedResources(this, version, nextResolver, next.ToImmutable());
    }

    public void Release(IEnumerable<TextureKey> keys)
    {
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
            contexts.Remove(key);
            handles.Remove(key);
            owners.Remove(key);
            Volatile.Write(ref published, published with { Values = published.Values.Remove(key) });
            mutationVersion++;
        }
    }

    public IDisposable Subscribe(Action<ResourceChangeSet> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        subscribers.Add(callback);
        return new Subscription(() => subscribers.Remove(callback));
    }

    private static TextureValue Resolve(TextureBuildContext context, ITextureReplacementResolver? replacementResolver)
    {
        TextureReplacement? replacement = replacementResolver?.Resolve(context);
        return replacement is null ? new TextureValue(context.Original, context.LogicalWidth, context.LogicalHeight, context.Source.Id) : new TextureValue(replacement.Image, replacement.LogicalWidth, replacement.LogicalHeight, replacement.Provenance, replacement.MipLevels);
    }

    public sealed class PreparedResources
    {
        private readonly ResourceService owner;
        private readonly long expectedVersion;
        private readonly ITextureReplacementResolver? resolver;
        private readonly ImmutableDictionary<TextureKey, TextureValue> values;
        private bool committed;
        private ResourceChangeSet? change;

        internal PreparedResources(ResourceService owner, long expectedVersion, ITextureReplacementResolver? resolver, ImmutableDictionary<TextureKey, TextureValue> values)
        {
            this.owner = owner;
            this.expectedVersion = expectedVersion;
            this.resolver = resolver;
            this.values = values;
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
            Volatile.Write(ref owner.published, new Publication(previous.Generation + 1, previous.Values.SetItems(values)));
            change = new ResourceChangeSet(previous.Generation, previous.Generation + 1, values.Keys.ToImmutableArray(), reason);
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
            owner.notifying = true;
            try
            {
                foreach (Action<ResourceChangeSet> callback in owner.subscribers.ToArray())
                {
                    try
                    {
                        callback(notification);
                    }
                    catch
                    {
                        // A consumer cannot roll back a committed resource group or starve other consumers.
                    }
                }
            }
            finally
            {
                owner.notifying = false;
            }
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? action = unsubscribe;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}
