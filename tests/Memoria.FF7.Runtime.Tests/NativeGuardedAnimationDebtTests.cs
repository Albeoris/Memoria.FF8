using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeGuardedAnimationDebtTests
{
    private sealed record Fixture(Ff7AutoPlayActiveEvent Active, FieldVmSnapshot Vm, Ff7FieldProcess Process, Ff7AutoPlayExecutionOwner Owner, byte[] Script);

    internal static void Run()
    {
        Fixture fixture = Create();
        string before = JsonSerializer.Serialize(fixture);
        Check(Settled(fixture), "A returned root's actual guarded held NPC animation was not presentation-settled.");
        Check(Settled(fixture with { Process = fixture.Process with { Actors = [fixture.Process.Actors[0] with { AnimationStatus = 4 }] } }), "An actually completed animation phase was excluded from cosmetic debt.");
        Check(before == JsonSerializer.Serialize(fixture) && fixture.Vm.Entities[1].Priority == 6 && fixture.Active.ExecutionProvenance!.Owners.Contains(fixture.Owner), "Settlement changed native execution, child priority or live provenance.");
        foreach (Fixture invalid in new[]
        {
            fixture with { Active = fixture.Active with { Returned = false } },
            fixture with { Active = fixture.Active with { ConditionFingerprint = "changed" } },
            fixture with { Active = fixture.Active with { ExecutionProvenance = null } },
            fixture with { Active = fixture.Active with { ExecutionProvenance = fixture.Active.ExecutionProvenance! with { RootReturned = false } } },
            fixture with { Vm = fixture.Vm with { ProgramIdentity = "other" } },
            fixture with { Process = fixture.Process with { PlayerLocked = true } },
            fixture with { Process = fixture.Process with { MenuDisabled = true } },
            fixture with { Process = fixture.Process with { Dialogues = [new(9, 0, 0, null, null, 0)] } },
            fixture with { Process = fixture.Process with { Actors = [fixture.Process.Actors[0] with { PlayerControlled = true }] } },
            fixture with { Process = fixture.Process with { Actors = [fixture.Process.Actors[0] with { Animation = 5 }] } },
            fixture with { Process = fixture.Process with { Actors = [fixture.Process.Actors[0] with { AnimationStatus = 2 }] } },
            fixture with { Vm = fixture.Vm with { PendingRequests = [new(9, 1, 6, 16, new FieldAnimation(4, 1, true, true))] } }
        })
        {
            Check(!Settled(invalid), "Unproved, interactive, controlled or outstanding native work became cosmetic debt.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            item => item.Script[10] = 0x30,
            item => item.Script[11] = 0x60,
            item => item.Script[15] = 5,
            item => item.Script[18] = 0,
            item => item.Script[20] = 8,
            item => item.Script[21] = 0x80,
            item => item.Script[10] = 0x80,
            item => item.Script[0] = 0x03,
            item => item.Script[1] = 0,
            item => item.Vm.Entities[1].SyncStates[6] = FieldSyncState.Waiting,
            item => item.Vm.Entities[1].SyncOwners[6] = 0
        })
        {
            Fixture invalid = Create();
            invalidate(invalid);
            Check(!Settled(invalid), "A key gate, changed native branch, effectful footer/producer or synchronous owner was ignored.");
        }
        Check(!Ff7AutoPlayPolicy.CanSettleGuardedNpcAnimation(fixture.Active, 2, fixture.Vm, fixture.Process, fixture.Owner, fixture.Script), "Another generation inherited cosmetic debt settlement.");
        Console.WriteLine("Guarded NPC animation presentation debt checks passed without native return.");
    }

    private static Fixture Create()
    {
        byte[] script = new byte[28];
        new byte[] { 0x02, 1, 0xC2 }.CopyTo(script, 0);
        new byte[] { 0x14, 0x50, 8, 1, 0, 6, 0xBA, 4, 1, 0x12, 9, 0, 0x80, 0x50, 0, 99 }.CopyTo(script, 10);
        FieldEntitySnapshot Entity(int priority, int pc) => new(pc, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 30, true, new byte[1280], new byte[256], [Entity(7, 3), Entity(6, 10)]);
        Ff7AutoPlayExecutionOwner owner = new(1, 6, 2, 10, 0, 1, 0, 20, false);
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 0, 1, Ff7AutoPlayTargetKind.LineTouch, null, 0, "before", Accepted: true, Returned: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, "program", 0, 10) with { RootReturned = true, Owners = [owner] } };
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 1, Model = 0, Animation = 4, AnimationStatus = 3 }] };
        return new(active, vm, process, owner, script);
    }

    private static bool Settled(Fixture fixture) => Ff7AutoPlayPolicy.CanSettleGuardedNpcAnimation(fixture.Active, 1, fixture.Vm, fixture.Process, fixture.Owner, fixture.Script);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
