using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
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
