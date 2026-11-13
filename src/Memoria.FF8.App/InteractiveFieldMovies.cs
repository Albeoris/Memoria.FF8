using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class InteractiveFieldMovies
{
    public static bool RequiresPlayerInput(GameState state) => IsAirborneRescue(state) || IsPrisonBridgeCollapse(state);

    public static bool IsAirborneRescue(GameState state) =>
        state.MovieSource is { DiscIndex: 1 } source && (state.FieldId, source.MovieIndex) is (144, 32) or (152, 33);

    public static bool IsPrisonBridgeCollapse(GameState state) =>
        state.FieldId == 809 && state.MovieSource is { DiscIndex: 1, MovieIndex: 3 };
}
