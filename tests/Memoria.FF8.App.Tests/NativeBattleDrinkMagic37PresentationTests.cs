namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDrinkMagic37PresentationTests
{
    [Fact]
    public void Original_Drink_Magic_component_completes_controlled_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([37], "MEMORIA_FF8_DRINK_MAGIC");
    }
}
