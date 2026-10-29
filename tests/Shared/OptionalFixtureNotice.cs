using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Memoria.FF8.Tests;

internal static class OptionalFixtureNotice
{
    // Build.ps1 reports the union once before starting its test hosts.
    internal const string ReportedVariable = "MEMORIA_TEST_FIXTURE_NOTICE_REPORTED";

    [ModuleInitializer]
    internal static void Report()
    {
        if (Environment.GetEnvironmentVariable(ReportedVariable) == "1")
        {
            return;
        }

        Assembly assembly = typeof(OptionalFixtureNotice).Assembly;
        using Stream catalog = assembly.GetManifestResourceStream("Memoria.OptionalFixtureInputs.json")!;
        Dictionary<string, string[]> inputs = JsonSerializer.Deserialize<Dictionary<string, string[]>>(catalog)!;
        if (!inputs.TryGetValue(assembly.GetName().Name!, out string[]? requirements))
        {
            return;
        }

        string[] missing = requirements.Where(requirement => requirement.Split('|').All(variable => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))).ToArray();
        if (missing.Length > 0)
        {
            Console.WriteLine($"Optional fixture inputs unset ({assembly.GetName().Name}): {string.Join(", ", missing)}. Dependent checks will be skipped; skip totals remain in the test summary.");
        }
    }
}
