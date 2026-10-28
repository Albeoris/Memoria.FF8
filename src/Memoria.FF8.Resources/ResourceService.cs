using Memoria.FF8.Resources.Localization;
using System.Collections.Immutable;

namespace Memoria.FF8.Resources;

/// <summary>The sole encoded-resource and decoded-texture entry point. The host owns publication timing.</summary>
public sealed class ResourceService : IResourceChangeFeed, IResourceLanguageSource
{
    private sealed record Publication(long Generation, ImmutableDictionary<TextureKey, TextureValue> Values, ImmutableDictionary<ResourceKey, StaticTextResource> Texts, ImmutableDictionary<ResourceKey, EncodedResource> Encoded, ImmutableArray<string> ExclusiveScopes, LocalizationCatalog Localization)
    {
        public long CatalogGeneration { get; init; }
    }
    private readonly IResourceSource source;
    private readonly Dictionary<TextureKey, TextureBuildContext> contexts = [];
    private readonly Dictionary<TextureKey, TextureTransformContext> transforms = [];
    private readonly Dictionary<TextureKey, AssetHandle<TextureValue>> handles = [];
    private readonly Dictionary<TextureKey, int> owners = [];
    private readonly List<Action<ResourceChangeSet>> subscribers = [];
    private readonly object encodedGate = new();
    private readonly HashSet<ResourceKey> consumedResources = [];
    private Publication published = new(0, ImmutableDictionary<TextureKey, TextureValue>.Empty, ImmutableDictionary<ResourceKey, StaticTextResource>.Empty, ImmutableDictionary<ResourceKey, EncodedResource>.Empty, [], LocalizationCatalog.Empty);
    private ITextureReplacementResolver? resolver;
    private bool notifying;
    private long mutationVersion;

    public ResourceService(IResourceSource source, string? resourceLanguage = null)
    {
        this.source = source;
        ResourceLanguage = Memoria.FF8.Platform.GameResourceLanguages.Normalize(resourceLanguage) ?? (source as IResourceLanguageSource)?.ResourceLanguage ?? source.Descriptor.Language;
    }

    public string ResourceLanguage { get; }
    public ResourceSourceDescriptor Source => source.Descriptor;
    public T? GetSource<T>() where T : class => (source as IResourceSourceProvider)?.GetSource(typeof(T), this) as T ?? source as T;
    public bool HasEncodedReplacement(ResourceKey key) => Volatile.Read(ref published).Encoded.ContainsKey(key);
    public LocalizationCatalog Localization => Volatile.Read(ref published).Localization;
    public string Localize(string key, string original) => SystemTextCatalog.KeysForCatalogKey(key) is { } aliases ? Localization.ResolveAliases(aliases, original) : Localization.Resolve(key, original);
    public PreparedResources PrepareLocalization(LocalizationCatalog localization)
    {
        if (notifying)
        {
            throw new InvalidOperationException("Prepare localization outside resource notifications.");
        }
        Publication current = published;
        if (current.Localization.ArtworkFingerprint == localization.ArtworkFingerprint)
        {
            return new(this, mutationVersion, resolver, ImmutableDictionary<TextureKey, TextureValue>.Empty, current.Texts, current.Encoded, current.ExclusiveScopes, [], localization);
        }
        long version = mutationVersion;
        HashSet<TextureKey> affected = current.Localization.ArtworkFingerprints.Keys.Union(localization.ArtworkFingerprints.Keys).Where(key => current.Localization.ArtworkFingerprints.GetValueOrDefault(key) != localization.ArtworkFingerprints.GetValueOrDefault(key) && contexts.ContainsKey(key)).ToHashSet();
        if (affected.Count == 0)
        {
            return new(this, version, resolver, ImmutableDictionary<TextureKey, TextureValue>.Empty, current.Texts, current.Encoded, current.ExclusiveScopes, [], localization);
        }
        ImmutableDictionary<TextureKey, TextureValue>.Builder values = current.Values.ToBuilder();
        foreach (TextureKey key in affected)
        {
            values[key] = Resolve(contexts[key], resolver, localization);
        }
        BuildTransforms(values, affected);
        return new(this, version, resolver, affected.ToImmutableDictionary(key => key, key => values[key]), current.Texts, current.Encoded, current.ExclusiveScopes, [], localization);
    }
    public long Generation => Volatile.Read(ref published).Generation;
    /// <summary>Advances only when a prepared resolver, encoded-resource or localization catalog is committed; native texture context updates and residency changes do not advance it.</summary>
    public long CatalogGeneration => Volatile.Read(ref published).CatalogGeneration;
    public int ResidentTextureCount => handles.Count;
    public event Action<TextureKey, Exception>? ReplacementFailed;

    public StaticTextResource? FindText(ResourceKey key) => Volatile.Read(ref published).Texts.GetValueOrDefault(key);

    public string? ReadText(ResourceKey key, int index, string? original = null) => FindText(key)?.At(index) ?? original;

    public ResourceContent Get(ResourceKey key)
    {
        lock (encodedGate)
        {
            consumedResources.Add(key);
            Publication current = Volatile.Read(ref published);
            if (HiddenByScope(key, current.Encoded, current.ExclusiveScopes))
            {
                throw new FileNotFoundException($"Resource {key} is absent from its exclusive resource scope.");
            }
            ResourceContent content = current.Encoded.GetValueOrDefault(key)?.Content ?? source.Find(key) ?? throw new FileNotFoundException($"Resource {key} is unavailable from {Source.Id}.");
            return content;
        }
    }

    public ResourcePrefix ReadPrefix(ResourceKey key, int maximumBytes)
    {
        lock (encodedGate)
        {
            return ReadPrefixCore(key, maximumBytes);
        }
    }

    private ResourcePrefix ReadPrefixCore(ResourceKey key, int maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        Publication current = Volatile.Read(ref published);
        if (!current.Encoded.ContainsKey(key) && !EncodedResource.IsInScope(key, current.ExclusiveScopes) && source is IResourcePrefixSource prefixSource && prefixSource.ReadPrefix(key, maximumBytes) is ResourcePrefix prefix)
        {
            if (prefix.Bytes is null || prefix.Bytes.Length > maximumBytes || prefix.Length is long length && (length < 0 || prefix.Bytes.Length > length))
            {
                throw new InvalidDataException("Resource prefix differs from its declared bound or length.");
            }
            consumedResources.Add(key);
            return prefix;
        }
        using Stream stream = Get(key).OpenRead();
        long? size = stream.CanSeek ? stream.Length - stream.Position : null;
        using ResourceTrafficTelemetry.StagingStream bytes = new();
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
        using ResourceTrafficTelemetry.StagingStream bytes = new();
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
        if (exists && previous!.Revision == context.Revision && previous.Layout == context.Layout && previous.RequestedVariant == context.RequestedVariant)
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
            ReportReplacementFailure(context.Key, exception);
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
    public PreparedResources Prepare(ITextureReplacementResolver? nextResolver, IEnumerable<StaticTextResource>? textResources = null, IEnumerable<EncodedResource>? encodedResources = null, LocalizationCatalog? localization = null)
    {
        long version = mutationVersion;
        LocalizationCatalog nextLocalization = localization ?? published.Localization;
        ImmutableDictionary<ResourceKey, EncodedResource> encoded;
        ImmutableArray<string> exclusiveScopes;
        lock (encodedGate)
        {
            encoded = encodedResources?.ToImmutableDictionary(resource => resource.Key) ?? published.Encoded;
            exclusiveScopes = EncodedResource.ScopePrefixes(encoded.Values);
            ValidateEncodedChanges(encoded, exclusiveScopes);
        }
        ImmutableDictionary<TextureKey, TextureValue>.Builder next = ImmutableDictionary.CreateBuilder<TextureKey, TextureValue>();
        ImmutableArray<(TextureKey Key, Exception Exception)>.Builder failures = ImmutableArray.CreateBuilder<(TextureKey Key, Exception Exception)>();
        foreach ((TextureKey key, TextureBuildContext context) in contexts.ToArray())
        {
            TextureValue value;
            try
            {
                value = Resolve(context, nextResolver, nextLocalization);
            }
            catch (ResourceReplacementRejectedException exception)
            {
                value = Resolve(context, null, nextLocalization);
                failures.Add((key, exception));
            }
            next.Add(key, value);
        }
        BuildTransforms(next);
        return new PreparedResources(this, version, nextResolver, next.ToImmutable(), textResources?.ToImmutableDictionary(text => text.Key) ?? published.Texts, encoded, exclusiveScopes, failures.ToImmutable(), nextLocalization);
    }

    private ImmutableArray<ResourceKey> ChangedEncodedResources(ImmutableDictionary<ResourceKey, EncodedResource> next, ImmutableArray<string> exclusiveScopes)
    {
        return published.Encoded.Keys.Union(next.Keys).Where(key => ContentChanged(published.Encoded, next, key) || published.Encoded.GetValueOrDefault(key)?.ExclusiveScope != next.GetValueOrDefault(key)?.ExclusiveScope)
            .Union(consumedResources.Where(key => HiddenByScope(key, published.Encoded, published.ExclusiveScopes) != HiddenByScope(key, next, exclusiveScopes))).ToImmutableArray();
    }

    private void ValidateEncodedChanges(ImmutableDictionary<ResourceKey, EncodedResource> next, ImmutableArray<string> exclusiveScopes)
    {
        ImmutableArray<ResourceKey> loaded = consumedResources.Where(key => ContentChanged(published.Encoded, next, key) || HiddenByScope(key, published.Encoded, published.ExclusiveScopes) != HiddenByScope(key, next, exclusiveScopes)).ToImmutableArray();
        if (!loaded.IsEmpty)
        {
            throw new ResourceReloadRequiredException(loaded);
        }
    }

    private static bool ContentChanged(ImmutableDictionary<ResourceKey, EncodedResource> before, ImmutableDictionary<ResourceKey, EncodedResource> after, ResourceKey key) => before.GetValueOrDefault(key)?.Fingerprint != after.GetValueOrDefault(key)?.Fingerprint || before.GetValueOrDefault(key)?.Content.Format != after.GetValueOrDefault(key)?.Content.Format;

    private static bool HiddenByScope(ResourceKey key, ImmutableDictionary<ResourceKey, EncodedResource> encoded, ImmutableArray<string> exclusiveScopes) => !encoded.ContainsKey(key) && EncodedResource.IsInScope(key, exclusiveScopes);

    private void BuildTransforms(ImmutableDictionary<TextureKey, TextureValue>.Builder next, ISet<TextureKey>? affected = null)
    {
        Dictionary<TextureKey, TextureTransformContext> preparation = new(transforms);
        if (affected is not null)
        {
            bool added;
            do
            {
                added = false;
                foreach ((TextureKey key, TextureTransformContext transform) in preparation)
                {
                    if (!affected.Contains(key) && transform.Dependencies.Any(affected.Contains))
                    {
                        affected.Add(key);
                        added = true;
                    }
                }
            }
            while (added);
        }
        foreach (TextureKey key in preparation.Keys.Where(key => affected is null || affected.Contains(key)))
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
        foreach (TextureKey key in preparation.Keys.Where(key => affected is null || affected.Contains(key)))
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

    private TextureValue Resolve(TextureBuildContext context, ITextureReplacementResolver? replacementResolver, LocalizationCatalog? localization = null)
    {
        TextureReplacement? replacement = (localization ?? published.Localization).ResolveArtwork(context) ?? replacementResolver?.Resolve(context) ?? (source as ITextureReplacementResolver)?.Resolve(context);
        TextureValue value = replacement is null ? new TextureValue(context.Original, context.LogicalWidth, context.LogicalHeight, context.Source.Id) : new TextureValue(replacement.Image, replacement.LogicalWidth, replacement.LogicalHeight, replacement.Provenance, replacement.MipLevels, replacement.Variants) { IsReplacement = true, Animation = replacement.Animation };
        return context.RequestedVariant is { } variant && value.Variants is { } variants && variants.TryGetValue(variant, out var image) ? value with { Image = image, MipLevels = null, Animation = null } : value;
    }

    private void ReportReplacementFailure(TextureKey key, Exception exception)
    {
        foreach (Action<TextureKey, Exception> callback in ReplacementFailed?.GetInvocationList() ?? [])
        {
            try
            {
                callback(key, exception);
            }
            catch
            {
                // Failure reporting cannot prevent the valid source texture from loading.
            }
        }
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
        private readonly ImmutableDictionary<ResourceKey, EncodedResource> encoded;
        private readonly ImmutableArray<string> exclusiveScopes;
        private readonly ImmutableArray<(TextureKey Key, Exception Exception)> failures;
        private readonly LocalizationCatalog localization;
        private bool committed;
        private ResourceChangeSet? change;

        internal PreparedResources(ResourceService owner, long expectedVersion, ITextureReplacementResolver? resolver, ImmutableDictionary<TextureKey, TextureValue> values, ImmutableDictionary<ResourceKey, StaticTextResource> texts, ImmutableDictionary<ResourceKey, EncodedResource> encoded, ImmutableArray<string> exclusiveScopes, ImmutableArray<(TextureKey Key, Exception Exception)> failures, LocalizationCatalog localization)
        {
            this.owner = owner;
            this.expectedVersion = expectedVersion;
            this.resolver = resolver;
            this.values = values;
            this.texts = texts;
            this.encoded = encoded;
            this.exclusiveScopes = exclusiveScopes;
            this.failures = failures;
            this.localization = localization;
        }

        /// <summary>Publishes the prepared generation after validation and any coordinated external mutation.</summary>
        /// <param name="beforePublish">Runs under the encoded acquisition lock after validation. The callback must not acquire resources or mutate this service.</param>
        public void Commit(string reason = "Texture catalog changed", bool notify = true, Action? beforePublish = null)
        {
            lock (owner.encodedGate)
            {
                Validate();
                beforePublish?.Invoke();
                ImmutableArray<ResourceKey> changedEncoded = owner.ChangedEncodedResources(encoded, exclusiveScopes);
                committed = true;
                owner.mutationVersion++;
                Publication previous = owner.published;
                owner.resolver = resolver;
                Volatile.Write(ref owner.published, new Publication(previous.Generation + 1, previous.Values.SetItems(values), texts, encoded, exclusiveScopes, localization) { CatalogGeneration = previous.CatalogGeneration + 1 });
                change = new ResourceChangeSet(previous.Generation, previous.Generation + 1, values.Keys.ToImmutableArray(), reason) { Texts = previous.Texts.Keys.Union(texts.Keys).Concat(previous.Localization.Fingerprint == localization.Fingerprint ? [] : new[] { new ResourceKey("localization/catalog") }).ToImmutableArray(), Resources = changedEncoded };
            }
            if (notify)
            {
                Notify();
            }
        }

        public void Validate()
        {
            lock (owner.encodedGate)
            {
                if (committed || owner.notifying || owner.mutationVersion != expectedVersion)
                {
                    throw new InvalidOperationException("Discard stale resource preparation and prepare the current resident set again.");
                }
                owner.ValidateEncodedChanges(encoded, exclusiveScopes);
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
            foreach ((TextureKey key, Exception exception) in failures)
            {
                owner.ReportReplacementFailure(key, exception);
            }
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? action = unsubscribe;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}
