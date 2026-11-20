using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCameraCompletionTests
{
    internal static void Run()
    {
        byte[] script = [0, 0x67, 0xF8, 13, 0];
        int[] saved = new int[8];
        saved[7] = 4;
        FieldEntitySnapshot owner = new(1, 6, 0, saved, new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldHostRequest request = new(1, 0, 6, 1, new FieldCameraCommand(FieldCameraOperation.Wait));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "camera-wait", 1, true, new byte[1280], new byte[256], [owner], PendingRequests: [request]);
        Ff7FieldProcess process = new() { CameraScroll = new(6, 0, 0, 50, 50, 80, 0) };
        string before = JsonSerializer.Serialize(new { vm, process });
        bool Wait(FieldVmSnapshot state, Ff7FieldProcess host, byte[]? code = null) => new Ff7AutoPlayNativeReadiness().ShouldWaitForCameraCompletion(1, "source", state, host, code ?? script);
        Check(Wait(vm, process), "The admitted finite camera wait was not retained before its movie continuation.");
        Check(Wait(vm, process with { CameraScroll = process.CameraScroll! with { Mode = 5 }, CameraTracking = new(1, 0, 0, 0, 0, Completed: true), Movie = new(13, false, 0, false, null) }), "Completed tracking or a prepared movie blocked finite scroll progress.");
        foreach (FieldVmSnapshot bad in new[]
        {
            vm with { PendingRequests = [] },
            vm with { PendingRequests = [request with { Entity = 1 }] },
            vm with { PendingRequests = [request with { Priority = 5 }] },
            vm with { PendingRequests = [request with { ProgramCounter = 2 }] },
            vm with { PendingRequests = [request with { Command = new FieldCameraCommand(FieldCameraOperation.ResetToPlayer) }] },
            vm with { Entities = [owner with { Priority = 7 }] },
            vm with { Entities = [owner with { SavedProgramCounters = new int[8] }] }
        })
        {
            Check(!Wait(bad, process), "An unauthenticated camera waiter retained physical planning.");
        }
        Check(!Wait(vm, process, [0, 0x66, 0, 0, 0]), "A non-wait opcode retained camera readiness.");
        foreach (Ff7FieldProcess bad in new[]
        {
            process with { CameraScroll = null },
            process with { CameraScroll = process.CameraScroll! with { Mode = 1 } },
            process with { CameraScroll = process.CameraScroll! with { Completed = true } },
            process with { CameraScroll = process.CameraScroll! with { Steps = 0 } },
            process with { CameraScroll = process.CameraScroll! with { Step = -1 } },
            process with { CameraScroll = process.CameraScroll! with { Step = 81 } },
            process with { CameraTracking = new(1, 0, 0, 0, 0) },
            process with { Movie = new(13, true, 0, false, 1) },
            process with { GlobalCommand = Ff7GlobalCommand.Movie }
        })
        {
            Check(!Wait(vm, bad), "Unsupported or inactive camera work authenticated finite readiness.");
        }
        Check(Wait(vm, process with { CameraScroll = process.CameraScroll! with { Step = 80 } }), "The terminal interpolation callback was stranded before native completion.");
        Ff7AutoPlayNativeReadiness frozen = new();
        bool waiting = true;
        for (int i = 0; i <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit; i++)
        {
            waiting = frozen.ShouldWaitForCameraCompletion(1, "source", vm with { Tick = i }, process, script);
        }
        Check(!waiting, "A frozen camera waiter retained planning indefinitely.");
        Ff7AutoPlayNativeReadiness reallocated = new();
        for (int i = 0; i <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit; i++)
        {
            waiting = reallocated.ShouldWaitForCameraCompletion(1, "source", vm with { PendingRequests = [request with { Id = i + 1 }] }, process, script);
        }
        Check(!waiting, "New request IDs bypassed the bounds for the same frozen camera owner.");
        Ff7AutoPlayNativeReadiness advancing = new();
        for (int step = 0; step <= 80; step++)
        {
            Check(advancing.ShouldWaitForCameraCompletion(1, "source", vm, process with { CameraScroll = process.CameraScroll! with { Step = step } }, script), "Actual camera progress exhausted the frozen-work allowance.");
        }
        Check(before == JsonSerializer.Serialize(new { vm, process }), "Readiness changed native source facts.");
        Ff7FieldProcess tracked = new() { CameraTracking = new(3, 0, 80, 0, 0), CameraFocusOffset = 0, Actors = [new() { Slot = 0, Entity = 0, Model = 0 }] };
        string trackedBefore = JsonSerializer.Serialize(tracked);
        Check(Wait(vm, tracked) && Wait(vm, tracked with { CameraTracking = tracked.CameraTracking! with { Mode = 2 } }), "A finite native target-camera wait was rejected.");
        Check(Wait(vm, tracked with { CameraTracking = tracked.CameraTracking! with { Step = 80 } }), "The final target-camera interpolation callback was stranded.");
        foreach (Ff7FieldProcess bad in new[]
        {
            tracked with { CameraTracking = tracked.CameraTracking! with { Mode = 1 } },
            tracked with { CameraTracking = tracked.CameraTracking! with { Steps = 0 } },
            tracked with { CameraTracking = tracked.CameraTracking! with { Step = -1 } },
            tracked with { CameraTracking = tracked.CameraTracking! with { Step = 81 } },
            tracked with { CameraTracking = tracked.CameraTracking! with { Slot = 1 } },
            tracked with { CameraFocusOffset = null },
            tracked with { CameraScroll = process.CameraScroll }
        })
        {
            Check(!Wait(vm, bad), "Persistent, invalid or competing target-camera work authenticated finite readiness.");
        }
        Ff7AutoPlayNativeReadiness trackingFrozen = new();
        for (int i = 0; i <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit; i++)
        {
            waiting = trackingFrozen.ShouldWaitForCameraCompletion(1, "source", vm with { Tick = i }, tracked, script);
        }
        Check(!waiting, "A frozen target camera retained planning indefinitely.");
        Ff7AutoPlayNativeReadiness trackingAdvancing = new();
        for (int step = 0; step <= 80; step++)
        {
            Check(trackingAdvancing.ShouldWaitForCameraCompletion(1, "source", vm, tracked with { CameraTracking = tracked.CameraTracking! with { Step = step } }, script), "Actual target-camera progress exhausted frozen readiness.");
        }
        Check(trackedBefore == JsonSerializer.Serialize(tracked), "Target-camera readiness changed source facts.");
        Console.WriteLine("PASS authenticated bounded current camera wait, terminal callback, owner checks and source purity");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CAMERA_COMPLETION_SAVE") ?? throw new InvalidOperationException("Select a native camera fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(stream).Ff7!);
        Check(session.Host.Process.CameraScroll is { Completed: false }, "The fixture has no active camera scroll.");
        Check(session.Host.Process.Movie is null, "The fixture already has a movie preparation.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        int polls = 0;
        bool prepared = false;
        bool cameraCompleted = false;
        while (controller.Running && polls < 64 && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            controller.Advance(1);
            polls++;
            cameraCompleted |= session.Host.Process.CameraScroll?.Completed == true;
            Check(controller.PlanningProgress.PhysicalNodes == 0 && controller.PlanningProgress.DetachedNativeCallbacks == 0, "Current camera wait started physical or detached graph work.");
            if (session.Host.Process.Movie is not null)
            {
                prepared = Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(session.Machine.Capture(), session.Host.Process, session.Field.Scripts.Data.Span);
                break;
            }
        }
        Console.WriteLine($"Original camera continuation: callbacks={controller.Steps}, polls={polls}, tick={session.Machine.Tick}, cameraCompleted={cameraCompleted}, scroll={session.Host.Process.CameraScroll}, movie={session.Host.Process.Movie}, physical={controller.PlanningProgress.PhysicalNodes}, detached={controller.PlanningProgress.DetachedNativeCallbacks}, error={controller.Error}");
        Check(controller.Error is null && prepared && cameraCompleted, "Normal policy did not complete the actual camera and prepare its native movie.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The standalone source changed.");
        Console.WriteLine("PASS original current camera completion and native movie preparation without planning");
    }

    internal static void RunTrackingOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CAMERA_COMPLETION_SAVE") ?? throw new InvalidOperationException("Select a native camera fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(stream).Ff7!);
        Check(session.Host.Process.CameraTracking is { Completed: false, Mode: 2 or 3 }, "The fixture has no finite target-camera wait.");
        long requestId = session.Machine.Capture().PendingRequests!.Single(request => request.Command is FieldCameraCommand { Operation: FieldCameraOperation.Wait }).Id;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        int polls = 0;
        bool completed = false;
        while (controller.Running && polls < 64 && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            controller.Advance(1);
            polls++;
            Check(controller.PlanningProgress.PhysicalNodes == 0 && controller.PlanningProgress.DetachedNativeCallbacks == 0, "Current target-camera wait started physical or detached graph work.");
            completed |= session.Host.Process.CameraTracking?.Completed == true;
            if (completed && !session.Machine.Capture().PendingRequests!.Any(request => request.Id == requestId))
            {
                break;
            }
        }
        Console.WriteLine($"Original target-camera continuation: callbacks={controller.Steps}, polls={polls}, tick={session.Machine.Tick}, tracking={session.Host.Process.CameraTracking}, physical={controller.PlanningProgress.PhysicalNodes}, detached={controller.PlanningProgress.DetachedNativeCallbacks}, error={controller.Error}");
        Check(controller.Error is null && completed, "Normal policy did not complete the actual finite target camera.");
        Check(!session.Machine.Capture().PendingRequests!.Any(request => request.Id == requestId), "The completed target-camera wait did not return to its admitted owner.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The standalone source changed.");
        Console.WriteLine("PASS original finite target-camera completion without planning");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
