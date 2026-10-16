using Albeoris.Games.Core.NsDisposables;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Albeoris.Games.FF8.FlArchives;

public sealed partial class FlArchive : IFlArchive
{
    private readonly EntryCollection _entries;
    private readonly DisposableStack _disposableStack = new();

    private FlArchive(Stream listingStream, Stream metricsStream, Stream contentStream)
    {
        ArgumentNullException.ThrowIfNull(listingStream);
        ArgumentNullException.ThrowIfNull(metricsStream);
        ArgumentNullException.ThrowIfNull(contentStream);

        _disposableStack.Add(listingStream);
        _disposableStack.Add(metricsStream);
        _disposableStack.Add(contentStream);

        _entries = metricsStream.Position == metricsStream.Length
            ? EntryCollection.CreateEmpty(listingStream, metricsStream, contentStream)
            : EntryCollectionReader.Read(listingStream, metricsStream, contentStream);
    }

    public IReadOnlyList<IFlArchiveEntry> Entries => _entries.Entries;

    /// <summary>Adds a new empty entry with the given relative path.</summary>
    public IFlArchiveEntry AddEntry(String relativePath) => _entries.AddEntry(relativePath);

    /// <summary>Removes the entry with the given relative path.</summary>
    public void RemoveEntry(String relativePath) => _entries.RemoveEntry(relativePath);

    /// <summary>Flushes all pending changes to the underlying streams.</summary>
    public void Flush() => _entries.Flush();

    public void Dispose()
    {
        try
        {
            Flush();
        }
        finally
        {
            _disposableStack.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            Flush();
        }
        finally
        {
            await _disposableStack.DisposeAsync().ConfigureAwait(false);
        }
    }
}
