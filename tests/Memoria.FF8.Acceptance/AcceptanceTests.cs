using System.Diagnostics;
using System.Reflection;

namespace Memoria.FF8.Acceptance;

/// <summary>
/// Acceptance of the recreated product against the original's recordings, through the recomp tool (the global tool, or
/// the executable named by RECOMP): scenarios, and every spec's acceptance observations ('recomp observe --all --no-record':
/// scenarios and UI snapshots, nothing written to the workspace).
/// </summary>
public sealed class AcceptanceTests
{
    private static string Workspace
    {
        get
        {
            string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_RECOMP_WORKSPACE");
            Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_RECOMP_WORKSPACE to a recording workspace to run external acceptance checks.");
            return Path.GetFullPath(directory!);
        }
    }

    /// <summary>Same arguments, inputs and UI actions as the original's scenarios (tests/scenarios.json): same observable result.</summary>
    [Fact]
    public void Scenarios_match_the_original()
    {
        Assert.SkipUnless(File.Exists(Path.Combine(Workspace, "tests", "scenarios.json")), "no tests/scenarios.json yet (recomp scenario add)");
        (int exit, string output) = Recomp("scenario", "run", "--candidate", Environment.GetEnvironmentVariable("MEMORIA_FF8_CANDIDATE") ?? Metadata("Candidate"));
        Assert.True(exit is 0 or 3, output);
    }

    /// <summary>Every statement's acceptance observation holds on the product.</summary>
    [Fact]
    public void Spec_observations_hold()
    {
        (int exit, string output) = Recomp("observe", "--all", "--no-record");
        Assert.True(exit is 0 or 3, output);
    }

    private static string Metadata(string key)
    {
        return typeof(AcceptanceTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value ?? throw new InvalidOperationException($"assembly metadata '{key}' is empty");
    }

    private static (int Exit, string Output) Recomp(params string[] arguments)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("RECOMP") ?? "recomp")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("--workspace");
        start.ArgumentList.Add(Workspace);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("recomp did not start");
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error.Result);
    }
}
