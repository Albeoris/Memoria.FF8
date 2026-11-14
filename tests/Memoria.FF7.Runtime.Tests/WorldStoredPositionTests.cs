using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldStoredPositionTests
{
    public static void Run(Ff7WorldRuntime world, Ff7WorldRuntime cold, Ff7WorldProcess ready, Ff7Installation installation)
    {
        world.Restore(ready);
        int model = ready.ControlledModel;
        Ff7WorldEntity before = ready.Entities.Single(entity => entity.Model == model);
        Apply(WorldOperation.SavePosition, 0);
        Ff7WorldProcess stored = world.Process;
        Check(stored.StoredPositions is { Length: 3 } && stored.StoredPositions[0] == new Ff7WorldStoredPosition(before.X, before.Z), "Opcode344 must capture a native XZ point with Y discarded.");
        world.Restore(stored with { Entities = stored.Entities.Select(entity => entity.Model == model ? entity with { X = entity.X + 160, Y = entity.Y + 999, Z = entity.Z - 320 } : entity).ToArray() });
        Check(world.ReadStoredPositionDistance(model, 0) == 30 && world.ReadStoredPositionDistance(model, 3) == 0, "Opcode1A must use wrapped Manhattan XZ distance shifted four and return zero for an invalid slot.");
        cold.Restore(JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(world.Process))!);
        Ff7WorldMath math = new(Ff7PcExecutableData.LoadWorldPresentation(installation, world.Resources));
        int heading = math.Direction(-160, 320);
        foreach (Ff7WorldRuntime runtime in new[] { world, cold })
        {
            runtime.Execute(new(runtime.Process.Vm.NextRequestId, model, model, 0, new(WorldOperation.FaceSavedPosition, [0])));
        }
        Check(world.Process.Entities.Single(entity => entity.Model == model) is var facing && facing.Direction == heading && facing.TargetDirection == heading, "Opcode345 must set both native direction owners toward the saved point.");
        Check(JsonSerializer.Serialize(world.Process) == JsonSerializer.Serialize(cold.Process), "Stored vectors and subsequent facing must cold-restore exactly.");
        Ff7WorldEntity source = ready.Entities.First(entity => entity.Model != model && world.Machine.IsModelAttached(entity.Model));
        Apply(WorldOperation.CopyModelPosition, source.Model);
        Ff7WorldEntity copied = world.Process.Entities.Single(entity => entity.Model == model);
        Check(copied.X == source.X && copied.Y == source.Y && copied.Z == source.Z && copied.PreviousX == source.PreviousX && copied.PreviousY == source.PreviousY && copied.PreviousZ == source.PreviousZ && copied.Direction == heading && (copied.Flags & 1) != 0, "Opcode347 must copy current/previous XYZ and movement flag without replacing unrelated facing.");
        string snapshot = JsonSerializer.Serialize(world.Process);
        Apply(WorldOperation.SavePosition, 3);
        Apply(WorldOperation.FaceSavedPosition, -1);
        Apply(WorldOperation.CopyModelPosition, 255);
        Check(JsonSerializer.Serialize(world.Process) == snapshot, "Native invalid point slots and absent copy targets must leave state unchanged.");
        world.Restore(ready);
        cold.Restore(ready);

        void Apply(WorldOperation operation, int value)
        {
            world.Execute(new(world.Process.Vm.NextRequestId, model, model, 0, new(operation, [value])));
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
