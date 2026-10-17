using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

/// <summary>Keeps provider-materialized files alive for the native decoder or output device.</summary>
internal sealed class GameFileResources(string gameDirectory) : IDisposable
{
    private IResourceSource? source;
    private ResourceService? resources;

    public ResourceService Resources => resources ??= new ResourceService(source ??= GameDataSourceFactory.Open(gameDirectory));

    public string ResolveDataFile(string relativePath)
    {
        IGameFileSource files = Resources.GetSource<IGameFileSource>() ?? throw new NotImplementedException("The selected resource provider does not implement file resolution.");
        return files.ResolveFile(relativePath);
    }

    public string ResolveInstallationFile(string relativePath)
    {
        string path = new ResourceKey(relativePath).Path;
        if (!path.StartsWith("data/", StringComparison.Ordinal))
        {
            throw new NotImplementedException("Game media paths must identify a resource in the logical data tree.");
        }
        return ResolveDataFile(path[5..]);
    }

    public void Dispose()
    {
        source?.Dispose();
        source = null;
        resources = null;
    }
}
