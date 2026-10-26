using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotWorldMigrationTests
{
    [Theory]
    [InlineData("World", "NativeTrainRuntime", "Load", 2)]
    [InlineData("World", "NativeScriptedRuntime", "Load", 3)]
    [InlineData("World", "NativeRagnarokRuntime", "Allocation", 3)]
    [InlineData("DepartingWorldState", "NativeTrainRuntime", "Load", 2)]
    [InlineData("DepartingWorldState", "NativeScriptedRuntime", "Load", 3)]
    [InlineData("DepartingWorldState", "NativeRagnarokRuntime", "Allocation", 3)]
    public void Absent_map_selection_comes_from_the_saved_allocation(string owner, string runtime, string sourceName, int mode)
    {
        JsonObject state = JsonNode.Parse("{\"Frame\":57,\"Unrelated\":[1,2]}")!.AsObject();
        state[sourceName] = new JsonObject { ["MapDisplayMode"] = mode };
        JsonObject saved = new() { [owner] = new JsonObject { [runtime] = state } };

        SnapshotWorldMigration.Apply(saved);

        Assert.Equal(mode, state["MapDisplayMode"]!.GetValue<int>());
        Assert.Equal(57, state["Frame"]!.GetValue<int>());
        Assert.Equal("[1,2]", state["Unrelated"]!.ToJsonString());
        string migrated = saved.ToJsonString();
        SnapshotWorldMigration.Apply(saved);
        Assert.Equal(migrated, saved.ToJsonString());
    }

    [Theory]
    [InlineData("World")]
    [InlineData("DepartingWorldState")]
    public void Station_foot_retains_its_train_allocation_selection(string owner)
    {
        JsonObject train = new() { ["Load"] = new JsonObject { ["MapDisplayMode"] = 1 } };
        JsonObject saved = new() { [owner] = new JsonObject { ["NativeFootRuntime"] = new JsonObject { ["StationTrains"] = train } } };

        SnapshotWorldMigration.Apply(saved);

        Assert.Equal(1, train["MapDisplayMode"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("99")]
    public void Existing_map_selection_including_invalid_data_is_preserved_for_validation(string value)
    {
        JsonObject saved = JsonNode.Parse("{\"World\":{\"NativeTrainRuntime\":{\"MapDisplayMode\":" + value + "}}}")!.AsObject();
        string original = saved.ToJsonString();

        SnapshotWorldMigration.Apply(saved);

        Assert.Equal(original, saved.ToJsonString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Load\":null}")]
    [InlineData("{\"Load\":{}}")]
    [InlineData("{\"Load\":{\"MapDisplayMode\":null}}")]
    [InlineData("{\"Load\":{\"MapDisplayMode\":3}}")]
    [InlineData("{\"Load\":{\"MapDisplayMode\":-1}}")]
    [InlineData("{\"Load\":{\"MapDisplayMode\":\"1\"}}")]
    public void Missing_or_invalid_allocation_values_refuse_without_partial_changes(string invalid)
    {
        JsonObject saved = JsonNode.Parse("{\"World\":{\"NativeTrainRuntime\":{\"Load\":{\"MapDisplayMode\":1}}},\"DepartingWorldState\":{\"NativeTrainRuntime\":" + invalid + "}}")!.AsObject();
        string original = saved.ToJsonString();

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => SnapshotWorldMigration.Apply(saved));

        Assert.Contains("DepartingWorldState.NativeTrainRuntime", error.Message);
        Assert.Equal(original, saved.ToJsonString());
    }

    [Fact]
    public void Absent_world_owners_are_not_created()
    {
        JsonObject saved = JsonNode.Parse("{\"World\":null,\"EventState\":{\"Frame\":9}}")!.AsObject();
        string original = saved.ToJsonString();

        SnapshotWorldMigration.Apply(saved);

        Assert.Equal(original, saved.ToJsonString());
    }
}
