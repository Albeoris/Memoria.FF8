using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;

internal static class NativeOwnedLadderGraphTests
{
    internal static void Run()
    {
        Ff7AutoPlayTarget root = new("ladder-owner", 1, 2, Ff7AutoPlayTargetKind.LinePush, "condition", false, default);
        foreach (byte mode in new byte[] { 0, 1, 2, 3 })
        {
            ushort held = Ff7AutoplayDriver.ForwardLadderKey(mode);
            Ff7FieldHost host = Host(() => held);
            FieldHostRequest request = new(10, 0, 6, 100, new FieldActorLadder(0, 0, 16, 1, mode, 3, 64, 1));
            host.Execute(request);
            host.AdvanceActors();
            host.AdvanceActors();
            Ff7Actor player = host.GetActor(0);
            Ff7NativeGraphDebt debt = new([root], [false], [new(1, 1, 0, true), new(0, 6, 0, false)], request.Id, 0, 6, LadderRoot: 0);
            Ff7AutoplayDriver.NativeGraphOwnership owner = new(debt, null);
            Ff7AutoplayInput?[] choices = Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, false, [request], owner);
            Check(choices.Length == 3 && choices.Distinct().Count() == 3 && choices[0] is { Held: var forward, Horizontal: 0, Vertical: 0, Talk: false, Run: false } && forward == Ff7AutoplayDriver.ForwardLadderKey(mode) && choices[1] is { Held: var backward } && backward == Ff7AutoplayDriver.BackwardLadderKey(mode) && choices[2] is { Held: 0 }, "Owned ladder choices lost a native direction/pause or injected a different control action.");
            string source = JsonSerializer.Serialize(host.Capture());
            foreach (Ff7AutoplayInput? choice in choices)
            {
                held = choice!.Value.Held;
                Ff7FieldHost branch = Host(() => held, JsonSerializer.Deserialize<Ff7FieldProcess>(source)!);
                branch.AdvanceActors();
                int expected = choice == choices[0] ? 2 : choice == choices[1] ? 0 : 1;
                Check(branch.GetActor(0).Ladder!.Step == expected && !branch.GetActor(0).Ladder!.Completed, "A graph ladder callback did not execute its native forward/backward/neutral step.");
            }
            Check(source == JsonSerializer.Serialize(host.Capture()), "Ladder choice enumeration or detached native callback modified its source.");
            Check(Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, true, [request], owner) is [null], "Auto-forward locked ladder was expanded into controllable direction choices.");
            Check(Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player with { Ladder = player.Ladder! with { Completed = true } }, false, [request], owner) is [null], "Completed ladder created another control branch before native return settlement.");
            Reject(() => Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, false, [request with { Id = 11 }], owner));
            Reject(() => Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, false, [request with { Entity = 2 }], owner));
            Reject(() => Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, false, [request with { Priority = 5 }], owner));
            Reject(() => Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, false, [request with { Command = ((FieldActorLadder)request.Command) with { Mode = (byte)(mode ^ 1) } }], owner));
            Reject(() => Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, false, [request with { Command = ((FieldActorLadder)request.Command) with { X = 1 } }], owner));
            Reject(() => Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, false, [request with { Command = new FieldDialogueCommand(0, 0) }], owner));
            Ff7AutoplayDriver.NativeGraphOwnership wrongRoot = new(debt with { LadderRoot = 1 }, null);
            Reject(() => Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, false, [request], wrongRoot));
            Ff7AutoplayDriver.NativeGraphOwnership returned = new(debt with { Owners = [new(1, 1, 0, true), new(0, 6, 0, true)] }, null);
            Reject(() => Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, false, [request], returned));
            Ff7NativeGraphDebt cold = JsonSerializer.Deserialize<Ff7NativeGraphDebt>(JsonSerializer.Serialize(debt))!;
            Check(Ff7AutoplayDriver.ReadNativeLadderSettlementInputs(player, false, [request], new(cold, null)).SequenceEqual(choices), "Cold owned ladder evidence changed the input basis.");
            held = Ff7AutoplayDriver.ForwardLadderKey(mode);
            Ff7FieldHost finish = Host(() => held, JsonSerializer.Deserialize<Ff7FieldProcess>(source)!);
            while (!finish.GetActor(0).Ladder!.Completed)
            {
                finish.AdvanceActors();
            }
            Check(finish.GetActor(0).Ladder is { Step: 4, Completed: true } && finish.GetActor(0).Triangle == 1, "Native forward branch did not finish at the actual ladder destination.");
            Check(finish.Execute(request).Completed && finish.GetActor(0).Ladder is null, "Native forward ladder request did not acknowledge/remove its process.");
            owner.Returned(new(0, 6, 101, 1));
            Check(owner.IsSettled(true) && !owner.IsSettled(false) && !owner.HasReturnedLadder, "Forward root/child settlement became a backward source-release certificate or ignored free control.");
            Ff7AutoplayDriver.NativeGraphOwnership backwardOwner = new(debt with { BackwardCompleted = true }, null);
            backwardOwner.Returned(new(0, 6, 101, 1));
            Check(backwardOwner.HasReturnedLadder, "Existing exact backward/root/child release evidence was lost.");
            held = Ff7AutoplayDriver.BackwardLadderKey(mode);
            Ff7FieldHost back = Host(() => held, JsonSerializer.Deserialize<Ff7FieldProcess>(source)!);
            while (!back.GetActor(0).Ladder!.Completed)
            {
                back.AdvanceActors();
            }
            Ff7Actor endpoint = back.GetActor(0);
            Check(endpoint.Triangle == player.Triangle && endpoint.Ladder is { Step: 0, Completed: true }, "Native backward settlement did not preserve the source triangle.");
            Ff7AutoplayDriver.NativeGraphOwnership sameTriangle = new(debt with { BackwardCompleted = endpoint.Ladder!.Completed && endpoint.Ladder.Step == 0 }, null);
            sameTriangle.Returned(new(0, 6, 101, 1));
            Check(!sameTriangle.CanReleaseSource(endpoint, [request]), "Completed backward ladder released before its native request/process acknowledgement.");
            Check(back.Execute(request).Completed && back.GetActor(0).Ladder is null, "Backward source request did not acknowledge/remove its native ladder.");
            Check(sameTriangle.IsSettled(true) && sameTriangle.CanReleaseSource(back.GetActor(0), []), "An authenticated backward/root/child/free release incorrectly required changing triangle or decreasing triangle-BFS distance.");
            Check(!sameTriangle.CanReleaseSource(back.GetActor(0), [request]), "Same-triangle release ignored an outstanding matching native request.");
            Check(!owner.CanReleaseSource(back.GetActor(0), []), "Forward completion borrowed same-triangle backward release authority.");
        }
        NativeLadderTests.Run();
        Console.WriteLine("PASS owned native ladder forward/backward/pause, exact request/root/child scope, locked/cold behavior and backward-only source release.");
    }

    private static Ff7FieldHost Host(Func<ushort> held, Ff7FieldProcess? process = null)
    {
        PcWalkmeshTriangle triangle = new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), -1, -1, -1);
        Ff7FieldResources field = new("owned-ladder", null!, null!, null!, [], [triangle, triangle with { Index = 1 }], null!, null!, null!);
        process ??= new() { Actors = [new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true }] };
        return new(field, (_, _) => 3, process) { ReadInputHeld = held, Movement = new([triangle], new short[512], new byte[129]) };
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (NotSupportedException)
        {
            return;
        }
        throw new InvalidOperationException("An unrelated native request borrowed ladder control.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
