using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerMovementSpeedTests
{
    [Fact]
    public void Field_speed_defaults_to_native_and_rejects_invalid_preferences_without_mutation()
    {
        EventEngine engine = new(GameSession.CreateColdProcess());
        Assert.Equal(100, engine.PlayerMovementSpeedPercent);
        engine.PlayerMovementSpeedPercent = 150;
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.PlayerMovementSpeedPercent = 49);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.PlayerMovementSpeedPercent = 301);
        Assert.Equal(150, engine.PlayerMovementSpeedPercent);
    }

    [Theory]
    [InlineData(31, 50, 15)]
    [InlineData(-31, 50, -15)]
    [InlineData(31, 300, 93)]
    [InlineData(-31, 300, -93)]
    [InlineData(200, 300, 600)]
    [InlineData(0, 300, 0)]
    public void Native_translation_scaling_retains_direction_and_integer_width(int movement, int percent, int expected)
    {
        Assert.Equal(expected, PlayerMovementSpeed.Scale(movement, percent));
    }
}
