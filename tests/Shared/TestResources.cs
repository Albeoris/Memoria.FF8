namespace Memoria.FF8.Tests;

internal static class TestResources
{
    public static string GameDirectory => RequireDirectory(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY"), "Set MEMORIA_FF8_GAME_DIR or FF8_GAME_DIRECTORY to an installed original game directory.");

    public static string FixtureDirectory => RequireDirectory(Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURES"), "Set MEMORIA_FF8_TEST_FIXTURES to an optional external regression fixture directory.");

    public static string GameFile(params string[] parts)
    {
        return Path.Combine([GameDirectory, .. parts]);
    }

    public static string FixtureFile(params string[] parts)
    {
        string path = Path.Combine([FixtureDirectory, .. parts]);
#if FF8_XUNIT_RESOURCE_TESTS
        Xunit.Assert.SkipUnless(File.Exists(path), "The optional external regression fixture is required.");
#endif
        return path;
    }

    private static string RequireDirectory(string? directory, string message)
    {
#if FF8_XUNIT_RESOURCE_TESTS
        Xunit.Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), message);
#endif
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(message);
        }
        return directory;
    }
}
