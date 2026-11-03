namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterNativeEventsTests
{
    [Theory]
    [InlineData(true, true, true, true, 8, 220)]
    [InlineData(false, true, true, true, 9, 221)]
    [InlineData(false, false, true, true, 10, null)]
    [InlineData(false, true, false, true, null, null)]
    [InlineData(false, false, true, false, null, null)]
    public void Preserves_native_branch_order_and_one_selected_event(bool opening, bool partyUnused, bool partyCount, bool lowHp, int? ability, int? variable)
    {
        EncounterNativeEventSelection? selected = EncounterNativeEvents.Select(EncounterBindingResolver.Disc1Edea, opening, partyUnused, partyCount, lowHp);
        Assert.Equal(ability, selected is null ? null : (int?)selected.Ability);
        Assert.Equal(variable, selected?.SetActorVariable);
    }

    [Fact]
    public void Unknown_encounter_does_not_borrow_the_native_adapter()
    {
        Assert.Throws<InvalidDataException>(() => EncounterNativeEvents.Select("example.encounter", true, true, true, true));
    }
}
