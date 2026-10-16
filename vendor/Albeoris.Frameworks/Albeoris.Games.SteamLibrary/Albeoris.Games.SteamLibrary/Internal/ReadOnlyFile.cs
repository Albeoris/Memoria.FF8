namespace Albeoris.Games.SteamLibrary.Internal;

internal static class ReadOnlyFile
{
    public static FileStream Open(String path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
    }
}
