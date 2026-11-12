using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestScriptInteractionTests
{
    [Theory]
    [InlineData(1, 0, 1, 0)]
    [InlineData(2, 1, 2, 65535)]
    [InlineData(2, 90, 2, 65535)]
    [InlineData(2, 5, 2, 65535)]
    [InlineData(3, 1, 3, 65535)]
    [InlineData(2, 769, 3, 65535)]
    [InlineData(4, 9, 4, 5)]
    [InlineData(4, 5, 4, 65535)]
    [InlineData(4, 7, 4, 65535)]
    [InlineData(2, 1033, 4, 5)]
    [InlineData(5, 15, 255, 65535)]
    public void Foreign_action_indices_are_not_reinterpreted_as_native_script_actions(byte command, ushort ability, byte nativeCommand, ushort nativeAction)
    {
        Ff7GuestScriptInteraction result = CrossoverGuestHostActions.ClassifyScriptInteraction(command, ability, 4);
        Assert.Equal(new Ff7GuestScriptInteraction(nativeCommand, nativeAction), result);
    }

    [Fact]
    public void Stock_and_cast_draw_keep_the_resource_source_separate_from_the_spell_target()
    {
        Assert.Equal(new Ff7GuestScriptInteraction(255, 65535, 7), CrossoverGuestHostActions.ClassifyScriptInteraction(2, 0xE001, 7));
        ushort cast = (ushort)(0xD000 | ((7 - 4) << 6) | 21);
        Assert.Equal(new Ff7GuestScriptInteraction(2, 65535, 7, HasRecipientEffect: true), CrossoverGuestHostActions.ClassifyScriptInteraction(2, cast, 0));
        Assert.Throws<InvalidDataException>(() => CrossoverGuestHostActions.ClassifyScriptInteraction(2, 0xE001, 0));
        Assert.Throws<InvalidDataException>(() => CrossoverGuestHostActions.ClassifyScriptInteraction(2, 0xD3C1, 4));
    }

    [Fact]
    public void Scan_uses_inspection_semantics_without_aliasing_an_item_or_attack()
    {
        Assert.Equal(new Ff7GuestScriptInteraction(6, 65535, InspectionOnly: true), CrossoverGuestHostActions.ClassifyScriptInteraction(2, 50, 4));
        Assert.False(CrossoverGuestHostActions.ClassifyScriptInteraction(4, 50, 4).InspectionOnly);
    }
}
