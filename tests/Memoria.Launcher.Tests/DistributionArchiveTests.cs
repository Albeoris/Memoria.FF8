namespace Memoria.Launcher.Tests;

public sealed class DistributionArchiveTests
{
    [Fact]
    public void PublishedArchiveCanBeValidatedAndStagedByUpdater()
    {
        string? archive = Environment.GetEnvironmentVariable("MEMORIA_FF8_DISTRIBUTION_PATH");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(archive), "Set MEMORIA_FF8_DISTRIBUTION_PATH to validate a published distribution archive.");
        string destination = Path.Combine(Path.GetTempPath(), "Memoria.Distribution.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            PackageManifest.Extract(archive!, destination);
            PackageManifest manifest = PackageManifest.Read(destination);
            Assert.NotEmpty(manifest.Files);
            Assert.True(File.Exists(Path.Combine(destination, "Data", "audio-host", "coreclr.dll")));
            foreach (string format in new[] { "Tonberry", "Demaster", "Junction" })
            {
                string directory = Path.Combine(destination, "Mods", format);
                Assert.True(Directory.Exists(directory));
                Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
            }
            foreach (string relative in new[] { "README.md", "fields/bghall_6.json", "fields/bgroom_1.json" })
            {
                string path = "Data/BundledMods/Memoria/Depixelation/" + relative;
                Assert.True(File.Exists(Path.Combine(destination, path)));
                Assert.Contains(manifest.Files, file => file.Path == path);
            }
            Assert.False(File.Exists(Path.Combine(destination, "Data/BundledMods/Memoria/Depixelation/mod.ini")));
            Assert.False(Directory.Exists(Path.Combine(destination, "Mods", "Memoria")));
        }
        finally
        {
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }
        }
    }
}
