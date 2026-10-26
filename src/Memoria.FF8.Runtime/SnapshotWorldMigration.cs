using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Retains the map selection formerly read directly from each saved allocation.</summary>
internal static class SnapshotWorldMigration
{
    internal static void Apply(JsonObject saved)
    {
        List<(JsonObject Owner, int Mode)> additions = [];
        foreach (string name in new[] { nameof(GameState.World), nameof(GameState.DepartingWorldState) })
        {
            if (saved[name] is not JsonObject world)
            {
                continue;
            }
            Collect(world["NativeTrainRuntime"], name + ".NativeTrainRuntime", "Load", 2, additions);
            Collect(world["NativeScriptedRuntime"], name + ".NativeScriptedRuntime", "Load", 3, additions);
            Collect(world["NativeRagnarokRuntime"], name + ".NativeRagnarokRuntime", "Allocation", 3, additions);
            if (world["NativeFootRuntime"] is JsonObject foot)
            {
                Collect(foot["StationTrains"], name + ".NativeFootRuntime.StationTrains", "Load", 2, additions);
            }
        }
        foreach ((JsonObject owner, int mode) in additions)
        {
            owner["MapDisplayMode"] = mode;
        }
    }

    private static void Collect(JsonNode? node, string path, string sourceName, int maximum, List<(JsonObject Owner, int Mode)> additions)
    {
        if (node is not JsonObject runtime || runtime.ContainsKey("MapDisplayMode"))
        {
            return;
        }
        if (runtime[sourceName] is not JsonObject source || source["MapDisplayMode"] is not JsonValue value || !value.TryGetValue(out int mode) || mode < 0 || mode > maximum)
        {
            throw new InvalidDataException(path + ": missing map display mode requires its saved " + sourceName + ".MapDisplayMode allocation value.");
        }
        additions.Add((runtime, mode));
    }
}

