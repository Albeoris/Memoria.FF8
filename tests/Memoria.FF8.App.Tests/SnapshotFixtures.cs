using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class SnapshotFixtures
{
    internal static GameState Require(string variable, int? fieldId, int? frame = null)
    {
        string? path = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), $"Set {variable} to an ordinary save snapshot for this gameplay boundary.");
        GameState state = GameSession.Load(path!);
        Assert.Equal(SceneKind.Field, state.Scene);
        if (fieldId is int expectedField)
        {
            Assert.Equal(expectedField, state.FieldId);
        }
        if (frame is int expectedFrame)
        {
            Assert.Equal(expectedFrame, state.EventState!.Value.GetProperty("Frame").GetInt32());
        }
        return state;
    }
}
