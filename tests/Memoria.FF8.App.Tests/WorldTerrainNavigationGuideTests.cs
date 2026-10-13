using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldTerrainNavigationGuideTests
{
    [Fact]
    public void Fire_cavern_advice_routes_around_the_actual_cliff_and_keeps_the_original_gate_identity()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original world terrain is required.");
        using WorldAssets assets = new(directory);
        var replacements = WorldTerrainSelection.Replacements(285);
        Assert.Null(assets.FindNativeGround(30240, -29393, replacements, 904));
        WorldGround target = Assert.IsType<WorldGround>(assets.FindNativeGround(30240, -29393, replacements, 691));
        Assert.True(target.Triangle.FieldEntrance);
        WorldFieldSelection gate = Assert.IsType<WorldFieldSelection>(assets.FieldEntries.Select(new WorldFieldSelectionInput(30240, -29393, 17, 0), target.Triangle.TextureFlags));
        Assert.Equal((2, (ushort)136), (gate.Entry.Index, gate.Entry.FieldId));

        WorldTerrainRoute route = WorldTerrainNavigationGuide.Plan(assets, 30374, -28873, 904, 285, 17, 30240, -29393, 64);
        Assert.Null(route.Refusal);
        Assert.Equal(new WorldTerrainWaypoint(30374, -28873, 904), route.Waypoints[0]);
        Assert.Equal(new WorldTerrainWaypoint(30240, -29393, 690), route.Waypoints[^1]);
        Assert.True(route.Waypoints.Count > 2);
        Assert.Contains(route.Waypoints, waypoint => waypoint.RawZ > -28500 && waypoint.GroundHeight < 700);
        Assert.InRange(route.ExpandedNodes, 1, 25000);
        Assert.True(route.ProbeCount > route.ExpandedNodes);

        WorldTerrainRoute ordered = WorldTerrainNavigationGuide.Plan(assets, 30279, -28757, 868, 285, 17, 30240, -29393, 64, simplify: false, advisoryClearance: 32);
        Assert.Null(ordered.Refusal);
        Assert.Equal(new WorldTerrainWaypoint(30279, -28757, 868), ordered.Waypoints[0]);
        Assert.Equal(new WorldTerrainWaypoint(30240, -29393, 690), ordered.Waypoints[^1]);
        Assert.True(ordered.Waypoints.Count > route.Waypoints.Count);
        foreach (var (first, second) in ordered.Waypoints.Zip(ordered.Waypoints.Skip(1)))
        {
            Assert.InRange(Math.Abs(second.X - first.X), 0, 64);
            Assert.InRange(Math.Abs(second.RawZ - first.RawZ), 0, 64);
        }
    }
}
