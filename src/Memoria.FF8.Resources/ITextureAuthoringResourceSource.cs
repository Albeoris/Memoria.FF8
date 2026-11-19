namespace Memoria.FF8.Resources;

/// <summary>Independent authoring readers bound nested archive retention and reopen encoded snapshots after eviction.</summary>
public interface ITextureAuthoringResourceSource
{
    bool IsTextureAuthoringSource { get; }
    int RetainedTextureAuthoringArchiveCount { get; }
    long RetainedTextureAuthoringArchiveBytes { get; }
    void ReleaseTextureAuthoringArchives();
}
