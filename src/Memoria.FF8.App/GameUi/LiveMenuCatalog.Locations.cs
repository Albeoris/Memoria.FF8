using Albeoris.Games.FF8.NamedicBin;
using System.Text;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class LiveMenuCatalog
{
    private string[] locationNames = [];
    private WorldMapLocations worldLocations = null!;

    private void ReadLocations(byte[] bytes, Encoding encoding, WorldMapLocations world)
    {
        locationNames = NamedicBin.ReadStrings(bytes, encoding);
        worldLocations = world;
    }

    public string? PlaceName(int placeId) => (uint)placeId < locationNames.Length ? FormatDescription(LocalizedTable("names_locations", placeId, locationNames[placeId])) : null;

    public int? LocationId(GameState state, int? fieldPlaceId = null)
    {
        if (state.Scene == SceneKind.WorldMap)
        {
            return state.World is { } world ? worldLocations.GetPlaceId(world.X, world.Z) : null;
        }
        if (fieldPlaceId.HasValue)
        {
            return fieldPlaceId;
        }
        if (state.EventState is { } snapshot && snapshot.TryGetProperty("Scene", out var scene) && scene.TryGetProperty("PlaceId", out var place))
        {
            return place.GetInt32();
        }
        return null;
    }

    public string? LocationName(GameState state, int? fieldPlaceId = null) => LocationId(state, fieldPlaceId) is int id ? PlaceName(id) : null;
}
