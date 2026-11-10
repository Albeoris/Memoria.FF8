using System.Diagnostics;

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
        string destination = Path.Combine(temporary, mac ? "Memoria.FF8.app" : "distribution");
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
            if (rid == "linux-x64" && OperatingSystem.IsLinux())
            {
                VerifyLinuxHelperWaitsForLauncher(temporary, destination);
            }
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, true);
            }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void VerifyLinuxHelperWaitsForLauncher(string temporary, string distribution)
    {
        string root = Path.Combine(temporary, "installation");
        string work = Path.Combine(root, UpdateInstaller.WorkDirectory);
        string runtime = Path.Combine(work, "recovery-runtime");
        Directory.CreateDirectory(work);
        Directory.Move(distribution, runtime);
        string manifest = Path.Combine(root, PackageManifest.ManifestPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.Copy(Path.Combine(runtime, PackageManifest.ManifestPath), manifest);
        string launcher = Path.Combine(root, "Memoria.Launcher");
        File.WriteAllText(launcher, "#!/bin/sh\ntouch restarted\n");
        File.SetUnixFileMode(launcher, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        using Mutex mutex = new(false, UpdateInstaller.MutexName(root));
        bool locked = mutex.WaitOne(0);
        Assert.True(locked);
        try
        {
            ProcessStartInfo start = new(Path.Combine(runtime, "Memoria.Launcher")) { WorkingDirectory = work, UseShellExecute = false };
            start.ArgumentList.Add("--recover-update");
            start.ArgumentList.Add(root);
            start.ArgumentList.Add("0");
            start.ArgumentList.Add("0");
            start.Environment.Remove("DISPLAY");
            start.Environment.Remove("WAYLAND_DISPLAY");
            start.Environment["XDG_DATA_HOME"] = Path.Combine(temporary, "local-data");
            using Process helper = Process.Start(start) ?? throw new IOException("Could not start the packaged update helper.");
            try
            {
                string ready = Path.Combine(work, "ready");
                Assert.True(SpinWait.SpinUntil(() => File.Exists(ready) || helper.HasExited, TimeSpan.FromSeconds(15)));
                Assert.True(File.Exists(ready));
                Assert.False(helper.WaitForExit(300));
                File.WriteAllText(Path.Combine(work, "accepted"), "accepted");
                Assert.False(helper.WaitForExit(300));
                mutex.ReleaseMutex();
                locked = false;

                Assert.True(helper.WaitForExit(15000));
                Assert.Equal(0, helper.ExitCode);
                Assert.True(SpinWait.SpinUntil(() => File.Exists(Path.Combine(root, "restarted")), TimeSpan.FromSeconds(5)));
            }
            finally
            {
                if (!helper.HasExited)
                {
                    helper.Kill(entireProcessTree: true);
                    helper.WaitForExit();
                }
            }
        }
        finally
        {
            if (locked)
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
