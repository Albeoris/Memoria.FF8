namespace Memoria.FF8.App.Tests;

public sealed class CrossoverSelectedFinisherTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void Source_selected_finisher_retains_one_turn_and_its_original_recipient_occurrences(byte id, bool overkill)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Fire", 0, 4, sourceHP: 1, nativeProcess: true, allFinishers: true);
        fixture.ExecuteSelectedFinisher(id, overkill);
        fixture.FollowWithFire();
    }
}
