using System.Diagnostics;
using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class AudioDecodeProcessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutAndCancellationTerminateTheDecoderAndItsChild(bool cancel)
    {
        using var fixture = new DecoderFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<byte[]> running = AudioDecodeProcess.RunAsync(fixture.Start(), null, cancellation.Token, TimeSpan.FromSeconds(cancel ? 30 : 5));
        await WaitUntil(() => fixture.ParentId is not null && fixture.ChildId is not null, "decoder and child PID readiness");
        int parent = fixture.ParentId!.Value;
        int child = fixture.ChildId!.Value;
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
        else
        {
            await Assert.ThrowsAsync<TimeoutException>(() => running);
        }
        await WaitUntil(() => !IsRunning(parent) && !IsRunning(child), $"decoder and child termination (parent {parent}, child {child})");
    }

    private static async Task WaitUntil(Func<bool> condition, string description)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            while (!condition())
            {
                await Task.Delay(10, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"Timed out waiting for {description}.");
        }
    }

    private static bool IsRunning(int id)
    {
        try
        {
            using Process process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private sealed class DecoderFixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria-audio-decoder-" + Guid.NewGuid().ToString("N"));
        private string ParentPath => Path.Combine(directory, "parent.pid");
        private string ChildPath => Path.Combine(directory, "child.pid");
        public int? ParentId => ReadId(ParentPath);
        public int? ChildId => ReadId(ChildPath);

        public ProcessStartInfo Start()
        {
            Directory.CreateDirectory(directory);
            ProcessStartInfo start;
            if (OperatingSystem.IsWindows())
            {
                start = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"));
                foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-Command", """
                    $childStart = New-Object System.Diagnostics.ProcessStartInfo
                    $childStart.FileName = $env:WINDIR + '\System32\ping.exe'
                    $childStart.Arguments = '-t 127.0.0.1'
                    $childStart.UseShellExecute = $false
                    $childStart.CreateNoWindow = $true
                    $childStart.RedirectStandardOutput = $true
                    $child = [Diagnostics.Process]::Start($childStart)
                    # A child PID alone does not establish that its executable has started.
                    if ($child.StandardOutput.BaseStream.ReadByte() -eq -1)
                    {
                        throw 'The decoder child exited before signaling readiness.'
                    }
                    [IO.File]::WriteAllText($env:MEMORIA_TEST_DECODER_CHILD, [string] $child.Id)
                    [IO.File]::WriteAllText($env:MEMORIA_TEST_DECODER_PARENT, [string] $PID)
                    [Console]::OpenStandardOutput().WriteByte(1)
                    Start-Sleep -Seconds 60
                    """ })
                {
                    start.ArgumentList.Add(argument);
                }
            }
            else
            {
                start = new("/bin/sh");
                start.ArgumentList.Add("-c");
                start.ArgumentList.Add("sleep 60 & child=$!; printf '%s' \"$child\" > \"$MEMORIA_TEST_DECODER_CHILD\"; printf '%s' \"$$\" > \"$MEMORIA_TEST_DECODER_PARENT\"; printf x; wait");
            }
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.Environment["MEMORIA_TEST_DECODER_CHILD"] = ChildPath;
            start.Environment["MEMORIA_TEST_DECODER_PARENT"] = ParentPath;
            return start;
        }

        private static int? ReadId(string path)
        {
            return File.Exists(path) && int.TryParse(File.ReadAllText(path), out int id) ? id : null;
        }

        public void Dispose()
        {
            foreach (int id in new[] { ParentId, ChildId }.OfType<int>())
            {
                try
                {
                    using Process process = Process.GetProcessById(id);
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(2000);
                    }
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
            File.Delete(ParentPath);
            File.Delete(ChildPath);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory);
            }
        }
    }
}
