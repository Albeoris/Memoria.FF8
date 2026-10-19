namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
    [Fact]
    public void Installed_Doomtrain_ordinary_command_completes_graphics_contact_and_disk_continuations() => Run(11, 191);
}
