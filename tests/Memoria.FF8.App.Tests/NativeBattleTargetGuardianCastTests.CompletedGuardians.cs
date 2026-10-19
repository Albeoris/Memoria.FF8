namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
    [Fact]
    public void Installed_Diablos_ordinary_command_completes_graphics_contact_and_disk_continuations() => Run(5, 325);

    [Fact]
    public void Installed_Pandemona_ordinary_command_completes_graphics_contact_and_disk_continuations() => Run(8, 291);
}
