using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeInclinedGatewayOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_INCLINED_GATEWAY_SAVE") ?? throw new InvalidOperationException("Select a native control-publication fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        string before = JsonSerializer.Serialize(new { Vm = session.Machine.Capture(), Process = session.Host.Capture(), session.Progress });
        var proof = session.ReadNavigationEntryProof(7, 2, session.Machine.Input);
        if (!proof.Complete || !proof.HasNoEffect || before != JsonSerializer.Serialize(new { Vm = session.Machine.Capture(), Process = session.Host.Capture(), session.Progress }))
        {
            throw new InvalidOperationException(proof.Reason ?? "The native atomic control proof changed live state.");
        }
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the native gateway."));
        bool departed = false;
        session.NativeFieldDeparting += receipt => departed |= receipt is { GatewayIndex: 0, Transition.Map: 66 };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.MapId == 66, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 15 && controller.Steps < 500 && controller.Running && controller.Error is null && !controller.Completed)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native inclined gateway: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, gatewayReceipt={departed}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !controller.Completed || !departed || !SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native inclined gateway did not execute its real field departure.", controller.Failure);
        }
    }
}
