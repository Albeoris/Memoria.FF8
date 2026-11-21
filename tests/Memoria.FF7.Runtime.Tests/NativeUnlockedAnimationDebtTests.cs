using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeUnlockedAnimationDebtTests
{
    private sealed record Fixture(Ff7AutoPlayActiveEvent Active, FieldVmSnapshot Vm, Ff7FieldProcess Process, Ff7AutoPlayExecutionOwner Owner, byte[] Script);

    internal static void Run()
    {
        Fixture fixture = Create();
        string before = JsonSerializer.Serialize(fixture);
        Check(Settled(fixture), "A real returned root's superseded async player animation was not presentation-settled.");
        Check(fixture.Vm.Entities[1].ProgramCounter == 10 && fixture.Vm.Entities[1].Priority == 6 && fixture.Active.ExecutionProvenance!.Owners.Contains(fixture.Owner), "Presentation settlement fabricated child return or removed native ownership.");
        Check(before == JsonSerializer.Serialize(fixture), "Presentation qualification mutated native state or provenance.");
        foreach (Fixture invalid in new[]
        {
            fixture with { Active = fixture.Active with { Returned = false } },
            fixture with { Active = fixture.Active with { ConditionFingerprint = "other" } },
            fixture with { Active = fixture.Active with { ExecutionProvenance = null } },
            fixture with { Active = fixture.Active with { ExecutionProvenance = fixture.Active.ExecutionProvenance! with { RootReturned = false } } },
            fixture with { Vm = fixture.Vm with { ProgramIdentity = "other" } },
            fixture with { Vm = fixture.Vm with { PendingRequests = [] } },
            fixture with { Process = fixture.Process with { PlayerLocked = true } },
            fixture with { Process = fixture.Process with { MenuDisabled = true } },
            fixture with { Process = fixture.Process with { Dialogues = [new(9, 0, 0, null, null, 0)] } },
            fixture with { Process = fixture.Process with { AnimationRequests = [] } },
            fixture with { Process = fixture.Process with { Actors = [fixture.Process.Actors[0] with { Animation = 8 }] } },
            fixture with { Process = fixture.Process with { Actors = [fixture.Process.Actors[0] with { PlayerControlled = false }] } },
            fixture with { Process = fixture.Process with { Actors = [fixture.Process.Actors[0] with { RequestedAnimation = 8 }] } }
        })
        {
            Check(!Settled(invalid), "Missing ownership, interactive work or a still-owned native animation became cosmetic debt.");
        }
        Check(!Ff7AutoPlayPolicy.CanSettleSupersededPlayerAnimation(fixture.Active, 2, fixture.Vm, fixture.Process, fixture.Owner, fixture.Script), "A different generation inherited presentation settlement.");
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            item => item.Script[15] = 0x80,
            item => item.Script[0] = 0x03,
            item => item.Script[1] = 0,
            item => item.Script[2] = 0xC3,
            item => item.Vm.Entities[1] = item.Vm.Entities[1] with { ProgramCounter = 11 },
            item => item.Vm.Entities[1].SyncStates[6] = FieldSyncState.Waiting,
            item => item.Vm.Entities[1].SyncOwners[6] = 0
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Settled(invalid), "A real footer instruction, wrong producer, mismatched current entry or synchronous debt was discarded.");
        }
        FieldHostRequest pending = fixture.Vm.PendingRequests!.Single();
        Check(!Settled(fixture with { Vm = fixture.Vm with { PendingRequests = [pending with { ProgramCounter = 11 }] } }), "A different pending request PC authenticated cosmetic debt.");
        Check(!Settled(fixture with { Vm = fixture.Vm with { PendingRequests = [pending with { Command = new FieldAnimation(9, 1, true, true, FirstFrame: 11, LastFrame: 19) }] } }), "A different typed pending animation authenticated cosmetic debt.");
        CheckLiteralA3Tail();
        Console.WriteLine("Unlocked superseded player animation debt guards passed without native completion.");
    }

    private static void CheckLiteralA3Tail()
    {
        Fixture fixture = Create();
        new byte[] { 0x33, 1, 0xA3, 8, 1, 0, 0x80, 0x50, 0, 99 }.CopyTo(fixture.Script, 10);
        fixture.Vm.Entities[1] = fixture.Vm.Entities[1] with { ProgramCounter = 12 };
        FieldHostRequest request = new(8, 1, 6, 12, new FieldAnimation(8, 1, true, false));
        fixture = fixture with { Vm = fixture.Vm with { PendingRequests = [request] } };
        string before = JsonSerializer.Serialize(fixture);
        Check(Settled(fixture), "A current literal A3/RET tail after an already executed prefix was not presentation-settled.");
        Check(before == JsonSerializer.Serialize(fixture), "A3 tail qualification changed native execution.");
        Check(!Settled(fixture with { Vm = fixture.Vm with { PendingRequests = [request with { Command = new FieldAnimation(8, 1, true, true) }] } }), "A wrong typed A3 pending command was ignored.");
        fixture.Script[15] = 0x80;
        Check(!Settled(fixture), "An effectful instruction after A3 was mistaken for its RET footer.");
    }

    private static Fixture Create()
    {
        byte[] script = new byte[24];
        script[0] = 0x02;
        script[1] = 1;
        script[2] = 0xC2;
        new byte[] { 0xBC, 8, 11, 19, 1, 0, 0x80, 0x50, 0, 99 }.CopyTo(script, 10);
        FieldEntitySnapshot Entity(int priority, int pc) => new(pc, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldHostRequest pending = new(8, 1, 6, 10, new FieldAnimation(8, 1, true, true, FirstFrame: 11, LastFrame: 19));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 30, true, new byte[1280], new byte[256], [Entity(7, 3), Entity(6, 10)], PendingRequests: [pending]);
        Ff7AutoPlayExecutionOwner owner = new(1, 6, 2, 10, 0, 1, 0, 20, false);
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, Returned: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 0, 10) with { Owners = [owner], RootReturned = true } };
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 1, Model = 0, PlayerControlled = true, Animation = 0, AnimationStatus = 2, FirstAnimationFrame = 11, LastAnimationFrame = 0 }], AnimationRequests = [new(8, 1)] };
        return new(active, vm, process, owner, script);
    }

    private static bool Settled(Fixture fixture) => Ff7AutoPlayPolicy.CanSettleSupersededPlayerAnimation(fixture.Active, 1, fixture.Vm, fixture.Process, fixture.Owner, fixture.Script);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
