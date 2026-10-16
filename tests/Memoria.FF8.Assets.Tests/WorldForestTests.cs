using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldForestTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void Original_forest_is_walkable_on_foot_denied_to_car_and_not_a_height_step_wall()
    {
        using WorldAssets assets = new(GameDirectory);
        var replacements = WorldTerrainSelection.Replacements(285);
        WorldGroundSelection source = assets.LookupNativeGround(10845, -79939, replacements, [])!;
        Assert.Equal((byte)1, source.Ground.Triangle.GroundType);
        Assert.Equal((byte)0xB1, source.Ground.Triangle.VertexFlags);
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(source.Identity) with { Alternatives = [new(0, [], source.Identity, source.Identity) { FailedSelections = 0 }] };
        WorldNativeGroundResult foot = assets.ResolveNativeGround(10845, -79939, 31, 0, 1162, null, cache, replacements, 0);
        Assert.False(foot.Blocked);
        Assert.Equal((10876, -79939), (foot.X, foot.Z));
        Assert.True(assets.ResolveNativeGround(10845, -79939, 31, 0, 1162, null, cache, replacements, 33).Blocked);
        Assert.NotNull(assets.FindNativeGround(10845, -79939, replacements, -5000));
        Assert.Null(assets.FindNativeGround(24576, -29406, replacements, -5000));
        Assert.NotNull(assets.FindNativeGround(24576, -29406, replacements, -5000, previousGroundType: 1));
    }

    [Fact]
    public void All_original_forest_entrance_polygons_keep_native_field_packets_and_entity_rules()
    {
        using WorldAssets assets = new(GameDirectory);
        using GameAssets fields = new(GameDirectory);
        var replacements = WorldTerrainSelection.Replacements(285);
        Dictionary<int, WorldFieldEntry> entries = [];
        int entrancePolygons = 0;
        int acceptedCenters = 0;
        for (int map = 0; map < 768; map++)
        {
            foreach (WorldTriangle triangle in assets.GetSegment(map, replacements).Triangles.Where(triangle => triangle.Forest && triangle.FieldEntrance))
            {
                entrancePolygons++;
                int x = (int)((triangle.A.X + triangle.B.X + triangle.C.X) / 3);
                int z = (int)((triangle.A.Z + triangle.B.Z + triangle.C.Z) / 3);
                foreach (ushort story in new ushort[] { 0, 17, 34, 205, 500, 1000, 2000, 4000 })
                {
                    foreach (int entity in new[] { 0, 1, 33, 48, 49, 50 })
                    {
                        WorldFieldSelection? selection = assets.FieldEntries.Select(new(x, z, story, entity), triangle.TextureFlags);
                        Assert.Null(assets.FieldEntries.Select(new(x, z, story, entity), (byte)(triangle.TextureFlags & ~8)));
                        if (selection is null)
                        {
                            continue;
                        }
                        acceptedCenters++;
                        entries.TryAdd(selection.Entry.Index, selection.Entry);
                    }
                }
            }
        }
        Assert.Equal(400, entrancePolygons);
        Assert.True(acceptedCenters > 0);
        foreach (WorldFieldEntry entry in entries.Values)
        {
            FieldAssets field = fields.LoadField(entry.FieldId);
            Assert.InRange(entry.Triangle, 0, field.Walkmesh.Triangles.Count - 1);
            Console.WriteLine($"Forest entry{entry.Index}: field{entry.FieldId}/{field.Name}, triangle{entry.Triangle} ({entry.X},{entry.Y}).");
        }
        Console.WriteLine($"Installed forest entries: {entrancePolygons} polygons, {acceptedCenters} selected centers across8 stories and6 entities, {entries.Count} packets.");
    }

    [Fact]
    public void Native_effect_records_decode_original_six_palettes_and_four_frames()
    {
        using WorldAssets assets = new(GameDirectory);
        Assert.Equal(new ushort[] { 227, 229, 231, 228, 226, 230 }, assets.ForestTables.PaletteY.Take(6));
        Assert.Equal(256, assets.ForestTables.Random.Length);
        foreach (NativeWorldForestEffect effect in assets.ForestTables.Effects.Take(6))
        {
            Assert.Equal((3968, 8, 4, 2, 255), ((int)effect.Shrink, (int)effect.Lifetime, (int)effect.Frames, (int)effect.FrameDuration, (int)effect.HalfSize));
            for (int frame = 0; frame < effect.Frames; frame++)
            {
                var texture = assets.GetForestTexture(effect.Type, frame);
                Assert.Equal((16, 16), (texture.Width, texture.Height));
                Assert.Contains(texture.Pixels, color => color.A != 0);
            }
        }
    }
}
