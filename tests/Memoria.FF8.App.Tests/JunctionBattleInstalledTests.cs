using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class JunctionBattleInstalledTests
{
    private static readonly string[] Roles = ["stages", "characters", "enemies"];

    [Fact]
    public void OriginalPackageSetHasOnlySupportedOrProvenUnusedTargets()
    {
        string archive = TestResources.FixturePath("junction-battle-stages");
        string junction = Path.GetDirectoryName(Path.GetDirectoryName(archive))!;
        Assert.Equal("Junction", Path.GetFileName(junction));
        using GameResourceHost host = new(TestResources.GameDirectory, watch: false, modsDirectory: Directory.GetParent(junction)!.FullName);
        foreach (string role in Roles)
        {
            string path = TestResources.FixturePath("junction-battle-" + role);
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(path, StringComparison.OrdinalIgnoreCase)).Status);
        }
        Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "junction-import-failed" or "ffnx-unsupported-target" or "junction-setting-conflict");
    }
}
