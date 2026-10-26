using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MusicWorkerOutputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusResumePreservesAnExistingNativeMusicPause(bool nativePaused)
    {
        using HelperFixture fixture = new("normal");
        using MusicWorkerOutput music = new(fixture.StartInfo(), TimeSpan.FromSeconds(5));
        using FieldAudioPresentation presentation = new(0, "", "");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(FieldAudioPresentation).GetField("music", flags)!.SetValue(presentation, music);
        if (nativePaused)
        {
            typeof(FieldAudioPresentation).GetMethod("PauseMusicOutput", flags)!.Invoke(presentation, null);
        }
        presentation.SetFocusPaused(true);
        presentation.SetFocusPaused(true);
        presentation.SetFocusPaused(false);
        presentation.SetFocusPaused(false);
        Assert.True(music.IsPlaying);
        string[] commands = File.ReadAllLines(fixture.Commands).Select(line => JsonSerializer.Deserialize<JsonElement>(line).GetProperty("Command").GetString()!).ToArray();
        Assert.Equal(nativePaused ? ["Pause", "Pause", "Playing"] : new[] { "Pause", "Resume", "Playing" }, commands);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FailedBattleMusicCannotEscapePresentationAndStoppingReleasesTheBackend(bool selectedSlot, bool battleStop)
    {
        using HelperFixture failedFixture = new("error");
        using MusicWorkerOutput failed = new(failedFixture.StartInfo(), TimeSpan.FromSeconds(5));
        failed.Play("segment.sgt", -10000);
        await WaitUntil(() => failed.Error is not null);
        using FieldAudioPresentation presentation = new(0, "", "");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var musicField = typeof(FieldAudioPresentation).GetField("music", flags)!;
        var pausedField = typeof(FieldAudioPresentation).GetField("nativeMidiPaused", flags)!;
        var rampField = typeof(FieldAudioPresentation).GetField("midiRamp", flags)!;
        musicField.SetValue(presentation, failed);
        pausedField.SetValue(presentation, true);
        rampField.SetValue(presentation, new FieldAudioPresentation.Ramp(127, 0, 1000));
        int[] ids = (int[])typeof(FieldAudioPresentation).GetField("musicIds", flags)!.GetValue(presentation)!;
        Array kinds = (Array)typeof(FieldAudioPresentation).GetField("musicKinds", flags)!.GetValue(presentation)!;
        if (selectedSlot)
        {
            kinds.SetValue(Enum.Parse(kinds.GetType().GetElementType()!, "Midi"), 0);
            ids[0] = 26;
        }
        NativeBattleMusicCommandReceipt resume = new(3, GainChannel: 1, Gain: 127, ResumeOutput: true);

        Assert.Null(Record.Exception(() => presentation.PlayBattleMusicCommand(resume)));
        Assert.Equal(failed.Error!.Message, presentation.Error);
        if (battleStop)
        {
            Assert.Null(Record.Exception(() => presentation.PlayBattleMusicCommand(new(1))));
        }
        Assert.Null(Record.Exception(presentation.StopAll));
        Assert.Null(musicField.GetValue(presentation));
        Assert.False((bool)pausedField.GetValue(presentation)!);
        Assert.Null(rampField.GetValue(presentation));
        Assert.Equal(new[] { -1, -1 }, ids);
        Assert.All(kinds.Cast<object>(), kind => Assert.Equal("None", kind.ToString()));
        Assert.Throws<ObjectDisposedException>(failed.Resume);
        Assert.Null(Record.Exception(() => presentation.PlayBattleMusicCommand(new(1))));
        await failedFixture.WaitExited();

        using HelperFixture healthyFixture = new("normal");
        using MusicWorkerOutput healthy = new(healthyFixture.StartInfo(), TimeSpan.FromSeconds(5));
        musicField.SetValue(presentation, healthy);
        presentation.PlayBattleMusicCommand(resume);
        Assert.True(healthy.IsPlaying);
        string[] commands = File.ReadAllLines(healthyFixture.Commands).Select(line => JsonSerializer.Deserialize<JsonElement>(line).GetProperty("Command").GetString()!).ToArray();
        Assert.Equal(new[] { "Resume", "Playing" }, commands);
    }

    [Fact]
    public async Task StartupTimeoutDoesNotBlockGameplayOrRestartHelper()
    {
        using HelperFixture fixture = new("startup");
        Stopwatch elapsed = Stopwatch.StartNew();
        using MusicWorkerOutput output = new(fixture.StartInfo(), TimeSpan.FromSeconds(3));
        output.Play("segment.sgt", -10000);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), "Music startup blocked the caller.");
        await fixture.WaitStarted();
        await WaitUntil(() => output.Error is not null);
        Assert.IsType<TimeoutException>(output.Error);
        Assert.Same(output.Error, Assert.Throws<TimeoutException>(() => output.Play("second.sgt", -10000)));
        await fixture.WaitExited();
        Assert.Single(File.ReadAllLines(fixture.Launches));
    }

    [Fact]
    public async Task FullInputPipeTimesOutWithoutBlockingGameplay()
    {
        using HelperFixture fixture = new("write");
        using MusicWorkerOutput output = new(fixture.StartInfo(), TimeSpan.FromSeconds(3));
        await fixture.WaitStarted();
        Stopwatch elapsed = Stopwatch.StartNew();
        output.Play(new string('x', 4 * 1024 * 1024), -10000);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), "A full command pipe blocked the caller.");
        await WaitUntil(() => output.Error is not null);
        Assert.IsType<TimeoutException>(output.Error);
        await fixture.WaitExited();
        Assert.Single(File.ReadAllLines(fixture.Launches));
    }

    [Fact]
    public async Task MissingCommandReplyTimesOutAndStopsHelper()
    {
        using HelperFixture fixture = new("reply");
        using MusicWorkerOutput output = new(fixture.StartInfo(), TimeSpan.FromSeconds(3));
        await fixture.WaitStarted();
        output.Play("segment.sgt", -10000);
        await WaitUntil(() => File.Exists(fixture.Commands));
        await WaitUntil(() => output.Error is not null);
        Assert.IsType<TimeoutException>(output.Error);
        await fixture.WaitExited();
        Assert.Single(File.ReadAllLines(fixture.Launches));
    }

    [Fact]
    public async Task CommandsPreserveProtocolOrderAndDisposeStopsHelper()
    {
        using HelperFixture fixture = new("normal");
        using MusicWorkerOutput output = new(fixture.StartInfo(), TimeSpan.FromSeconds(5));
        output.Play("segment.sgt", -10000, 17);
        output.Volume(-5000);
        output.Pause();
        output.Resume();
        output.Stop();
        Assert.True(output.IsPlaying);
        Assert.Null(output.Error);
        string[] commands = File.ReadAllLines(fixture.Commands);
        Assert.Equal(new[] { "Play", "Volume", "Pause", "Resume", "Stop", "Playing" }, commands.Select(line => JsonSerializer.Deserialize<JsonElement>(line).GetProperty("Command").GetString()));
        using JsonDocument play = JsonDocument.Parse(commands[0]);
        Assert.Equal("segment.sgt", play.RootElement.GetProperty("Path").GetString());
        Assert.Equal(-10000, play.RootElement.GetProperty("Volume").GetInt32());
        Assert.Equal(17, play.RootElement.GetProperty("StartPoint").GetInt32());
        Stopwatch elapsed = Stopwatch.StartNew();
        output.Dispose();
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3), "Music shutdown exceeded its bound.");
        await fixture.WaitExited();
        Assert.Single(File.ReadAllLines(fixture.Launches));
    }

    [Fact]
    public async Task BoundedQueueFailsPromptlyAndDisposeInterruptsStartup()
    {
        using HelperFixture fixture = new("startup");
        using MusicWorkerOutput output = new(fixture.StartInfo(), TimeSpan.FromSeconds(30), capacity: 1);
        await fixture.WaitStarted();
        output.Play("segment.sgt", -10000);
        Stopwatch elapsed = Stopwatch.StartNew();
        Assert.Throws<InvalidOperationException>(() => output.Volume(-5000));
        Assert.NotNull(output.Error);
        output.Dispose();
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3), "Queue failure or shutdown blocked the caller.");
        await fixture.WaitExited();
        Assert.Single(File.ReadAllLines(fixture.Launches));
    }

    [Fact]
    public async Task DisposeInterruptsBlockedStartupBeforeItsDeadline()
    {
        using HelperFixture fixture = new("startup");
        using MusicWorkerOutput output = new(fixture.StartInfo(), TimeSpan.FromSeconds(30));
        await fixture.WaitStarted();
        output.Play("segment.sgt", -10000);
        Stopwatch elapsed = Stopwatch.StartNew();
        output.Dispose();
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3), "Shutdown waited for the startup deadline.");
        await fixture.WaitExited();
        Assert.Null(output.Error);
        Assert.Throws<ObjectDisposedException>(() => output.Resume());
    }

    [Theory]
    [InlineData("error", typeof(InvalidOperationException))]
    [InlineData("malformed", typeof(JsonException))]
    [InlineData("closed", typeof(IOException))]
    public async Task InvalidRepliesRetainOneFailureAndStopHelper(string mode, Type exceptionType)
    {
        using HelperFixture fixture = new(mode);
        using MusicWorkerOutput output = new(fixture.StartInfo(), TimeSpan.FromSeconds(5));
        await fixture.WaitStarted();
        output.Play("segment.sgt", -10000);
        await WaitUntil(() => output.Error is not null);
        Assert.IsAssignableFrom(exceptionType, output.Error);
        Assert.Same(output.Error, Record.Exception(output.Resume));
        await fixture.WaitExited();
        Assert.Single(File.ReadAllLines(fixture.Launches));
    }

    [Fact]
    public async Task StartFailureIsReportedWithoutRetryingOnCaller()
    {
        ProcessStartInfo start = new(Path.Combine(Path.GetTempPath(), "Memoria-missing-" + Guid.NewGuid().ToString("N"), "helper.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
        Stopwatch elapsed = Stopwatch.StartNew();
        using MusicWorkerOutput output = new(start, TimeSpan.FromSeconds(3));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), "Starting the helper blocked the caller.");
        await WaitUntil(() => output.Error is not null);
        Assert.IsType<System.ComponentModel.Win32Exception>(output.Error);
        Assert.Same(output.Error, Record.Exception(output.Resume));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(20);
        }
        Assert.True(condition(), "The music helper did not reach the expected state within ten seconds.");
    }

    private sealed class HelperFixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria-music-ipc-" + Guid.NewGuid().ToString("N"));
        private readonly string mode;
        private readonly string script;
        private int? processId;

        public HelperFixture(string mode)
        {
            this.mode = mode;
            Directory.CreateDirectory(directory);
            script = Path.Combine(directory, "helper.ps1");
            File.WriteAllText(script, """
                param([string] $Directory, [string] $Mode)
                [IO.File]::AppendAllText([IO.Path]::Combine($Directory, 'launches.txt'), "started`n")
                if ($Mode -ne 'startup') {
                    [Console]::WriteLine('{"Result":true}')
                    [Console]::Out.Flush()
                }
                [IO.File]::WriteAllText([IO.Path]::Combine($Directory, 'pid.txt'), [string] $PID)
                if ($Mode -eq 'startup' -or $Mode -eq 'write') {
                    Start-Sleep -Seconds 60
                    exit
                }
                while ($null -ne ($Line = [Console]::ReadLine())) {
                    [IO.File]::AppendAllText([IO.Path]::Combine($Directory, 'commands.txt'), $Line + "`n")
                    if ($Mode -eq 'reply') {
                        Start-Sleep -Seconds 60
                        exit
                    }
                    if ($Mode -eq 'closed') {
                        exit
                    }
                    if ($Mode -eq 'error') {
                        [Console]::WriteLine('{"Error":"fixture failure"}')
                        [Console]::Out.Flush()
                        continue
                    }
                    if ($Mode -eq 'malformed') {
                        [Console]::WriteLine('invalid json')
                        [Console]::Out.Flush()
                        continue
                    }
                    [Console]::WriteLine('{"Result":true}')
                    [Console]::Out.Flush()
                }
                """);
        }

        public string Launches => Path.Combine(directory, "launches.txt");
        public string Commands => Path.Combine(directory, "commands.txt");

        public ProcessStartInfo StartInfo()
        {
            string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            ProcessStartInfo start = new(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
            foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, directory, mode })
            {
                start.ArgumentList.Add(argument);
            }
            return start;
        }

        public async Task WaitStarted()
        {
            string pid = Path.Combine(directory, "pid.txt");
            await WaitUntil(() => File.Exists(pid) && int.TryParse(File.ReadAllText(pid), out _));
            processId = int.Parse(File.ReadAllText(pid), System.Globalization.CultureInfo.InvariantCulture);
        }

        public async Task WaitExited()
        {
            if (processId is null)
            {
                await WaitStarted();
            }
            await WaitUntil(() => !IsRunning(processId!.Value));
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

        public void Dispose()
        {
            string pid = Path.Combine(directory, "pid.txt");
            if (processId is null && File.Exists(pid) && int.TryParse(File.ReadAllText(pid), out int id))
            {
                processId = id;
            }
            if (processId is int running && IsRunning(running))
            {
                using Process process = Process.GetProcessById(running);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
            }
            Directory.Delete(directory, recursive: true);
        }
    }
}
