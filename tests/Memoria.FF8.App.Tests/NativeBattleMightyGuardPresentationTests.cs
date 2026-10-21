namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMightyGuardPresentationTests
{
    [Fact]
    public void Original_Mighty_Guard_component_completes_controlled_contact_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([73, 78], "MEMORIA_FF8_MIGHTY_GUARD");
    }
}
