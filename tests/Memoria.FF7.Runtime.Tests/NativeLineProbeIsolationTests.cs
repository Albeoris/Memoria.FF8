using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLineProbeIsolationTests
{
    internal static void Run()
    {
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, X4096 = 100 << 12, Y4096 = 100 << 12, CollisionRadius = 10 };
        Ff7Line line = new(0, 2, 0, 0, 0, 50, 0, 0, Push: true);
        Ff7FieldResources field = new("line-probe-fixture", null!, null!, null!, [], [], null!, null!, null!);
        Ff7FieldHost host = new(field, (_, _) => 1, new() { MapJumpDisabled = true, Actors = [player], Lines = [line] });
        FieldMachine machine = new(new FieldProgram([0, 0], [new ushort[32]]));
        machine.Initialize();
        Ff7Session session = (Ff7Session)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Session));
        typeof(Ff7Session).GetProperty(nameof(Ff7Session.Host))!.SetValue(session, host);
        typeof(Ff7Session).GetProperty(nameof(Ff7Session.Machine))!.SetValue(session, machine);
        Ff7AutoplayDriver driver = new(session);
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo rejected = typeof(Ff7AutoplayDriver).GetField("navigationLineRejected", Flags)!;
        FieldInputState input = new();
        Ff7AutoPlayTarget target = new("protected-line", 2, 2, Ff7AutoPlayTargetKind.LinePush, "condition", false, default, MayChangeNavigation: true);
        typeof(Ff7AutoplayDriver).GetField("navigationLineTargets", Flags)!.SetValue(driver, new[] { target });
        typeof(Ff7AutoplayDriver).GetField("navigationLineInput", Flags)!.SetValue(driver, input);
        var skips = (Dictionary<(int Entity, int Script, FieldInputState Input, bool Talk), bool>)typeof(Ff7AutoplayDriver).GetField("navigationLineSkips", Flags)!.GetValue(driver)!;
        skips.Add((2, 2, input, false), false);
        Ff7NativeMoveResult move = new(player with { X4096 = 110 << 12 }, true, false, []);
        string native = JsonSerializer.Serialize(new { Process = host.Capture(), Vm = machine.Capture() });
        foreach (bool prior in new[] { false, true })
        {
            rejected.SetValue(driver, prior);
            Check(!driver.ProbePlannedMove(player, move), "The diagnostic probe bypassed an actual pending protected Push.");
            Check((bool)rejected.GetValue(driver)! == prior, "A diagnostic rejection contaminated the current candidate's retained rejection state.");
        }
        rejected.SetValue(driver, false);
        bool actual = (bool)typeof(Ff7AutoplayDriver).GetMethod("AllowsPlannedMove", Flags)!.Invoke(driver, [player, move])!;
        Check(!actual && (bool)rejected.GetValue(driver)!, "The real candidate guard lost its rejection signal when adding the isolated probe.");
        Check(native == JsonSerializer.Serialize(new { Process = host.Capture(), Vm = machine.Capture() }), "Guard evaluation changed native process, actor, input or VM facts.");

        typeof(Ff7FieldHost).GetProperty(nameof(Ff7FieldHost.Process))!.SetValue(host, host.Process with { Lines = [line with { Push = false }] });
        native = JsonSerializer.Serialize(new { Process = host.Capture(), Vm = machine.Capture() });
        foreach (bool prior in new[] { false, true })
        {
            rejected.SetValue(driver, prior);
            Check(driver.ProbePlannedMove(player, move) && (bool)rejected.GetValue(driver)! == prior, "An allowed diagnostic move changed the retained rejection state.");
        }
        Check(native == JsonSerializer.Serialize(new { Process = host.Capture(), Vm = machine.Capture() }), "An allowed diagnostic probe changed native state.");
        Console.WriteLine("Native protected LINE probes preserve caller rejection state while real movement guards remain side effectful.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
