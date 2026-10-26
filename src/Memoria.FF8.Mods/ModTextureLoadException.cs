using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

/// <summary>Identifies the package contribution that failed replacement preparation.</summary>
public sealed class ModTextureLoadException : ResourceReplacementRejectedException
{
    public ModTextureLoadException(string packageId, string relativePath, Exception innerException)
        : base($"Texture '{relativePath}' from package '{packageId}' could not be loaded: {innerException.Message}", innerException)
    {
        PackageId = packageId;
        RelativePath = relativePath;
    }

    public string PackageId { get; }
    public string RelativePath { get; }
}
