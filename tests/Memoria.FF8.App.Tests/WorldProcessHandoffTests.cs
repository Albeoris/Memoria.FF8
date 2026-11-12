using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[CollectionDefinition("World handoff environment", DisableParallelization = true)]
public sealed class WorldHandoffEnvironmentCollection;

[Collection("World handoff environment")]
public sealed class WorldProcessHandoffTests
{
    [Fact]
    public async Task Release_requires_readiness_before_shutting_down()
    {
        using PipePair pipes = await PipePair.ConnectAsync();
        WorldHandoffProtocol source = new(pipes.Server, Task.CompletedTask, pipes.Cancellation.Token);
        Task ready = source.WaitForReadyAsync();
        bool shutdown = false;

        Assert.Throws<InvalidOperationException>(() => source.Release(() => shutdown = true));

        Assert.False(shutdown);
        Assert.False(source.Committed);
        Assert.False(ready.IsCompleted);
        pipes.Client.WriteByte(1);
        await ready.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(source.DestinationReady);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Unexpected_ready_marker_cannot_release_source(int marker)
    {
        using PipePair pipes = await PipePair.ConnectAsync();
        WorldHandoffProtocol source = new(pipes.Server, Task.CompletedTask, pipes.Cancellation.Token);
        Task ready = source.WaitForReadyAsync();

        pipes.Client.WriteByte((byte)marker);

        await Assert.ThrowsAsync<IOException>(() => ready.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Throws<IOException>(source.ThrowIfFailed);
        Assert.False(source.DestinationReady);
        Assert.Throws<InvalidOperationException>(() => source.Release(() => throw new Exception("Must not shut down")));
    }

    [Fact]
    public async Task Destination_disconnect_before_readiness_reports_the_failed_phase()
    {
        using PipePair pipes = await PipePair.ConnectAsync();
        WorldHandoffProtocol source = new(pipes.Server, Task.CompletedTask, pipes.Cancellation.Token);
        Task ready = source.WaitForReadyAsync();

        pipes.Client.Dispose();

        EndOfStreamException failure = await Assert.ThrowsAsync<EndOfStreamException>(() => ready.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal("The destination game host did not acknowledge readiness.", failure.Message);
        Assert.False(source.DestinationReady);
        Assert.False(source.Committed);
    }

    [Fact]
    public async Task Release_shuts_down_before_peer_receives_permission_and_waits_for_cover()
    {
        using PipePair pipes = await PipePair.ConnectAsync();
        WorldHandoffProtocol source = new(pipes.Server, Task.CompletedTask, pipes.Cancellation.Token);
        Task ready = source.WaitForReadyAsync();
        pipes.Client.WriteByte(1);
        await ready.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        byte[] message = new byte[1];
        Task read = pipes.Client.ReadExactlyAsync(message, pipes.Cancellation.Token).AsTask();
        bool shutdown = false;

        source.Release(() =>
        {
            Assert.False(read.IsCompleted);
            shutdown = true;
        });

        await read.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(shutdown);
        Assert.Equal(2, message[0]);
        Assert.True(source.Committed);
        Assert.False(source.IsCovered);
        pipes.Client.WriteByte(3);
        await WaitUntilAsync(() => source.IsCovered);
        Assert.Throws<InvalidOperationException>(() => source.Release(() => throw new Exception("Must not shut down twice")));
        source.ThrowIfFailed();
    }

    [Fact]
    public async Task Shutdown_failure_does_not_send_release_or_commit()
    {
        using PipePair pipes = await PipePair.ConnectAsync();
        WorldHandoffProtocol source = new(pipes.Server, Task.CompletedTask, pipes.Cancellation.Token);
        Task ready = source.WaitForReadyAsync();
        pipes.Client.WriteByte(1);
        await ready.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        byte[] message = new byte[1];
        Task read = pipes.Client.ReadExactlyAsync(message, pipes.Cancellation.Token).AsTask();
        InvalidOperationException failure = new("Steam shutdown failed");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => source.Release(() => throw failure)));

        Assert.False(read.IsCompleted);
        Assert.False(source.Committed);
        Assert.False(source.IsCovered);
        source.Release(() => { });
        await read.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(2, message[0]);
    }

    [Fact]
    public async Task Destination_waits_for_predecessor_exit_after_release_and_cover()
    {
        using PipePair pipes = await PipePair.ConnectAsync();
        TaskCompletionSource peerExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WorldHandoffProtocol destination = new(pipes.Client, peerExited.Task, pipes.Cancellation.Token);
        destination.AcknowledgeReady();
        destination.AcknowledgeReady();
        byte[] message = new byte[1];
        await pipes.Server.ReadExactlyAsync(message, pipes.Cancellation.Token);
        Assert.Equal(1, message[0]);
        Assert.False(destination.ReleaseReceived);
        Assert.False(destination.IsReleased);
        Assert.Throws<InvalidOperationException>(destination.AcknowledgeCover);

        pipes.Server.WriteByte(2);
        await WaitUntilAsync(() => destination.ReleaseReceived);
        Assert.False(destination.IsReleased);
        destination.AcknowledgeCover();
        destination.AcknowledgeCover();
        await pipes.Server.ReadExactlyAsync(message, pipes.Cancellation.Token);
        Assert.Equal(3, message[0]);
        Assert.False(destination.IsReleased);
        destination.ThrowIfFailed();

        peerExited.SetResult();
        await WaitUntilAsync(() => destination.IsReleased);
        destination.ThrowIfFailed();
    }

    [Fact]
    public async Task Predecessor_exit_without_release_does_not_unlock_destination()
    {
        using PipePair pipes = await PipePair.ConnectAsync();
        WorldHandoffProtocol destination = new(pipes.Client, Task.CompletedTask, pipes.Cancellation.Token);
        destination.AcknowledgeReady();
        byte[] message = new byte[1];
        await pipes.Server.ReadExactlyAsync(message, pipes.Cancellation.Token);
        pipes.Server.Dispose();

        Exception failure = await WaitForFailureAsync(destination);

        Assert.IsType<EndOfStreamException>(failure);
        Assert.False(destination.ReleaseReceived);
        Assert.False(destination.IsReleased);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Unexpected_release_marker_never_unlocks_destination(int marker)
    {
        using PipePair pipes = await PipePair.ConnectAsync();
        WorldHandoffProtocol destination = new(pipes.Client, Task.CompletedTask, pipes.Cancellation.Token);
        destination.AcknowledgeReady();
        pipes.Server.WriteByte((byte)marker);

        Assert.IsType<IOException>(await WaitForFailureAsync(destination));
        Assert.False(destination.ReleaseReceived);
        Assert.False(destination.IsReleased);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Unexpected_cover_marker_does_not_confirm_visible_destination(int marker)
    {
        using PipePair pipes = await PipePair.ConnectAsync();
        WorldHandoffProtocol source = new(pipes.Server, Task.CompletedTask, pipes.Cancellation.Token);
        Task ready = source.WaitForReadyAsync();
        pipes.Client.WriteByte(1);
        await ready.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        source.Release(() => { });
        pipes.Client.WriteByte((byte)marker);

        Assert.IsType<IOException>(await WaitForFailureAsync(source));
        Assert.True(source.Committed);
        Assert.False(source.IsCovered);
    }

    [Fact]
    public async Task Cancellation_interrupts_wait_for_predecessor_exit()
    {
        using PipePair pipes = await PipePair.ConnectAsync();
        TaskCompletionSource peerExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WorldHandoffProtocol destination = new(pipes.Client, peerExited.Task, pipes.Cancellation.Token);
        destination.AcknowledgeReady();
        pipes.Server.WriteByte(2);
        await WaitUntilAsync(() => destination.ReleaseReceived);

        pipes.Cancellation.Cancel();

        Assert.IsAssignableFrom<OperationCanceledException>(await WaitForFailureAsync(destination));
        Assert.False(destination.IsReleased);
    }

    [Fact]
    public void Launch_specification_roundtrip_preserves_options_defaults_and_window_placement()
    {
        LaunchOptions options = new()
        {
            GameDirectory = Path.Combine(Path.GetTempPath(), "game resources"),
            Ff7GameDirectory = Path.Combine(Path.GetTempPath(), "other game resources"),
            StatePath = Path.Combine(Path.GetTempPath(), "world transfer", "destination.ffsave"),
            CrossoverStatePath = Path.Combine(Path.GetTempPath(), "world transfer", "destination.ffsave"),
            SettingsDirectory = Path.Combine(Path.GetTempPath(), "shared settings"),
            MuteAudio = true,
            AchievementsLocalOnly = true,
            Width = 1600,
            Height = 900
        };
        WindowTransferRectangle bounds = new(-1600, 40, 1600, 900);
        WindowTransferPlacement placement = new(new(-1400, 80, 1280, 720), true, 2, 3, 4, 5, 6);
        WindowTransferState window = new(true, bounds, placement, placement with { Maximized = false }, 1600, 900, 1600, 900);
        WorldLaunchSpecification specification = new(1, "unique-transfer-pipe", 1234, 5678, options, window);

        string json = JsonSerializer.Serialize(specification);
        WorldLaunchSpecification restored = JsonSerializer.Deserialize<WorldLaunchSpecification>(json)!;

        Assert.Equal(specification.Version, restored.Version);
        Assert.Equal(specification.Pipe, restored.Pipe);
        Assert.Equal(specification.ParentProcess, restored.ParentProcess);
        Assert.Equal(specification.ParentStarted, restored.ParentStarted);
        Assert.Equal(window, restored.Window);
        Assert.Equal(JsonSerializer.Serialize(options), JsonSerializer.Serialize(restored.Options));
        Assert.Equal(options.InitialState.Scene, restored.Options.InitialState.Scene);
        Assert.Equal(options.InitialState.FieldId, restored.Options.InitialState.FieldId);
    }

    [Fact]
    public void Launch_arguments_preserve_literal_specification_and_clear_predecessor_environment()
    {
        string[] cleared = ["SteamAppId", "SteamGameId", "SteamOverlayGameId", "MEMORIA_FF8_HOST_FAILURE_FILE", "MEMORIA_FF8_HOST_READY_FILE"];
        const string inherited = "MEMORIA_WORLD_HANDOFF_TEST_PRESERVED";
        Dictionary<string, string?> previous = cleared.Append(inherited).ToDictionary(key => key, Environment.GetEnvironmentVariable);
        string specification = Path.Combine(Path.GetTempPath(), "world transfer", "quoted \" specification.json");
        try
        {
            foreach (string key in cleared)
            {
                Environment.SetEnvironmentVariable(key, "previous host");
            }
            Environment.SetEnvironmentVariable(inherited, "preserved value");

            ProcessStartInfo start = WorldProcessHandoff.CreateStartInfo(specification);

            Assert.False(start.UseShellExecute);
            Assert.Equal(Environment.ProcessPath, start.FileName);
            Assert.Equal(AppContext.BaseDirectory, start.WorkingDirectory);
            Assert.Equal("--world-handoff", start.ArgumentList[^2]);
            Assert.Equal(specification, start.ArgumentList[^1]);
            Assert.All(cleared, key => Assert.False(start.Environment.ContainsKey(key)));
            Assert.Equal("preserved value", start.Environment[inherited]);
        }
        finally
        {
            foreach ((string key, string? value) in previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static async Task<Exception> WaitForFailureAsync(WorldHandoffProtocol protocol)
    {
        Exception? failure = null;
        await WaitUntilAsync(() => (failure = Record.Exception(protocol.ThrowIfFailed)) is not null);
        return failure!;
    }

    private sealed class PipePair : IDisposable
    {
        internal CancellationTokenSource Cancellation { get; } = new(TimeSpan.FromSeconds(10));
        internal NamedPipeServerStream Server { get; }
        internal NamedPipeClientStream Client { get; }

        private PipePair()
        {
            string name = "Memoria-handoff-test-" + Guid.NewGuid().ToString("N");
            Server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 4096, 4096);
            Client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        }

        internal static async Task<PipePair> ConnectAsync()
        {
            PipePair pair = new();
            try
            {
                Task connected = pair.Server.WaitForConnectionAsync(pair.Cancellation.Token);
                await pair.Client.ConnectAsync(pair.Cancellation.Token);
                await connected;
                return pair;
            }
            catch
            {
                pair.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Cancellation.Cancel();
            Client.Dispose();
            Server.Dispose();
            Cancellation.Dispose();
        }
    }
}
