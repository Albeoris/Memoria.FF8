using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeExcavationDigStrategyTests
{
    private const string Program = "CB5DD0DE1FCFDBC2FB36AA8B960EEB05A6E46AED03B256712851B87434A5ADDE";
    private sealed record Fixture(FieldVmSnapshot Vm, Ff7FieldProcess Process);

    internal static void Run()
    {
        foreach (int triangle in new[] { 37, 273 })
        {
            Fixture source = Create(triangle);
            string before = JsonSerializer.Serialize(source);
            Check(Read(source) == triangle, "The dig target did not follow the actual native word and marker triangle.");
            Check(before == JsonSerializer.Serialize(source), "Dig strategy inspection changed native quest flags, placement or execution.");
        }
        foreach (Action<Fixture> invalidate in new Action<Fixture>[]
        {
            source => source.Vm.MapVariables[0x0C] = 2,
            source => source.Vm.MapVariables[0x2B] = 1,
            source => source.Vm.PersistentVariables[0xEB] = 0,
            source => source.Vm.PersistentVariables[0xEA] = 1,
            source => source.Vm.PersistentVariables[0xE7] = 8,
            source => source.Vm.Entities[0] = source.Vm.Entities[0] with { Priority = 6 },
            source => source.Vm.Entities[0] = source.Vm.Entities[0] with { ProgramCounter = 0x06CD },
            source => source.Vm.Entities[2] = source.Vm.Entities[2] with { Priority = 6 },
            source => source.Vm.Entities[2].SavedProgramCounters[6] = 10,
            source => source.Vm.Entities[2].SyncStates[6] = FieldSyncState.Done,
            source => source.Vm.Entities[2].SyncOwners[6] = 0,
            source => source.Vm.Entities[1] = source.Vm.Entities[1] with { PartyCharacter = 3 }
        })
        {
            Fixture invalid = Create(37);
            invalidate(invalid);
            Check(Read(invalid) is null, "Another phase, completed quest or busy native owner admitted a dig target.");
        }
        Fixture fixture = Create(37);
        Check(Read(fixture with { Vm = fixture.Vm with { ProgramIdentity = "unreviewed" } }) is null, "An unreviewed program admitted the native strategy.");
        Ff7Actor marker = fixture.Process.Actors.Single(actor => actor.Entity == 6);
        Ff7Actor player = fixture.Process.Actors.Single(actor => actor.PlayerControlled);
        foreach (Ff7Actor wrong in new[]
        {
            marker with { Entity = 7 },
            marker with { EntityBound = false },
            marker with { PlayerControlled = true },
            marker with { CollisionEnabled = true },
            marker with { Triangle = 38 }
        })
        {
            Ff7Actor[] actors = wrong.PlayerControlled ? [wrong] : [player, wrong];
            Check(Read(fixture with { Process = fixture.Process with { Actors = actors } }) is null, "An unrelated or conflicting actor became the native marker.");
        }
        Check(Read(fixture with { Process = fixture.Process with { Actors = [player] } }) is null, "A missing native marker admitted a target.");
        Check(Read(fixture with { Process = fixture.Process with { Actors = [player, marker, marker with { Slot = 2 }] } }) is null, "Ambiguous native marker bindings admitted a target.");
        Check(Read(fixture with { Process = fixture.Process with { Actors = [player with { Entity = 2 }, marker] } }) is null, "A controlled entity other than native party zero admitted a target.");
        Check(Read(fixture with { Process = fixture.Process with { PlayerLocked = true } }) is null, "Locked native controls admitted a dig target.");
        Check(Read(fixture with { Process = fixture.Process with { MenuDisabled = false } }) is null, "A different native menu phase admitted a dig target.");
        Check(Read(fixture with { Process = fixture.Process with { Transition = new(1, 0, 0, 0, 0) } }) is null, "A field departure admitted another action.");
        for (int address = 0x12; address <= 0x1C; address += 2)
        {
            Fixture duplicate = Create(273);
            duplicate.Vm.MapVariables[address] = 17;
            duplicate.Vm.MapVariables[address + 1] = 1;
            Check(Read(duplicate) is null, "Another native excavation outcome sharing this triangle was treated as the unique harp result.");
        }
        CheckRewardContext();
        Console.WriteLine("Native variable-triangle excavation strategy, unique marker/outcome fences and source purity checks passed.");
    }

    private static void CheckRewardContext()
    {
        FieldVariables Variables(int outcome = 1, int active = 1, int flags = 0)
        {
            FieldVariables variables = new();
            variables.WriteByte(FieldBank.Persistent0Byte, 0xEA, (byte)outcome);
            variables.WriteByte(FieldBank.Persistent0Byte, 0xEB, (byte)active);
            variables.WriteByte(FieldBank.Persistent0Byte, 0xE7, (byte)flags);
            return variables;
        }
        FieldVariables source = Variables();
        byte[] before = source.CopyPersistent();
        const string rewardProgram = "ED04C8C40A6AA8EB9AF8681DC31E804D85176B47540FAAA84FA9D3B087C91E32";
        Check(Ff7AutoPlayDiscovery.IsExcavationRewardContext(rewardProgram, 13, 1, Ff7AutoPlayTargetKind.ActorTalk, source), "The current native harp reward context was excluded.");
        foreach (FieldVariables invalid in new[] { Variables(0), Variables(2), Variables(active: 0), Variables(active: 2), Variables(flags: 8) })
        {
            Check(!Ff7AutoPlayDiscovery.IsExcavationRewardContext(rewardProgram, 13, 1, Ff7AutoPlayTargetKind.ActorTalk, invalid), "Another outcome, inactive quest or already-owned harp inherited the reward adapter.");
        }
        foreach (string wrong in new[] { "unreviewed", Program, "5B48DC271CE35A81BAFA437883DC586FC512427A6F000010BE45722E201AB1DE" })
        {
            Check(!Ff7AutoPlayDiscovery.IsExcavationRewardContext(wrong, 13, 1, Ff7AutoPlayTargetKind.ActorTalk, source), "An unreviewed or alternate resource profile inherited the reward adapter.");
        }
        Check(!Ff7AutoPlayDiscovery.IsExcavationRewardContext(rewardProgram, 12, 1, Ff7AutoPlayTargetKind.ActorTalk, source), "Another actor inherited the reward adapter.");
        Check(!Ff7AutoPlayDiscovery.IsExcavationRewardContext(rewardProgram, 13, 2, Ff7AutoPlayTargetKind.ActorTalk, source), "Another script inherited the reward adapter.");
        Check(!Ff7AutoPlayDiscovery.IsExcavationRewardContext(rewardProgram, 13, 1, Ff7AutoPlayTargetKind.ActorContact, source), "A contact event inherited Talk reward metadata.");
        Check(before.AsSpan().SequenceEqual(source.CopyPersistent()), "Reward context qualification changed native progress flags.");
    }

    private static Fixture Create(int triangle)
    {
        FieldEntitySnapshot[] entities = Enumerable.Range(0, 3).Select(_ => new FieldEntitySnapshot(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray())).ToArray();
        entities[0] = entities[0] with { ProgramCounter = 0x06C9 };
        entities[1] = entities[1] with { ModelBound = true, PartyCharacter = 2 };
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, Program, 1, true, new byte[1280], new byte[256], entities);
        vm.MapVariables[0x0C] = 1;
        vm.MapVariables[0x10] = (byte)(triangle & 255);
        vm.MapVariables[0x11] = (byte)(triangle >> 8);
        vm.PersistentVariables[0xEB] = 1;
        FieldVariables variables = new(vm.PersistentVariables, vm.MapVariables);
        variables.WriteByte(FieldBank.Persistent1Byte, 9, 2);
        vm = vm with { PersistentVariables = variables.CopyPersistent() };
        return new(vm, new() { MenuDisabled = true, Actors = [new() { Slot = 0, Entity = 6, Model = 0, Triangle = triangle, CollisionEnabled = false }, new() { Slot = 1, Entity = 1, Model = 0, PlayerControlled = true }] });
    }

    private static int? Read(Fixture fixture)
    {
        return Ff7AutoPlayScriptedInput.ReadExcavationDigTriangle(fixture.Vm, fixture.Process);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
