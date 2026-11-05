using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ErrorReportSnapshotCaptureTests
{
    [Theory]
    [InlineData(SceneKind.Field)]
    [InlineData(SceneKind.WorldMap)]
    public void Current_snapshot_and_validation_run_on_owner_without_changing_live_session(SceneKind scene)
    {
        int owner = Environment.CurrentManagedThreadId;
        GameSession session = new(new GameState { Scene = scene, FieldId = 180, X = 42, WorldArrival = scene == SceneKind.WorldMap ? new WorldTransition(0, 0, 0) : null });
        GameState original = session.State;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ErrorReportSnapshot result = ErrorReportSnapshotCapture.Capture(SaveBoundaryStatus.Ready, () =>
        {
            Assert.Equal(owner, Environment.CurrentManagedThreadId);
            return session.PrepareSnapshot();
        }, () => throw new InvalidOperationException("Fallback must not run"), state =>
        {
            Assert.Equal(owner, Environment.CurrentManagedThreadId);
            Assert.Equal(42, state.X);
        }, now);

        Assert.Same(original, session.State);
        Assert.NotNull(result.State);
        session.SetPosition(99, 0);
        Assert.Equal(42, result.State.X);
        Assert.Equal("current validated snapshot", result.Kind);
        Assert.Equal(now, result.CapturedUtc);
    }

    [Fact]
    public void Faulted_boundary_never_calls_current_capture_and_labels_validated_earlier_save()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        GameState earlier = new() { Scene = SceneKind.Field, X = 12 };
        int validations = 0;
        ErrorReportSnapshot result = ErrorReportSnapshotCapture.Capture(SaveBoundaryStatus.Faulted, () => throw new InvalidOperationException("Unsafe current capture"), () => [(earlier, now.AddMinutes(-2))], state =>
        {
            Assert.Same(earlier, state);
            validations++;
        }, now);

        Assert.Same(earlier, result.State);
        Assert.Equal(1, validations);
        Assert.Contains("earlier validated", result.Kind);
        Assert.Contains("120 seconds", result.Kind);
        Assert.Contains("Faulted", result.Kind);
        Assert.Contains("reproduction of this failure has not been verified", result.UnavailableReason);
    }

    [Fact]
    public void Current_capture_validation_failure_still_attempts_compatible_earlier_save()
    {
        GameState current = new() { X = 1 };
        GameState incompatible = new() { X = 2 };
        GameState earlier = new() { X = 3 };
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ErrorReportSnapshot result = ErrorReportSnapshotCapture.Capture(SaveBoundaryStatus.Ready, () => current, () => [(incompatible, now.AddMinutes(-1)), (earlier, now.AddMinutes(-2))], state =>
        {
            if (state != earlier)
            {
                throw new NotSupportedException("Unsupported resource continuation");
            }
        }, now);

        Assert.Same(earlier, result.State);
        Assert.Contains("Current snapshot capture/validation failed", result.Kind);
        Assert.Contains("Earlier save continuation validation failed", result.Kind);
    }

    [Fact]
    public void Earlier_quick_save_uses_the_player_loader_and_remains_detached()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-report-save-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "quick.json");
            new GameSession(new GameState { Scene = SceneKind.Field, X = 12 }).Save(path, compact: true);
            int loads = 0;
            PlayerSaveStore store = new(Path.Combine(root, "configured.json"), root, discoverOriginalSaves: false, loadMemoria: loadedPath =>
            {
                Assert.Equal(path, loadedPath);
                loads++;
                return new GameState { Scene = SceneKind.Field, X = 77 };
            });

            (GameState state, DateTimeOffset capturedUtc) = Assert.Single(ErrorReportSnapshotCapture.ReadEarlierPlayerSnapshots(store, null, _ => throw new InvalidOperationException("No configured save"), failure => throw failure));

            Assert.Equal(1, loads);
            Assert.Equal(77, state.X);
            Assert.Equal(new DateTimeOffset(File.GetLastWriteTimeUtc(path)), capturedUtc);
            Assert.Equal(12, GameSession.Load(path).X);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Gameplay_component_quick_save_is_restored_by_the_real_player_loader()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, false);
        string root = Path.Combine(Path.GetTempPath(), "memoria-report-save-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "quick.json");
            using (FileStream destination = File.Create(path))
            {
                GameplaySaveCodec.Write(destination, document);
            }
            byte[] original = File.ReadAllBytes(path);
            Assert.True(original.AsSpan().StartsWith("MFF8GPC1"u8));
            Assert.ThrowsAny<System.Text.Json.JsonException>(() => GameSession.Load(path));
            PlayerSaveStore store = new(Path.Combine(root, "configured.json"), root, discoverOriginalSaves: false, loadMemoria: source => PlayerStateLoader.Load(source, directory, sharedAssets: assets));

            (GameState state, _) = Assert.Single(ErrorReportSnapshotCapture.ReadEarlierPlayerSnapshots(store, null, _ => throw new InvalidOperationException("No configured save"), failure => throw failure));

            Assert.Equal(SceneKind.Field, state.Scene);
            Assert.Equal(document.Resume.FieldId, state.FieldId);
            Assert.NotNull(state.EventState);
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Configured_binary_save_uses_format_aware_loader()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-report-save-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "configured.sav");
            File.WriteAllBytes(path, "MFF8GPC1"u8.ToArray());
            PlayerSaveStore store = new(path, Path.Combine(root, "saves"), discoverOriginalSaves: false);
            GameState expected = new() { Scene = SceneKind.Field, X = 42 };
            int loads = 0;

            (GameState state, _) = Assert.Single(ErrorReportSnapshotCapture.ReadEarlierPlayerSnapshots(store, path, loadedPath =>
            {
                Assert.Equal(path, loadedPath);
                loads++;
                return expected;
            }, failure => throw failure));

            Assert.Same(expected, state);
            Assert.Equal(1, loads);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Failed_save_contributors_return_honest_unavailable_result()
    {
        ErrorReportSnapshot result = ErrorReportSnapshotCapture.Capture(SaveBoundaryStatus.Ready, () => throw new IOException("Snapshot owner failed"), () => throw new IOException("Save file missing"), _ => throw new InvalidOperationException("Must not validate"), DateTimeOffset.UtcNow);

        Assert.Null(result.State);
        Assert.Contains("Snapshot owner failed", result.UnavailableReason);
        Assert.Contains("Save file missing", result.UnavailableReason);
        Assert.Contains("No validated reproduction save", result.UnavailableReason);
    }
    [Fact]
    public void Vm_fault_retains_typed_source_and_real_stack_before_reporting()
    {
        byte[] program = new byte[28];
        program[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(12), 3);
        BinaryPrimitives.WriteInt32LittleEndian(program.AsSpan(16), (int)Jsm.Opcode.LBL);
        BinaryPrimitives.WriteInt32LittleEndian(program.AsSpan(20), (int)Jsm.Opcode.WAIT);
        BinaryPrimitives.WriteInt32LittleEndian(program.AsSpan(24), (int)Jsm.Opcode.RET);
        EventEngine engine = new(new GameSession(new GameState()));
        engine.LoadField(100, program, []);

        EventScriptException exception = Assert.Throws<EventScriptException>(() => engine.Step());

        Assert.NotNull(exception.FaultContext);
        Assert.Equal(100, exception.FaultContext.FieldId);
        Assert.Equal(0, exception.FaultContext.ObjectId);
        Assert.Equal(0, exception.FaultContext.ScriptId);
        Assert.Equal(1, exception.FaultContext.Operation);
        Assert.Equal("WAIT", exception.FaultContext.Opcode);
        Assert.Empty(exception.FaultContext.Operands);
        Assert.Same(exception.FaultContext, engine.LatestFaultContext);
        Assert.Contains("EventEngine", exception.StackTrace);
    }

}
