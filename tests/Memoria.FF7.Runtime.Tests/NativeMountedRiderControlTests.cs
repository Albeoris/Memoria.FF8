using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMountedRiderControlTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        using Ff7WorldRuntime world = new(installation, new byte[1280], 0, [0, 2, 8]);
        Set(world, "initialized", true);
        Set(world, "mainState", 1);
        Set(world, "controlledModel", 4);
        Set(world, "movementEnabled", true);
        Set(world, "controlLock", 3);
        Set(world, "chocobo", new Ff7WorldChocoboState(1) { Model = 4 });
        var entities = (Dictionary<int, Ff7WorldEntity>)typeof(Ff7WorldRuntime).GetField("entities", Private)!.GetValue(world)!;
        entities[1] = new(1);
        entities[4] = new(4, 1000, 10, 2000, TerrainResolved: true) { AttachedModel = 1 };
        world.Machine.Restore(world.Machine.Capture() with { Models = [new WorldEntitySnapshot(1, 0, 0, 0, false, 0, []) { Detached = true }, new(4, 0, 0, 0, false, 0, [])] });
        string before = JsonSerializer.Serialize(world.Process);
        Ff7AutoPlayWorldControlObservation candidate = world.ReadAutoPlayChocoboControlCandidate() ?? throw new InvalidOperationException("An enabled native rider with a retained control tag was rejected.");
        Check(world.ChooseAutoPlayControlInput(candidate) is { Held: 0x40, Horizontal: 0, Vertical: 0 } && before == JsonSerializer.Serialize(world.Process), "Choosing the real Cancel pulse changed native state or used movement.");
        Set(world, "chocobo", new Ff7WorldChocoboState(1, CancelHeldTicks: 1) { Model = 4 });
        Check(world.ChooseAutoPlayControlInput(candidate).Held == 0, "The short hold was not followed by a genuine release.");
        Set(world, "chocobo", new Ff7WorldChocoboState(1, DismountPhase: 1) { Model = 4 });
        Check(world.ChooseAutoPlayControlInput(candidate).Held == 0 && !world.HasAutoPlayControlCompleted(candidate), "An unfinished native placement manufactured a rider return.");
        Set(world, "controlledModel", 1);
        Set(world, "movementEnabled", false);
        Set(world, "controlLock", 6);
        Check(!world.HasAutoPlayControlCompleted(candidate) && world.ChooseAutoPlayControlInput(candidate).Held == 0, "The rider handoff completed before the native release/control continuation.");
        Set(world, "movementEnabled", true);
        Check(world.HasAutoPlayControlCompleted(candidate), "A genuinely enabled rider was blocked by a retained native ownership tag.");
        Set(world, "controlledModel", 4);
        Set(world, "chocobo", new Ff7WorldChocoboState(1) { Model = 4 });
        entities[4] = entities[4] with { Terrain = 3 };
        Check(world.ReadAutoPlayChocoboControlCandidate() is null, "A native forbidden-terrain dismount was offered.");
        entities[4] = entities[4] with { Terrain = 0, AttachedModel = null };
        Check(world.ReadAutoPlayChocoboControlCandidate() is null, "An unowned rider attachment acquired a control prerequisite.");
        entities[4] = entities[4] with { AttachedModel = 1 };
        Set(world, "movementEnabled", false);
        Check(world.ReadAutoPlayChocoboControlCandidate() is null, "A disabled native player acquired a control prerequisite.");
        Set(world, "movementEnabled", true);
        bool rejected = false;
        try
        {
            world.ChooseAutoPlayControlInput(candidate with { ConditionFingerprint = "different" });
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }
        Check(rejected, "An unrelated source condition published Chocobo controls.");
        Console.WriteLine("Mounted rider controls preserve native pulse, ownership, terrain and actual release settlement.");
    }

    private static void Set(Ff7WorldRuntime world, string name, object value) => typeof(Ff7WorldRuntime).GetField(name, Private)!.SetValue(world, value);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
