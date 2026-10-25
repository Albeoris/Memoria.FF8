using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class JunctionStartupInstalledTests
{
    [Fact]
    public void ReviewedArchivePublishesIndependentStartupPolicyAndIntrinsicManualSkip()
    {
        string archive = TestResources.FixturePath("junction-startup");
        string settings = archive + ".ini";
        byte[]? original = File.Exists(settings) ? File.ReadAllBytes(settings) : null;
        string junction = Path.GetDirectoryName(archive)!;
        Assert.Equal("Junction", Path.GetFileName(junction));
        string mods = Directory.GetParent(junction)!.FullName;
        try
        {
            File.WriteAllText(settings, "enabled = 1\n");
            using GameResourceHost host = new(TestResources.GameDirectory, watch: false, modsDirectory: mods);
            ModPackage Package() => Assert.Single(host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(archive, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(ModEffectiveStatus.Active, Package().Status);
            Assert.Equal(new StartupPolicy(true, true, true, true), host.StartupPolicy);
            Assert.DoesNotContain(host.Mods.Catalog.Files, file => file.PackageId == Package().Id);
            StartupSequence first = new(true);
            first.Configure(host.StartupPolicy);
            Assert.False(first.Active);

            string[] options = Package().JunctionMetadata!.Options.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();
            Assert.Equal(4, options.Length);
            for (int mask = 0; mask < 16; mask++)
            {
                File.WriteAllLines(settings, new[] { "enabled = 1" }.Concat(options.Select((option, index) => $"option.{option} = {(mask >> index) & 1}")));
                host.Mods.Refresh();
                host.Pump(true);
                Assert.Equal(ModEffectiveStatus.Active, Package().Status);
                Assert.Equal(StartupPolicy.FromCatalog(host.Mods.Catalog), host.StartupPolicy);
                Assert.Contains(JunctionBehavior.InstantManualSkip, Package().JunctionMetadata!.Behaviors);
                Assert.Equal(4, Package().JunctionMetadata!.Options.Count);
                Assert.False(first.Active);
            }

            Assert.True(host.Mods.SetEnabled(Package().Id, false).Success);
            host.Pump(true);
            Assert.Equal(StartupPolicy.Default, host.StartupPolicy);
            StartupSequence manual = new(true);
            manual.Configure(host.StartupPolicy);
            manual.Next();
            manual.Next();
            manual.Next();
            Assert.Equal(StartupStage.Credits, manual.Stage);
            Assert.True(manual.HandleInput(true, false));
            Assert.False(manual.Active);
            Assert.Equal(TimeSpan.Zero, manual.Elapsed);
            Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "junction-import-failed" or "replacement-failed");
        }
        finally
        {
            if (original is null)
            {
                File.Delete(settings);
            }
            else
            {
                File.WriteAllBytes(settings, original);
            }
        }
    }
}
