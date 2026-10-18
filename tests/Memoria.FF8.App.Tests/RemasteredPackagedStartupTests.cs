using System.Diagnostics;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class RemasteredPackagedStartupTests
{
    [Fact]
    public async Task PackagedGraphicsHostOpensLiveRemasteredStartupMenus()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        string? executable = Environment.GetEnvironmentVariable("MEMORIA_FF8_APP_PATH");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run the optional installed startup check.");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(executable) && File.Exists(executable), "Set MEMORIA_FF8_APP_PATH to the packaged Memoria.FF8.exe to run the optional graphics process check.");
        string? proofDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_PROOF_DIR");
        string root = Path.Combine(string.IsNullOrWhiteSpace(proofDirectory) ? Path.GetTempPath() : Path.GetFullPath(proofDirectory), "remastered-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "requests"));
        ProcessStartInfo start = new(Path.GetFullPath(executable!))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable!))!
        };
        foreach (string argument in new[] { "--game-dir", directory!, "--control-dir", root, "--mute-audio", "--exit-after", "45", "--save-state", Path.Combine(root, "checkpoint.json") })
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = new() { StartInfo = start };
        Task<string> stdout = Task.FromResult(string.Empty);
        Task<string> stderr = Task.FromResult(string.Empty);
        bool started = false;
        try
        {
            Assert.True(process.Start());
            started = true;
            stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            // The queue exercises the real graphics host and live providers; it does not simulate physical title-menu input.
            foreach (string screen in new[] { "new-game-options", "config" })
            {
                JsonElement result = await RequestAsync(process, root, "ui-open", new { Screen = screen, Demo = false });
                Assert.True(result.GetProperty("IsOpen").GetBoolean());
                Assert.False(result.GetProperty("IsDemo").GetBoolean());
                Assert.Equal(screen, result.GetProperty("ScreenId").GetString());
                Assert.NotEmpty(result.GetProperty("Entries").EnumerateArray());
                string capture = Path.Combine(root, screen + ".png");
                await RequestAsync(process, root, "capture", path: capture);
                await WaitForFileAsync(process, capture, png: true);
            }
            await RequestAsync(process, root, "ui-open", new { Screen = "new-game-options", Demo = false });
            await RequestAsync(process, root, "ui-input", new { Input = "Down" });
            await RequestAsync(process, root, "ui-input", new { Input = "Down" });
            JsonElement startedGame = await RequestAsync(process, root, "ui-input", new { Input = "Confirm" });
            Assert.False(startedGame.GetProperty("IsOpen").GetBoolean());
            JsonElement status = await RequestAsync(process, root, "status");
            Stopwatch opening = Stopwatch.StartNew();
            while (status.GetProperty("Scene").GetString() == "Field" && opening.Elapsed < TimeSpan.FromSeconds(20))
            {
                Assert.False(status.GetProperty("FieldFaulted").GetBoolean());
                await Task.Delay(200, TestContext.Current.CancellationToken);
                status = await RequestAsync(process, root, "status");
            }
            Assert.Equal("Movie", status.GetProperty("Scene").GetString());
            Assert.False(status.GetProperty("FieldFaulted").GetBoolean());
            await RequestAsync(process, root, "quit");
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (started)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                // Teardown must finish even when the test has been cancelled.
                await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            }
            await File.WriteAllTextAsync(Path.Combine(root, "stdout.log"), await stdout, CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(root, "stderr.log"), await stderr, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(proofDirectory))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<JsonElement> RequestAsync(Process process, string root, string command, object? ui = null, string? path = null)
    {
        string id = Guid.NewGuid().ToString("N");
        string request = Path.Combine(root, "requests", id + ".json");
        await File.WriteAllTextAsync(request + ".tmp", JsonSerializer.Serialize(new { Id = id, Command = command, Ui = ui, Path = path, Compact = true }), TestContext.Current.CancellationToken);
        File.Move(request + ".tmp", request);
        string response = Path.Combine(root, "responses", id + ".json");
        await WaitForFileAsync(process, response);
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(response, TestContext.Current.CancellationToken));
        JsonElement value = document.RootElement;
        Assert.Equal(id, value.GetProperty("Id").GetString());
        Assert.True(value.GetProperty("Ok").GetBoolean(), $"{command} failed: {value}");
        return value.GetProperty("Result").Clone();
    }

    private static async Task WaitForFileAsync(Process process, string path, bool png = false)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(20))
        {
            if (File.Exists(path) && (!png || IsCompletePng(path)))
            {
                return;
            }
            Assert.False(process.HasExited, $"Packaged application exited before publishing {Path.GetFileName(path)}.");
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        Assert.Fail($"Packaged application did not publish {Path.GetFileName(path)} within 20 seconds.");
    }

    private static bool IsCompletePng(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
            if (stream.Length < 20)
            {
                return false;
            }
            Span<byte> signature = stackalloc byte[8];
            stream.ReadExactly(signature);
            stream.Seek(-12, SeekOrigin.End);
            Span<byte> end = stackalloc byte[12];
            stream.ReadExactly(end);
            return signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && end.SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 });
        }
        catch (IOException)
        {
            return false;
        }
    }
}
