namespace Memoria.Launcher.Tests;

public sealed class DistributionArchiveTests
{
    [Fact]
    public void PublishedArchiveCanBeValidatedAndStagedByUpdater()
    {
        string? archive = Environment.GetEnvironmentVariable("MEMORIA_FF8_DISTRIBUTION_PATH");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(archive), "Set MEMORIA_FF8_DISTRIBUTION_PATH to validate a published distribution archive.");
        string rid = Environment.GetEnvironmentVariable("MEMORIA_FF8_DISTRIBUTION_RID") ?? Path.GetFileName(archive) switch
        {
            "Memoria-FF8-linux-x64.tar.gz" => "linux-x64",
            "Memoria-FF8-osx-x64.tar.gz" => "osx-x64",
            "Memoria-FF8-osx-arm64.tar.gz" => "osx-arm64",
            _ => "win-x64"
        };
        string temporary = Path.Combine(Path.GetTempPath(), "Memoria.Distribution.Tests", Guid.NewGuid().ToString("N"));
        bool mac = rid.StartsWith("osx-", StringComparison.Ordinal);
        string destination = mac ? Path.Combine(temporary, "Memoria.FF8.app") : temporary;
        string prefix = mac ? "Contents/Resources/" : "";
        try
        {
            PackageManifest.Extract(archive!, destination, rid);
            PackageManifest manifest = PackageManifest.Read(destination);
            Assert.NotEmpty(manifest.Files);
            string runtime = rid == "win-x64" ? "coreclr.dll" : mac ? "libcoreclr.dylib" : "libcoreclr.so";
            Assert.True(File.Exists(Path.Combine(destination, prefix + "Data/audio-host/" + runtime)));
            if (rid == "linux-x64")
            {
                Assert.DoesNotContain(manifest.Files, file => file.Path == "createdump");
            }
            if (rid == "win-x64")
            {
                foreach (string format in new[] { "Tonberry", "Demaster", "Junction" })
                {
                    string directory = Path.Combine(destination, "Mods", format);
                    Assert.True(Directory.Exists(directory));
                    Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
                }
            }
            foreach (string relative in new[] { "README.md", "fields/bghall_6.json", "fields/bgroom_1.json" })
            {
                string path = prefix + "Data/BundledMods/Memoria/Depixelation/" + relative;
                Assert.True(File.Exists(Path.Combine(destination, path)));
                Assert.Contains(manifest.Files, file => file.Path == path);
            }
            Assert.False(File.Exists(Path.Combine(destination, prefix + "Data/BundledMods/Memoria/Depixelation/mod.ini")));
            Assert.False(Directory.Exists(Path.Combine(destination, prefix + "Mods/Memoria")));
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, true);
            }
        }
    }
}
