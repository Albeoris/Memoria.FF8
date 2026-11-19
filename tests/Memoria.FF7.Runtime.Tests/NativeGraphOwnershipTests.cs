using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeGraphOwnershipTests
{
    internal static void Run()
    {
        Ff7AutoPlayTarget root = new("contact", 13, 5, Ff7AutoPlayTargetKind.LineEnter, "source", false, default, MayChangeNavigation: true);
        Ff7AutoplayDriver.NativeGraphOwnership ownership = new(null, root);
        Check(!ownership.HasDebt && ownership.Allows(13, 5) && !ownership.Allows(14, 5), "A graph event borrowed another contact's admission.");
        Check(ownership.Requested(13, 5, true) && ownership.HasDebt, "Accepted native contact did not establish its exact root.");
        ownership.Dispatched(new(13, 1, 100, 17, 8, 6, 1, true, false, 1));
        ownership.Returned(new(13, 1, 101, 1));
        Check(!ownership.IsSettled(true) && ownership.OwnsInput(17, 6) && !ownership.OwnsInput(18, 6) && !ownership.Allows(13, 5), "Root RET completed a live child or rearmed another root entry.");
        Ff7NativeGraphDebt pending = ownership.Capture()!;
        Ff7AutoplayDriver.NativeGraphOwnership restored = new(pending, null);
        Check(JsonSerializer.Serialize(pending) == JsonSerializer.Serialize(restored.Capture()) && !restored.IsSettled(true), "Cold graph supervisor state lost native child ownership.");
        restored.Returned(new(17, 6, 120, 2));
        Check(!restored.IsSettled(false) && restored.IsSettled(true) && !restored.HasReturnedLadder, "Completed knockback was confused with a ladder source-release proof or free control was not required.");
        Ff7AutoplayDriver.NativeGraphOwnership replaced = new(pending, null);
        replaced.Dispatched(new(99, 7, 10, 17, 9, 6, 1, true, false, 2));
        Check(replaced.Invalid, "An unrelated native dispatch replaced a debt owner without invalidating it.");
        Ff7NativeGraphDebt recovery = new([root], [false], [new(13, 1, 0, true), new(17, 6, 0, true)], 50, 17, 6, true);
        Check(new Ff7AutoplayDriver.NativeGraphOwnership(recovery, null).HasReturnedLadder, "Completed native backward/root/child receipts lost source-release eligibility.");
        Check(!new Ff7AutoplayDriver.NativeGraphOwnership(recovery with { BackwardCompleted = false }, null).HasReturnedLadder, "A forward or incomplete ladder acquired backward-release authority.");
        Check(!new Ff7AutoplayDriver.NativeGraphOwnership(recovery with { Owners = [new(13, 1, 0, false), new(17, 1, 0, true), new(17, 6, 0, true)] }, null).HasReturnedLadder, "A child's priority1 RET replaced the exact root RET.");
        Check(Ff7AutoplayDriver.ReadNavigationRunModes(true, null).SequenceEqual([true, false]) && Ff7AutoplayDriver.ReadNavigationRunModes(true, false).SequenceEqual([false]) && Ff7AutoplayDriver.ReadNavigationRunModes(false, true).SequenceEqual([true]), "Native graph run/walk choices were merged into a greedy speed choice.");
        CheckForcedForest(root);
        CheckExternalAdmissions();
        CheckExactNativeIdentity(pending, recovery);
        Console.WriteLine("Native graph owner lineage, settlement, ladder evidence, exact identity and speed checks passed.");
    }

    private static void CheckExternalAdmissions()
    {
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        FieldProgram program = new([0, 0x80, 0x50, 0, 9, 0], [entries]);
        FieldMachine machine = new(program);
        machine.Initialize();
        Ff7AutoPlayTarget first = new("empty-enter", 0, 5, Ff7AutoPlayTargetKind.LineEnter, "condition", false, default);
        Ff7AutoPlayTarget second = first with { Id = "empty-touch", Script = 4, Kind = Ff7AutoPlayTargetKind.LineTouch };
        Ff7AutoplayDriver.NativeGraphOwnership empty = new(new([first, second], [true, true], []), null);
        string before = JsonSerializer.Serialize(machine.Capture());
        foreach (Ff7AutoPlayTarget target in new[] { first, second })
        {
            FieldExternalScriptAdmission receipt = machine.RequestExternalScript(target.Entity, target.Script);
            Check(receipt is { Accepted: true, Started: false }, "An immediate RET was reported as a new native execution.");
            empty.Requested(receipt.Entity, receipt.Script, receipt.Accepted, receipt.Started);
        }
        Check(!empty.Invalid && !empty.HasDebt && empty.Capture() is null && !empty.HasReturnedLadder && before == JsonSerializer.Serialize(machine.Capture()), "Empty queued admissions invented a live owner, recovery evidence or native mutation.");
        foreach (FieldSyncState sync in new[] { FieldSyncState.Waiting, FieldSyncState.Done })
        {
            FieldVmSnapshot snapshot = machine.Capture();
            snapshot.Entities[0].SyncStates[1] = sync;
            snapshot.Entities[0].SyncOwners[1] = 0;
            FieldMachine synchronized = FieldMachine.Restore(program, snapshot);
            string synchronizedBefore = JsonSerializer.Serialize(synchronized.Capture());
            Check(synchronized.RequestExternalScript(0, 1) is { Accepted: true, Started: false } && synchronizedBefore == JsonSerializer.Serialize(synchronized.Capture()), "An existing sync acknowledgement created a second native lifetime.");
        }
        Ff7AutoPlayTarget real = first with { Id = "real", Script = 1, Kind = Ff7AutoPlayTargetKind.LineTalk };
        Ff7AutoplayDriver.NativeGraphOwnership owner = new(null, real);
        FieldExternalScriptAdmission started = machine.RequestExternalScript(0, 1);
        Check(started is { Accepted: true, Started: true } && machine.Capture().Entities[0].Priority == 1, "A real admitted entry lost its native execution receipt.");
        owner.Requested(started.Entity, started.Script, started.Accepted, started.Started);
        FieldExternalScriptAdmission busy = machine.RequestExternalScript(0, 1);
        owner.Requested(busy.Entity, busy.Script, busy.Accepted, busy.Started);
        Check(busy is { Accepted: false, Started: false } && !owner.Invalid && owner.OwnsInput(0, 1), "A busy native slot created another owner lifetime.");
        machine.ScriptReturned += owner.Returned;
        machine.AdvanceTick();
        Check(owner.IsSettled(true) && !owner.HasReturnedLadder, "A real native RET failed to settle its exact owner or borrowed recovery authority.");
        Ff7AutoplayDriver.NativeGraphOwnership collision = new(null, real);
        collision.Requested(0, 1, true, true);
        collision.Requested(0, 1, true, true);
        Check(collision.Invalid, "Two genuinely started lifetimes replaced the same live slot without rejection.");
    }

    private static void CheckForcedForest(Ff7AutoPlayTarget root)
    {
        Ff7AutoplayDriver.NativeGraphOwnership forest = new(null, root);
        forest.Requested(root.Entity, root.Script, true);
        forest.Dispatched(new(13, 1, 100, 17, 8, 6, 1, true, false, 1));
        forest.Returned(new(13, 1, 101, 1));
        Ff7AutoPlayTarget second = root with { Id = "second", Entity = 14 };
        forest.AdoptForcedRoot(second);
        forest.Requested(14, 5, true);
        forest.Dispatched(new(14, 1, 110, 17, 8, 6, 1, false, false, 2));
        forest.Returned(new(14, 1, 111, 2));
        Check(!forest.Invalid && !forest.IsSettled(true) && forest.OwnsInput(17, 6), "A rejected busy-player child request replaced the original forced owner.");
        forest.AdoptForcedRoot(root);
        forest.Requested(13, 5, true);
        forest.Returned(new(13, 1, 120, 3));
        Check(!forest.Invalid && forest.Capture()!.Roots.Length == 3 && !forest.IsSettled(true), "A repeated forced root lost its separate native lifetime or completed the original child.");
        forest.Returned(new(17, 6, 130, 4));
        Check(forest.IsSettled(true) && !forest.Allows(13, 5) && !forest.Allows(14, 5) && !forest.HasReturnedLadder, "Settled forced knockbacks retained contact permission or acquired source-release authority.");
        Ff7AutoplayDriver.NativeGraphOwnership restored = new(forest.Capture(), null);
        Check(restored.IsSettled(true) && !restored.Allows(13, 5), "A captured debt forest borrowed returned-root permission after restore.");
    }

    private static void CheckExactNativeIdentity(Ff7NativeGraphDebt pending, Ff7NativeGraphDebt recovery)
    {
        FieldMachine machine = new(new FieldProgram([0, 0], [new ushort[32]]));
        machine.Initialize();
        Ff7SessionSave native = new() { ResourceProfile = "authored-graph", Language = "en", FieldName = "authored", FieldGeneration = 1, Progress = new() { InitialKernelData = [1], Characters = [], Party = [255, 255, 255], Inventory = [], Materia = [], Clock = new(ElapsedSeconds: 2) }, Vm = machine.Capture(), Process = new() };
        string supervisor = JsonSerializer.Serialize(new { Debt = pending, Recovery = (Ff7NativeGraphDebt?)null });
        string exact = Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(native, supervisor);
        byte[] publication = native.Vm.PersistentVariables.ToArray();
        publication[16] ^= 1;
        foreach (Ff7SessionSave changed in new[] { native with { Vm = native.Vm with { Tick = native.Vm.Tick + 1 } }, native with { Vm = native.Vm with { PersistentVariables = publication } }, native with { Progress = native.Progress with { Clock = new(ElapsedSeconds: 3) } }, native with { Progress = native.Progress with { FieldRandomIndex = 1 } } })
        {
            Check(exact != Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(changed, supervisor), "A native tick, clock publication, elapsed time or RNG change was erased from visited identity.");
        }
        Check(exact != Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(native, JsonSerializer.Serialize(new { Debt = pending, Recovery = recovery })), "Completed recovery evidence was erased from graph node identity.");
        Check(exact != Ff7AutoplayDriver.ReadExactNativeGraphFingerprint(native, JsonSerializer.Serialize(new { Debt = pending with { Owners = [new(13, 1, 0, true)] }, Recovery = (Ff7NativeGraphDebt?)null })), "A distinct native ownership debt was erased from visited identity.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
