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
            Assert.True(Directory.Exists(Path.Combine(destination, "Mods", "Tonberry")));
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
