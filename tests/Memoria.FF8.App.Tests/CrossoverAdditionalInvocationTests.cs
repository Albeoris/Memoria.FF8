namespace Memoria.FF8.App.Tests;

public sealed class CrossoverAdditionalInvocationTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_cinematic_command_uses_shared_resource_ownership_and_completes_the_next_cast()
    {
        new CrossoverParticipantResourceHostTests(output).Run("Homing Laser", 3, 4);
    }
}
