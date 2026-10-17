using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets;

/// <summary>Opens an archive family by logical name. The caller owns and disposes each returned archive.</summary>
public interface IGameArchiveSource
{
    IFlArchive OpenArchive(string family);
}

/// <summary>Resolves an original Data-relative file to a readable path for consumers that require a file.</summary>
public interface IGameFileSource
{
    string ResolveFile(string relativeDataPath);
}
