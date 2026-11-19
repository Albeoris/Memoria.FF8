using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TextureSceneEligibilityTests
{
    [Theory]
    [InlineData(SceneKind.Field, false, false, true, false, false, false, true)]
    [InlineData(SceneKind.WorldMap, false, false, false, true, false, false, true)]
    [InlineData(SceneKind.Title, false, true, false, false, false, false, true)]
    [InlineData(SceneKind.Title, false, false, true, true, false, false, false)]
    [InlineData(SceneKind.Movie, false, false, true, false, false, false, false)]
    [InlineData(SceneKind.Field, true, false, true, false, false, false, false)]
    [InlineData(SceneKind.Field, false, false, false, false, false, false, false)]
    [InlineData(SceneKind.Field, false, false, true, false, true, false, false)]
    [InlineData(SceneKind.WorldMap, false, false, false, true, false, true, false)]
    public void Only_current_nonexclusive_ff8_gameplay_presentations_are_eligible(SceneKind scene, bool ff7, bool battle, bool field, bool world, bool faulted, bool exclusive, bool expected)
    {
        Assert.Equal(expected, TextureSceneEligibility.CanCapture(scene, ff7, battle, field, world, faulted, exclusive));
    }
}
