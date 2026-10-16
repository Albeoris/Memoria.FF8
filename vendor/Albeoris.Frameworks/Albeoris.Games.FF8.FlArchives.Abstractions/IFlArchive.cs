namespace Albeoris.Games.FF8.FlArchives.Abstractions;

public interface IFlArchive : IDisposable, IAsyncDisposable
{
    IReadOnlyList<IFlArchiveEntry> Entries { get; }

    IFlArchiveEntry AddEntry(String relativePath);
    void RemoveEntry(String relativePath);
    void Flush();
}