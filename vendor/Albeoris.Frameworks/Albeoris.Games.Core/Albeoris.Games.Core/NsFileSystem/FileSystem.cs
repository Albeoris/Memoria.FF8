namespace Albeoris.Games.Core.NsFileSystem;

public static class FileSystem
{
    public static void EnsureParentDirectoryExists(String filePath)
    {
        String? directoryPath = Path.GetDirectoryName(filePath);
        if (directoryPath is null)
            throw new DirectoryNotFoundException($"Directory path was not found. File path: {filePath}");
        Directory.CreateDirectory(directoryPath);
    }
}