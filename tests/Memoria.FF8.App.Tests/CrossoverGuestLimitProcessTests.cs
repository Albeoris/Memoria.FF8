namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestLimitProcessTests
{
    [Theory]
    [InlineData("Duel", (byte)1, (byte)11)]
    [InlineData("Slot", (byte)5, (byte)16)]
    public void Original_limit_selection_and_input_complete_one_owned_host_turn(string name, byte character, byte command)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new(name, character, 4, nativeProcess: true);
        fixture.ExecuteNativeProcess(command);
        fixture.FollowWithFire();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Every_original_Shot_ammunition_selection_opens_fires_and_closes(byte ammunition)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Shot", 2, 4, nativeProcess: true);
        fixture.ExecuteNativeProcess(14, (ushort)(0x0E00 | ammunition));
        fixture.FollowWithFire();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Combine_selects_each_original_learned_Angelo_finisher(byte action)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Combine", 4, 4, nativeProcess: true);
        fixture.ExecuteNativeProcess(19, (ushort)(0x1300 | action), angeloFlags: (byte)(1 << (4 + action)));
        fixture.FollowWithFire();
    }

    [Fact]
    public void Automatic_Duel_uses_source_UI_random_and_finishes_its_native_timer()
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Duel", 1, 4, nativeProcess: true);
        fixture.ExecuteNativeProcess(11, automaticDuel: true);
        fixture.FollowWithFire();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Every_original_Duel_move_is_reached_through_its_native_button_recipe(byte action)
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Duel", 1, 4, nativeProcess: true);
        fixture.ExecuteNativeProcess(11, duelAction: action);
        fixture.FollowWithFire();
    }
}
