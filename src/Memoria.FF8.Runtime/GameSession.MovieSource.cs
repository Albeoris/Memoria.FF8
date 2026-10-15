using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeMovieSource? MovieSource => State.MovieSource;
    public bool CanStoreMovieSource => true;

    public void SetMovieSource(NativeMovieSource source)
    {
        source.Validate();
        if (IsMoviePlaying && State.MovieSource != source)
        {
            throw new NotSupportedException("MOVIEREADY cannot replace an active native movie resource.");
        }
        State = State with { MovieSource = source };
    }

    private static void ValidateMovieSourceCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.MovieSource?.Validate();
        if (state.Scene == SceneKind.Movie && state.MovieSource is { } source && source.MovieIndex != state.MovieIndex)
        {
            throw new InvalidDataException("Active native movie index differs from its prepared resource.");
        }
        if (currentVm is JsonElement vm && (!vm.TryGetProperty("MovieSource", out JsonElement saved) || !JsonElement.DeepEquals(saved, JsonSerializer.SerializeToElement(state.MovieSource, JsonOptions))))
        {
            throw new InvalidDataException("Root and VM native movie resource bindings disagree; recreate authentic history.");
        }
    }
}
