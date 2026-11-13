using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldChocoboTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP FFVII yellow Chocobo original resources");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        byte[] variables = new byte[1280];
        BinaryPrimitives.WriteUInt16LittleEndian(variables, 386);
        variables[0x54] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(variables.AsSpan(0x3B8), 228926);
        BinaryPrimitives.WriteUInt32LittleEndian(variables.AsSpan(0x3BC), 141315);
        variables[0x3FF] = unchecked((byte)-variables.Skip(0x380).Sum(value => value));
        byte[] party = [0, 1, 2];
        using Ff7WorldRuntime world = new(installation, variables, 0, party, entryModule: 1);
        Assert(world.Machine.OwnsHostOperation(0x09FE, WorldOperation.BoardTransport), "The fixture must use the original caught-Chocobo boarding instruction.");
        world.Advance();
        Ff7WorldProcess mounted = world.Process;
        Assert(world.ControlledModel == 4 && mounted.Chocobo?.RiderModel == 0 && mounted.Vm.Models.Single(model => model.Model == 0).Detached && mounted.Audio?.Song == 2, "Native catch return must board the yellow Chocobo and detach the original rider.");
        Assert(Bird(world) is { X: 228926, Y: 233, Z: 141315, TerrainResolved: true }, "The catch return must use the original saved position and installed terrain.");
        Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, 4);
        Assert(descriptor.HrcName.Equals("aja.hrc", StringComparison.OrdinalIgnoreCase) && descriptor.Load(world.Resources).Animations.Count == 3, "The original yellow Chocobo must load its own HRC and three animation clips.");
        using Ff7WorldRuntime cold = new(installation, world.Machine.CopyPersistent(), 0, party);
        RestoreAndCompare(world, cold);
        for (int i = 0; i < 120 && !world.MovementEnabled; i++)
        {
            world.SetButtons(32, 32);
            cold.SetButtons(32, 32);
            if (world.Process.Dialogue is { AwaitingInput: true } dialogue)
            {
                world.CompleteDialogue(dialogue.RequestId, 0);
                cold.CompleteDialogue(dialogue.RequestId, 0);
            }
            AdvanceAndCompare(world, cold);
        }
        Assert(world.MovementEnabled && world.Process.Dialogue is { Text: 59, Completed: true }, "The original boarding prompt must finish before mounted movement.");
        world.SetButtons(0, 0);
        cold.SetButtons(0, 0);
        world.SetMovement(1, 0, true);
        cold.SetMovement(1, 0, true);
        AdvanceAndCompare(world, cold);
        Assert(Bird(world) is { X: 229046, Z: 141315, Direction: 256, TargetDirection: 1024, PoseAnimation: 1 }, "The yellow Chocobo must use the native doubled displacement and turn toward the travel direction.");

        using Ff7WorldRuntime encounterProbe = new(installation, world.Machine.CopyPersistent(), 0, party);
        Ff7WorldProcess moving = world.Process;
        encounterProbe.Restore(moving with { BattleState = moving.BattleState! with { MovementCounter = 16, Danger = 1 << 18, Random = new byte[0x209], RandomIndex = 0 } });
        encounterProbe.RandomBattlesEnabled = true;
        encounterProbe.Advance();
        Ff7WorldBattleState afterMountedStep = encounterProbe.Process.BattleState!;
        Assert(Bird(encounterProbe).X != Bird(world).X && afterMountedStep is { MovementCounter: 0, Pending: null, Danger: 1 << 18, RandomIndex: 1 }, "Native caught Chocobo movement must sample input RNG and clear the encounter counter without sampling an encounter.");

        using Ff7WorldRuntime blocked = new(installation, world.Machine.CopyPersistent(), 0, party);
        RestoreAndCompare(world, blocked);
        blocked.SetMovement(0, 1, true);
        for (int i = 0; i < 174; i++)
        {
            blocked.Advance();
        }
        Assert(Bird(blocked) is { X: 221205, Z: 157162, PreviousX: 221205, PreviousZ: 157162 } marsh && (marsh.Terrain & 31) == 7 && (marsh.Flags & 1) == 0, "Original marsh geometry must reject the forward displacement.");
        blocked.SetMovement(0, 0, true);
        blocked.SetButtons(64, 64);
        blocked.Advance();
        blocked.SetButtons(0, 0);
        for (int i = 0; i < 3; i++)
        {
            blocked.Advance();
        }
        Assert(blocked.ControlledModel == 4 && blocked.Process.Chocobo is { RiderModel: 0, DismountPhase: 0 } && blocked.MovementEnabled, "The native marsh terrain mask must reject dismount and retain the rider.");

        world.SetMovement(0, 0, true);
        cold.SetMovement(0, 0, true);
        world.SetButtons(64, 64);
        cold.SetButtons(64, 64);
        AdvanceAndCompare(world, cold);
        world.SetButtons(0, 0);
        cold.SetButtons(0, 0);
        AdvanceAndCompare(world, cold);
        Assert(world.Process.Chocobo?.DismountPhase == 1, "Releasing a short Cancel hold must start the native dismount phase.");
        AdvanceAndCompare(world, cold);
        Assert(world.Process.Chocobo?.DismountPhase == 2, "The dismount must defer terrain displacement until the placement phase.");
        RestoreAndCompare(world, cold);
        AdvanceAndCompare(world, cold);
        Assert(world.ControlledModel == 0 && world.Process.Chocobo?.Landing is { X: 229323, Z: 141201 } && world.Process.Vm.Models.Single(model => model.Model == 4).ProgramCounter == 0x1E2F && !world.Process.Vm.Models.Single(model => model.Model == 0).Detached && world.Process.Audio?.Song == 1, "The valid side-step must restore the original rider, retain the bird, and queue original model event 5.");
        RestoreAndCompare(world, cold);
        bool ran = false;
        for (int i = 0; i < 220 && world.Process.Chocobo is not null; i++)
        {
            AdvanceAndCompare(world, cold);
            if (!ran && world.Process.Chocobo?.Release is { RunPhase: 10 })
            {
                ran = true;
                RestoreAndCompare(world, cold);
            }
        }
        Assert(ran && world.Process.Chocobo is null && world.Process.Entities.All(entity => entity.Model != 4) && world.MovementEnabled && world.ControlledModel == 0, "The original release script and farm-directed run must complete with the rider active and the released bird removed.");
        RestoreAndCompare(world, cold);
        AdvanceAndCompare(world, cold);
        Console.WriteLine("PASS FFVII yellow Chocobo original boarding, movement, terrain rejection, dismount, release and cold continuation");
    }

    private static Ff7WorldEntity Bird(Ff7WorldRuntime world) => world.Process.Entities.Single(entity => entity.Model == 4);

    private static void RestoreAndCompare(Ff7WorldRuntime world, Ff7WorldRuntime cold)
    {
        cold.Restore(JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(world.Process))!);
        AssertSame(world, cold);
    }

    private static void AdvanceAndCompare(Ff7WorldRuntime world, Ff7WorldRuntime cold)
    {
        world.Advance();
        cold.Advance();
        AssertSame(world, cold);
    }

    private static void AssertSame(Ff7WorldRuntime world, Ff7WorldRuntime cold)
    {
        Assert(JsonSerializer.Serialize(world.Process) == JsonSerializer.Serialize(cold.Process) && world.Machine.CopyPersistent().SequenceEqual(cold.Machine.CopyPersistent()), "A cold world restore must preserve exact ownership, camera, terrain, presentation and native VM continuation.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
