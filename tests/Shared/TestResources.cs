using System.Text.Json;

namespace Memoria.FF8.Tests;

internal static class TestResources
{
    private static readonly Lazy<string> Artifacts = new(CreateArtifactDirectory);

    public static string GameDirectory => RequireDirectory(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY"), "Set MEMORIA_FF8_GAME_DIR or FF8_GAME_DIRECTORY to an installed original game directory.");

    public static string FixtureDirectory
    {
        get
        {
            string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
            string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURES") ?? (string.IsNullOrWhiteSpace(catalog) ? null : Path.GetDirectoryName(Path.GetFullPath(catalog)));
            return RequireDirectory(directory, "Set MEMORIA_FF8_TEST_FIXTURES or MEMORIA_FF8_TEST_FIXTURE_CATALOG to optional external regression fixtures.");
        }
    }

    public static string ArtifactDirectory => Artifacts.Value;

    public static string ArtifactFile(string name)
    {
        return Path.Combine(ArtifactDirectory, name);
    }

    public static string FixturePath(string resource)
    {
        if (Path.IsPathFullyQualified(resource))
        {
            return resource;
        }
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
#if FF8_XUNIT_RESOURCE_TESTS
        Xunit.Assert.SkipUnless(!string.IsNullOrWhiteSpace(catalog) && File.Exists(catalog), "Set MEMORIA_FF8_TEST_FIXTURE_CATALOG to an optional JSON object mapping test resource IDs to external paths.");
#endif
        if (string.IsNullOrWhiteSpace(catalog) || !File.Exists(catalog))
        {
            throw new FileNotFoundException("Set MEMORIA_FF8_TEST_FIXTURE_CATALOG to an optional JSON object mapping test resource IDs to external paths.");
        }
        Dictionary<string, string> resources = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalog)) ?? throw new InvalidDataException("The external fixture catalog must contain a JSON object.");
#if FF8_XUNIT_RESOURCE_TESTS
        Xunit.Assert.SkipUnless(resources.ContainsKey(resource), $"The optional external fixture catalog has no resource '{resource}'.");
#endif
        if (!resources.TryGetValue(resource, out string? path) || string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidDataException($"The external fixture catalog has no path for resource '{resource}'.");
        }
        string resolved = Path.GetFullPath(path, FixtureDirectory);
#if FF8_XUNIT_RESOURCE_TESTS
        Xunit.Assert.SkipUnless(File.Exists(resolved), $"The optional external fixture for resource '{resource}' is unavailable.");
#endif
        return resolved;
    }

    private static string CreateArtifactDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        };
        return directory;
    }

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
