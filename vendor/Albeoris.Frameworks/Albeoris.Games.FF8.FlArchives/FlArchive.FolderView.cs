using System.Diagnostics.CodeAnalysis;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Albeoris.Games.FF8.FlArchives;

/// <summary>
/// Wraps a raw <see cref="FlArchive"/> and transparently expands sub-archives contained within
/// it. A sub-archive is a triplet of entries sharing the same base name and having the extensions
/// <c>.fl</c>, <c>.fi</c>, and <c>.fs</c>. Entries belonging to a sub-archive are exposed with
/// the sub-archive's <c>.fl</c> relative path used as a directory prefix, e.g.
/// <c>field/mapdata/bc/bccent12.fl/texture.tim</c>.
/// </summary>
internal sealed class FolderFlArchive : IFlArchive
{
    private readonly FlArchive _parent;

    // Maps sub-archive key (the .fl relative path) → its loaded state.
    private readonly Dictionary<String, SubArchiveState> _subArchives;

    // The composite ordered entry list presented to callers.
    private List<IFlArchiveEntry> _compositeEntries;

    public FolderFlArchive(FlArchive parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        _parent = parent;
        _subArchives = new Dictionary<String, SubArchiveState>(FlArchive.PathComparer);
        _compositeEntries = new List<IFlArchiveEntry>();
        LoadSubArchives();
        RebuildCompositeEntries();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IFlArchiveEntry> Entries => _compositeEntries;

    /// <summary>
    /// Adds a new entry. If <paramref name="relativePath"/> contains a <c>.fl/</c> path segment,
    /// the entry is routed to the matching sub-archive; otherwise it is added to the parent archive.
    /// </summary>
    public IFlArchiveEntry AddEntry(String relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        if (TryParseSubArchivePath(relativePath, out String? subKey, out String? innerPath))
        {
            SubArchiveState state = GetOrCreateSubArchiveState(subKey);
            IFlArchiveEntry innerEntry = state.InnerArchive.AddEntry(ToFullInnerPath(subKey, innerPath));
            state.HasChanges = true;
            RebuildCompositeEntries();
            return new CompositeEntry(innerEntry, subKey, state);
        }

        IFlArchiveEntry parentEntry = _parent.AddEntry(relativePath);
        RebuildCompositeEntries();
        return parentEntry;
    }

    /// <summary>
    /// Removes the entry with the given relative path. If the path contains a <c>.fl/</c>
    /// segment, the removal is routed to the matching sub-archive.
    /// </summary>
    public void RemoveEntry(String relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        if (TryParseSubArchivePath(relativePath, out String? subKey, out String? innerPath))
        {
            SubArchiveState state = GetExistingSubArchiveState(subKey);
            state.InnerArchive.RemoveEntry(ToFullInnerPath(subKey, innerPath));
            state.HasChanges = true;
            RebuildCompositeEntries();
            return;
        }

        _parent.RemoveEntry(relativePath);
        RebuildCompositeEntries();
    }

    /// <summary>Flushes all modified sub-archives back to the parent, then flushes the parent.</summary>
    public void Flush()
    {
        foreach (SubArchiveState state in _subArchives.Values)
        {
            if (!state.HasChanges)
                continue;

            state.InnerArchive.Flush();
            WriteSubArchiveStreamToParent(state.FlRelativePath, state.InnerListing);
            WriteSubArchiveStreamToParent(state.FiRelativePath, state.InnerMetrics);
            WriteSubArchiveStreamToParent(state.FsRelativePath, state.InnerContent);
            state.HasChanges = false;
        }

        _parent.Flush();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Flush();
        foreach (SubArchiveState state in _subArchives.Values)
            state.InnerArchive.Dispose();
        _parent.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Flush();
        foreach (SubArchiveState state in _subArchives.Values)
            await state.InnerArchive.DisposeAsync().ConfigureAwait(false);
        await _parent.DisposeAsync().ConfigureAwait(false);
    }

    // ── Construction helpers ─────────────────────────────────────────────────────────────────────

    private void LoadSubArchives()
    {
        IReadOnlyList<IFlArchiveEntry> parentEntries = _parent.Entries;

        // Index all entries by relative path for fast lookup.
        Dictionary<String, IFlArchiveEntry> byPath = new Dictionary<String, IFlArchiveEntry>(FlArchive.PathComparer);
        foreach (IFlArchiveEntry e in parentEntries)
            byPath[e.RelativePath] = e;

        foreach (IFlArchiveEntry entry in parentEntries)
        {
            if (!entry.RelativePath.EndsWith(".fl", StringComparison.OrdinalIgnoreCase))
                continue;

            String basePath = entry.RelativePath.Substring(0, entry.RelativePath.Length - 3); // strip .fl
            String fiPath = basePath + ".fi";
            String fsPath = basePath + ".fs";

            if (!byPath.TryGetValue(fiPath, out IFlArchiveEntry? fiEntry) ||
                !byPath.TryGetValue(fsPath, out IFlArchiveEntry? fsEntry))
                continue;

            // Load all three component streams into memory.
            MemoryStream listing = ExtractToMemory(entry);
            MemoryStream metrics = ExtractToMemory(fiEntry);
            MemoryStream content = ExtractToMemory(fsEntry);

            // Files inside sub-archives are never compressed; open in Files mode.
            IFlArchive inner = FlArchive.Open(listing, metrics, content, leaveOpen: true, FlArchiveRepresentation.Files);

            _subArchives[entry.RelativePath] = new SubArchiveState(
                flRelativePath: entry.RelativePath,
                fiRelativePath: fiPath,
                fsRelativePath: fsPath,
                innerListing: listing,
                innerMetrics: metrics,
                innerContent: content,
                innerArchive: (FlArchive)inner);
        }
    }

    private void RebuildCompositeEntries()
    {
        // Build the set of raw sub-archive component paths to exclude from direct exposure.
        HashSet<String> hiddenPaths = new HashSet<String>(FlArchive.PathComparer);
        foreach (SubArchiveState state in _subArchives.Values)
        {
            hiddenPaths.Add(state.FlRelativePath);
            hiddenPaths.Add(state.FiRelativePath);
            hiddenPaths.Add(state.FsRelativePath);
        }

        List<IFlArchiveEntry> result = new List<IFlArchiveEntry>();

        foreach (IFlArchiveEntry parentEntry in _parent.Entries)
        {
            if (hiddenPaths.Contains(parentEntry.RelativePath))
            {
                if (_subArchives.TryGetValue(parentEntry.RelativePath, out SubArchiveState? state))
                {
                    // Inject sub-archive entries in order at this position.
                    foreach (IFlArchiveEntry innerEntry in state.InnerArchive.Entries)
                        result.Add(new CompositeEntry(innerEntry, state.FlRelativePath, state));
                }
                // .fi and .fs raw entries are silently skipped.
                continue;
            }

            result.Add(parentEntry);
        }

        _compositeEntries = result;
    }

    private static MemoryStream ExtractToMemory(IFlArchiveEntry entry)
    {
        MemoryStream ms = new MemoryStream((Int32)Math.Max(entry.Size, 0));
        if (entry.Size > 0)
        {
            using (Stream src = entry.OpenForRead())
                src.CopyTo(ms);
        }
        ms.Position = 0;
        return ms;
    }

    private void WriteSubArchiveStreamToParent(String relativePath, MemoryStream source)
    {
        source.Position = 0;
        IFlArchiveEntry? parentEntry = null;
        foreach (IFlArchiveEntry e in _parent.Entries)
        {
            if (FlArchive.PathComparer.Equals(e.RelativePath, relativePath))
            {
                parentEntry = e;
                break;
            }
        }

        if (parentEntry is null)
            throw new InvalidOperationException($"Parent archive does not contain the expected sub-archive component entry '{relativePath}'.");

        UInt32 newSize = checked((UInt32)source.Length);
        using (Stream dest = parentEntry.OpenForWrite(newSize))
            source.CopyTo(dest);
    }

    // ── Path routing ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a user-facing inner relative path (the part after the <c>.fl/</c> separator in a
    /// composite path) to the full root-relative path as stored inside the inner archive.
    /// For example, <c>("arc.fl", "new.bin")</c> → <c>"arc/new.bin"</c>.
    /// </summary>
    private static String ToFullInnerPath(String subArchiveKey, String innerRelativePath)
    {
        // Use the same separator as the sub-archive key itself.
        String keyWithoutExt = subArchiveKey.Substring(0, subArchiveKey.Length - 3); // strip ".fl"
        return keyWithoutExt + '/' + innerRelativePath;
    }

    /// <summary>
    /// Splits <paramref name="path"/> at the first path segment that ends with <c>.fl</c>.
    /// Returns <see langword="true"/> when such a segment is found.
    /// </summary>
    private static Boolean TryParseSubArchivePath(String path, [NotNullWhen(true)] out String? subArchiveKey, [NotNullWhen(true)] out String? innerRelativePath)
    {
        ReadOnlySpan<char> span = path;
        Int32 start = 0;

        while (start < span.Length)
        {
            Int32 slash = span.Slice(start).IndexOf('/');
            Int32 segmentEnd = slash < 0 ? span.Length : start + slash;
            ReadOnlySpan<char> segment = span.Slice(start, segmentEnd - start);

            if (segment.EndsWith(".fl", StringComparison.OrdinalIgnoreCase))
            {
                subArchiveKey = path.Substring(0, segmentEnd);
                innerRelativePath = segmentEnd < span.Length ? path.Substring(segmentEnd + 1) : String.Empty;
                return true;
            }

            if (slash < 0)
                break;

            start = segmentEnd + 1;
        }

        subArchiveKey = null;
        innerRelativePath = null;
        return false;
    }

    private SubArchiveState GetExistingSubArchiveState(String subKey)
    {
        if (!_subArchives.TryGetValue(subKey, out SubArchiveState? state))
            throw new InvalidOperationException($"No sub-archive found for key '{subKey}'.");
        return state;
    }

    private SubArchiveState GetOrCreateSubArchiveState(String subKey)
    {
        if (_subArchives.TryGetValue(subKey, out SubArchiveState? state))
            return state;

        // Add the three component entries to the parent. They start at size=0 and are given
        // their real content by WriteSubArchiveStreamToParent during Flush().
        _parent.AddEntry(subKey);
        String basePath = subKey.Substring(0, subKey.Length - 3);
        _parent.AddEntry(basePath + ".fi");
        _parent.AddEntry(basePath + ".fs");

        MemoryStream listing = new MemoryStream();
        MemoryStream metrics = new MemoryStream();
        MemoryStream content = new MemoryStream();

        IFlArchive inner = FlArchive.Open(listing, metrics, content, leaveOpen: true, FlArchiveRepresentation.Files);

        state = new SubArchiveState(
            flRelativePath: subKey,
            fiRelativePath: basePath + ".fi",
            fsRelativePath: basePath + ".fs",
            innerListing: listing,
            innerMetrics: metrics,
            innerContent: content,
            innerArchive: (FlArchive)inner)
        {
            HasChanges = true,
        };

        _subArchives[subKey] = state;
        return state;
    }

    // ── Nested types ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Holds the in-memory state of a single loaded sub-archive.</summary>
    private sealed class SubArchiveState
    {
        public String FlRelativePath { get; }
        public String FiRelativePath { get; }
        public String FsRelativePath { get; }

        public MemoryStream InnerListing { get; }
        public MemoryStream InnerMetrics { get; }
        public MemoryStream InnerContent { get; }

        public FlArchive InnerArchive { get; }

        public Boolean HasChanges { get; set; }

        public SubArchiveState(
            String flRelativePath, String fiRelativePath, String fsRelativePath,
            MemoryStream innerListing, MemoryStream innerMetrics, MemoryStream innerContent,
            FlArchive innerArchive)
        {
            FlRelativePath = flRelativePath;
            FiRelativePath = fiRelativePath;
            FsRelativePath = fsRelativePath;
            InnerListing = innerListing;
            InnerMetrics = innerMetrics;
            InnerContent = innerContent;
            InnerArchive = innerArchive;
        }
    }

    /// <summary>
    /// Adapts an inner <see cref="IFlArchiveEntry"/> by prepending the sub-archive key as a
    /// directory prefix to its <see cref="RelativePath"/>. Marks the owning
    /// <see cref="SubArchiveState"/> as dirty when content is written so that <see cref="FolderFlArchive.Flush"/>
    /// writes the modified sub-archive back to the parent.
    /// </summary>
    private sealed class CompositeEntry : IFlArchiveEntry
    {
        private readonly IFlArchiveEntry _inner;
        private readonly String _subArchiveKey;
        private readonly SubArchiveState _state;
        // Sub-archive key without '.fl', normalised to '/', with trailing '/'. Used to strip
        // the shared root-relative prefix from inner entry paths.
        private readonly String _innerPathPrefix;

        public CompositeEntry(IFlArchiveEntry inner, String subArchiveKey, SubArchiveState state)
        {
            _inner = inner;
            _subArchiveKey = subArchiveKey;
            _state = state;
            _innerPathPrefix = subArchiveKey.Substring(0, subArchiveKey.Length - 3);
        }

        /// <inheritdoc/>
        public String RelativePath
        {
            get
            {
                String innerPath = _inner.RelativePath;

                if (!innerPath.StartsWith(_innerPathPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Inner entry path '{innerPath}' does not begin with the expected sub-archive " +
                        $"prefix '{_innerPathPrefix}'. Sub-archive key: '{_subArchiveKey}'.");

                String stripped = innerPath.Substring(_innerPathPrefix.Length);
                String relativePath = (_subArchiveKey + stripped);
                return NormalizePath(relativePath);
            }
        }

        /// <inheritdoc/>
        public UInt32 Offset => _inner.Offset;

        /// <inheritdoc/>
        public UInt32 Size => _inner.Size;

        /// <inheritdoc/>
        public Stream OpenForRead() => _inner.OpenForRead();

        /// <inheritdoc/>
        public Stream OpenForWrite(UInt32 desiredSize)
        {
            _state.HasChanges = true;
            return _inner.OpenForWrite(desiredSize);
        }

        private static String NormalizePath(string relativePath)
        {
            return relativePath.Replace('\\', '/');
        }
    }
}
