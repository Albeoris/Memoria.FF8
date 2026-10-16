using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocationPresentationTests
{
    [Fact]
    public void OriginalPlaceNamesUsePersistentFieldWordAndCurrentWorldCoordinates()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed area and world resources are required.");
        using GameAssets assets = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        GameState field = new() { Scene = SceneKind.Field, FieldId = 618, FieldName = "fhpanel1", EventState = JsonSerializer.SerializeToElement(new { Scene = new { PlaceId = 148 } }) };
        Assert.Equal("FH- Sun Panel", LiveMenuData.Build(field, catalog: catalog).Location);
        Assert.Equal("FH- Sun Panel", LiveMenuData.Build(field with { FieldId = 619, FieldName = "another_file" }, catalog: catalog).Location);
        Assert.Equal("Deling City- Gateway", LiveMenuData.Build(field, catalog: catalog, placeId: 132).Location);
        GameState world = field with { Scene = SceneKind.WorldMap, World = new WorldState { X = 12875, Z = -26746 } };
        Assert.Equal(1, catalog.LocationId(world, 148));
        Assert.Equal("Balamb- Alcauld Plains", LiveMenuData.Build(world, catalog: catalog, placeId: 148).Location);
        Assert.Equal("Unavailable", LiveMenuData.Build(new GameState(), catalog: catalog).Location);
        Assert.Null(catalog.PlaceName(-1));
        Assert.Null(catalog.PlaceName(65535));
    }

    [Fact]
    public void WindowTitleKeepsSceneIdentityAndSpeedWithCompactWorldCoordinates()
    {
        GameState field = new() { Scene = SceneKind.Field, FieldId = 618, FieldName = "fhpanel1" };
        string title = LocationWindowTitle.Format(field, null, null, 461, 3);
        Assert.Contains("Field 618:fhpanel1", title);
        Assert.Contains("F461", title);
        Assert.EndsWith(" | x3", title);
        GameState world = field with { Scene = SceneKind.WorldMap, World = new WorldState { X = 12875, Z = -26746, NativeHeight = -303, Frame = 1641 }, WorldArrival = new WorldTransition(0, 0, -1) };
        title = LocationWindowTitle.Format(world, null, null, 461, 1, 1);
        Assert.Contains("World S", title);
        Assert.Contains("P1 XYZ(12875,-303,-26746)", title);
        Assert.Contains("F1641", title);
        Assert.DoesNotContain("fhpanel1", title);
    }
}
