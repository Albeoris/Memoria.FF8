using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Bootstrap.Tests;

[Collection("Bootstrap environment")]
public sealed class GraphicsRestartTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria graphics restart tests", Guid.NewGuid().ToString("N"));
    private readonly string? previousDiagnostics;
    private string RequestPath => Path.Combine(directory, "restart.json");
    private string StatePath => Path.Combine(directory, "current.ff8state");

    public GraphicsRestartTests()
    {
        Directory.CreateDirectory(directory);
        previousDiagnostics = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY");
        Environment.SetEnvironmentVariable("MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY", Path.Combine(directory, "diagnostics"));
    }

    [Fact]
    public void MissingReceiptDoesNotRequestRestart()
    {
        Assert.Null(GraphicsRestart.ReadArguments(RequestPath, "owned-token", 42, ["--validate"]));
    }

    [Fact]
    public void ValidReceiptReplacesOnlyStateAndBackendAndRetainsOtherArgumentsExactly()
    {
        WriteReceipt();
        string[] original = ["--game-dir", "game files", "--state", "old state", "--mods-dir", "my mods", "--graphics-backend", "Auto", "--achievements-local-only", "--save-state", "player destination", "--state", "second old state", "--graphics-backend", "Direct3D11"];
        string[] resumed = GraphicsRestart.ReadArguments(RequestPath, "owned-token", 42, original)!;
        Assert.Equal(["--game-dir", "game files", "--mods-dir", "my mods", "--achievements-local-only", "--save-state", "player destination", "--graphics-backend", "Vulkan", "--state", StatePath], resumed);
        Assert.Equal("old state", original[3]);
        Assert.True(File.Exists(StatePath));
    }

    [Theory]
    [InlineData("token", "stale-token")]
    [InlineData("processId", "41")]
    [InlineData("schemaVersion", "2")]
    [InlineData("sha256", "bad-hash")]
    public void StaleOrUnownedOrChangedReceiptIsRejected(string property, string value)
    {
        JsonObject receipt = WriteReceipt();
        receipt[property] = property is "processId" or "schemaVersion" ? JsonValue.Create(int.Parse(value)) : JsonValue.Create(value);
        File.WriteAllText(RequestPath, receipt.ToJsonString());
        Assert.Throws<InvalidDataException>(() => GraphicsRestart.ReadArguments(RequestPath, "owned-token", 42, []));
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("relative")]
    [InlineData("wrong-extension")]
    [InlineData("empty")]
    [InlineData("changed")]
    public void StateMustBeUnchangedNonemptyStandaloneFileBesideOwnedReceipt(string scenario)
    {
        JsonObject receipt = WriteReceipt();
        if (scenario == "outside")
        {
            string nested = Path.Combine(directory, "other", "current.ff8state");
            Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
            File.Copy(StatePath, nested);
            receipt["statePath"] = nested;
        }
        else if (scenario == "relative")
        {
            receipt["statePath"] = "current.ff8state";
        }
        else if (scenario == "wrong-extension")
        {
            string renamed = Path.ChangeExtension(StatePath, ".json");
            File.Copy(StatePath, renamed);
            receipt["statePath"] = renamed;
        }
        else
        {
            File.WriteAllText(StatePath, scenario == "empty" ? "" : "changed save contents");
        }
        File.WriteAllText(RequestPath, receipt.ToJsonString());
        Assert.Throws<InvalidDataException>(() => GraphicsRestart.ReadArguments(RequestPath, "owned-token", 42, []));
    }

    [Theory]
    [InlineData("--state")]
    [InlineData("--graphics-backend")]
    public void IncompleteExistingOptionIsRejectedInsteadOfRebuildingAmbiguousArguments(string option)
    {
        WriteReceipt();
        Assert.Throws<ArgumentException>(() => GraphicsRestart.ReadArguments(RequestPath, "owned-token", 42, [option]));
    }

    [Fact]
    public async Task CleanReadyHostRestartsOnceThenReportsTheNewHostReadiness()
    {
        List<GraphicsBackend> attempts = [];
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", "Vulkan", "--game-dir", "game files"], output, error, TestContext.Current.CancellationToken, backend =>
        {
            attempts.Add(backend);
            return Child(backend, ReadyScript + (attempts.Count == 1 ? RestartScript + "\nexit 0" : "\nexit 0"));
        }, TimeSpan.FromSeconds(15));
        Assert.Equal(0, result);
        Assert.Equal([GraphicsBackend.Vulkan, OperatingSystem.IsWindows() ? GraphicsBackend.Direct3D11 : GraphicsBackend.Vulkan], attempts);
        Assert.Contains("Graphics restart: resuming the preserved standalone game in a new host.", output.ToString());
        Assert.Contains("Graphics ready: requested=Auto actual=" + attempts[1], output.ToString());
        Assert.Equal(2, Directory.GetFiles(Path.Combine(directory, "diagnostics"), "result.json", SearchOption.AllDirectories).Length);
        Assert.Single(Directory.GetFiles(Path.Combine(directory, "diagnostics"), "*.ff8state", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(37, true)]
    [InlineData(0, false)]
    public async Task ReceiptCannotRestartAfterGameplayCrashOrWithoutGraphicsReadiness(int exitCode, bool ready)
    {
        int attempts = 0;
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", "Vulkan"], output, error, TestContext.Current.CancellationToken, backend =>
        {
            attempts++;
            return Child(backend, (ready ? ReadyScript : "") + RestartScript + $"\nexit {exitCode}");
        }, TimeSpan.FromSeconds(15));
        Assert.Equal(1, attempts);
        Assert.Equal(ready ? exitCode : 1, result);
        Assert.DoesNotContain("Graphics restart: resuming", output.ToString());
    }

    [Fact]
    public async Task AStaleReceiptCannotAuthorizeAnotherHostAfterCleanReadyExit()
    {
        int attempts = 0;
        using StringWriter output = new();
        using StringWriter error = new();
        await Assert.ThrowsAsync<InvalidDataException>(() => GraphicsDispatcher.RunAsync(directory, ["--graphics-backend", "Vulkan"], output, error, TestContext.Current.CancellationToken, backend =>
        {
            attempts++;
            return Child(backend, ReadyScript + "$env:MEMORIA_GRAPHICS_RESTART_TOKEN = 'stale-token'\n" + RestartScript + "\nexit 0");
        }, TimeSpan.FromSeconds(15)));
        Assert.Equal(1, attempts);
        Assert.DoesNotContain("Graphics restart: resuming", output.ToString());
    }

    [Fact]
    public void UnsupportedBackendAndMissingStateFailBeforeArgumentsAreRebuilt()
    {
        JsonObject receipt = WriteReceipt();
        receipt["backend"] = "OpenGL";
        File.WriteAllText(RequestPath, receipt.ToJsonString());
        Assert.Throws<ArgumentException>(() => GraphicsRestart.ReadArguments(RequestPath, "owned-token", 42, []));
        WriteReceipt();
        File.Delete(StatePath);
        Assert.Throws<FileNotFoundException>(() => GraphicsRestart.ReadArguments(RequestPath, "owned-token", 42, []));
    }

    [Theory]
    [InlineData("--validate")]
    [InlineData("--headless-play")]
    [InlineData("--import-save")]
    public async Task NongraphicsCommandsIgnoreRestartReceipts(string command)
    {
        int attempts = 0;
        using StringWriter output = new();
        using StringWriter error = new();
        int result = await GraphicsDispatcher.RunAsync(directory, [command, "--graphics-backend", "Vulkan"], output, error, TestContext.Current.CancellationToken, backend =>
        {
            attempts++;
            return Child(backend, RestartScript + "\nexit 0");
        });
        Assert.Equal(0, result);
        Assert.Equal(1, attempts);
        Assert.DoesNotContain("Graphics restart: resuming", output.ToString());
    }

    private JsonObject WriteReceipt()
    {
        File.WriteAllText(StatePath, "standalone state bytes");
        JsonObject receipt = new() { ["schemaVersion"] = 1, ["processId"] = 42, ["token"] = "owned-token", ["backend"] = "Vulkan", ["statePath"] = StatePath, ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(StatePath))).ToLowerInvariant() };
        File.WriteAllText(RequestPath, receipt.ToJsonString());
        return receipt;
    }

    private ProcessStartInfo Child(GraphicsBackend backend, string body)
    {
        string script = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".ps1");
        File.WriteAllText(script, "param([string] $Backend)\n$ErrorActionPreference = 'Stop'\n" + body);
        ProcessStartInfo start = new("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script, backend.ToString() })
        {
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    private const string ReadyScript = """
        $ready = @{schemaVersion=1; processId=$PID; backend=$Backend} | ConvertTo-Json -Compress
        [IO.File]::WriteAllText($env:MEMORIA_FF8_HOST_READY_FILE + '.tmp', $ready)
        [IO.File]::Move($env:MEMORIA_FF8_HOST_READY_FILE + '.tmp', $env:MEMORIA_FF8_HOST_READY_FILE)
        Start-Sleep -Milliseconds 250

        """;

    private const string RestartScript = """

        $state = Join-Path ([IO.Path]::GetDirectoryName($env:MEMORIA_GRAPHICS_RESTART_REQUEST)) 'continuation.ff8state'
        [IO.File]::WriteAllText($state, 'preserved standalone state')
        $hash = (Get-FileHash -LiteralPath $state -Algorithm SHA256).Hash.ToLowerInvariant()
        $receipt = @{schemaVersion=1; processId=$PID; token=$env:MEMORIA_GRAPHICS_RESTART_TOKEN; backend='Auto'; statePath=$state; sha256=$hash} | ConvertTo-Json -Compress
        [IO.File]::WriteAllText($env:MEMORIA_GRAPHICS_RESTART_REQUEST + '.tmp', $receipt)
        [IO.File]::Move($env:MEMORIA_GRAPHICS_RESTART_REQUEST + '.tmp', $env:MEMORIA_GRAPHICS_RESTART_REQUEST)
        """;

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("MEMORIA_FF8_HOST_DIAGNOSTICS_DIRECTORY", previousDiagnostics);
        Directory.Delete(directory, recursive: true);
    }
}
