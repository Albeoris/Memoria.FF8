namespace Memoria.FF8.Assets;

/// <summary>Optional seam for future mod packages; paths are normalized original archive paths, not host filesystem paths.</summary>
public interface IAssetOverrideSource
{
    bool TryRead(string logicalPath, out byte[] content);
}
