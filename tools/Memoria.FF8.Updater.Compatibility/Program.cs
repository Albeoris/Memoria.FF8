using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.Launcher;

namespace Memoria.FF8.Updater.Compatibility;

internal static class Program
{
    private static readonly string[] ExecutablePaths = ["Memoria.Launcher.exe", "Data/Memoria.FF8.exe", "Data/audio-host/Memoria.FF8.Music.exe"];
    private static readonly IReadOnlyDictionary<string, string> UserFiles = new Dictionary<string, string>
    {
        ["Mods/Tonberry/CompatibilityUser/mod.ini"] = "user Tonberry settings",
        ["Mods/Demaster/CompatibilityUser/mod.ini"] = "user Demaster settings",
        ["Mods/Junction/CompatibilityUser/mod.ini"] = "user Junction settings",
        ["Mods/Memoria/CompatibilityUser/mod.ini"] = "user Memoria settings",
        ["Data/compatibility-user/custom.bin"] = "unowned application data",
        ["compatibility-user-notes.txt"] = "user notes"
    };

    private static int Main(string[] arguments)
    {
        if (arguments.Length != 1 || !File.Exists(arguments[0]))
        {
            Console.Error.WriteLine("Usage: Memoria.FF8.Updater.Compatibility <existing-distribution.zip>");
            return 2;
        }
        try
        {
            VerifyFixture("PackageManifest.cs", "6ab487324d9a8b6cd5ce13b7a7aeef871da62695f0fd3987fb043b07b41f9902");
            VerifyFixture("UpdateInstaller.cs", "3bda5477acc0885565bc95d0c1b8d45e1849990eeb2ad91bb2ae7ee44b6d30a2");
            CheckRejectedLayouts();
            CheckInstallation(Path.GetFullPath(arguments[0]), injectFailure: false);
            CheckInstallation(Path.GetFullPath(arguments[0]), injectFailure: true);
            Console.WriteLine("Released updater compatibility passed: complete archive extraction, installation, rollback and retained user files.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Released updater compatibility failed: " + exception.Message);
            return 1;
        }
    }

    private static void VerifyFixture(string name, string expected)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Frozen." + name) ?? throw new InvalidDataException("Missing released updater source fixture: " + name);
        using StreamReader reader = new(stream, Encoding.UTF8);
        string text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        string actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        Require(actual.Equals(expected, StringComparison.OrdinalIgnoreCase), "The frozen released updater source changed: " + name);
    }

    private static void CheckInstallation(string archive, bool injectFailure)
    {
        WithTemporaryDirectory(root =>
        {
            PackageManifest original = CreateOriginalInstallation(root);
            byte[] originalManifest = File.ReadAllBytes(Path.Combine(root, PackageManifest.ManifestPath));
            foreach ((string path, string content) in UserFiles)
            {
                Write(root, path, Encoding.UTF8.GetBytes(content));
            }
            string staged = Path.Combine(UpdateInstaller.Prepare(root), "new");
            PackageManifest.Extract(archive, staged);
            PackageManifest incoming = PackageManifest.Read(staged);
            bool failureInjected = false;
            try
            {
                UpdateInstaller.Apply(root, path =>
                {
                    if (injectFailure && path == PackageManifest.ManifestPath)
                    {
                        failureInjected = true;
                        throw new InjectedFailureException();
                    }
                });
            }
            catch (InjectedFailureException) when (injectFailure)
            {
            }
            Require(failureInjected == injectFailure, "The rollback injection did not reach the manifest replacement boundary.");
            PackageManifest actual = PackageManifest.Read(root);
            Require(actual.BuildId == (injectFailure ? original.BuildId : incoming.BuildId), "The installed distribution identity is incorrect.");
            PackageManifest.Verify(root, actual);
            string marker = injectFailure ? "rolled-back" : "committed";
            Require(File.Exists(Path.Combine(root, UpdateInstaller.WorkDirectory, marker)), "The released installer did not record " + marker + ".");
            foreach ((string path, string content) in UserFiles)
            {
                Require(File.ReadAllText(Path.Combine(root, path)) == content, "An unowned user file changed: " + path);
            }
            if (injectFailure)
            {
                Require(File.ReadAllBytes(Path.Combine(root, PackageManifest.ManifestPath)).SequenceEqual(originalManifest), "Rollback did not restore the original manifest bytes.");
                HashSet<string> expected = new(original.Files.Select(file => file.Path), StringComparer.OrdinalIgnoreCase);
                expected.UnionWith(UserFiles.Keys);
                expected.Add(PackageManifest.ManifestPath);
                HashSet<string> installed = new(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Where(path => !path.StartsWith(UpdateInstaller.WorkDirectory + "/", StringComparison.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase);
                Require(installed.SetEquals(expected), "Rollback left unexpected installed files or removed original files.");
            }
            else
            {
                Require(actual.Files.SequenceEqual(incoming.Files), "The installed manifest inventory differs from the incoming archive.");
            }
        });
    }

    private static PackageManifest CreateOriginalInstallation(string root)
    {
        PackageFile[] files = ExecutablePaths.Select(path =>
        {
            byte[] content = Encoding.UTF8.GetBytes("original:" + path);
            Write(root, path, content);
            return new PackageFile(path, content.Length, Convert.ToHexString(SHA256.HashData(content)));
        }).ToArray();
        PackageManifest manifest = new(1, "released-updater-compatibility-original", "original", DateTimeOffset.UnixEpoch, files);
        Write(root, PackageManifest.ManifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest));
        return manifest;
    }

    private static void CheckRejectedLayouts()
    {
        WithTemporaryDirectory(root =>
        {
            string valid = Path.Combine(root, "valid");
            CreateOriginalInstallation(valid);
            foreach (string unexpected in new[] { "Unexpected/", "Unexpected/file.txt", "Mods/Memoria/", "Mods/Memoria/Example/file.txt" })
            {
                string archive = Path.Combine(root, "invalid.zip");
                ZipFile.CreateFromDirectory(valid, archive);
                using (ZipArchive zip = ZipFile.Open(archive, ZipArchiveMode.Update))
                {
                    zip.CreateEntry(unexpected);
                }
                bool rejected = false;
                try
                {
                    PackageManifest.Extract(archive, Path.Combine(root, "rejected"));
                }
                catch (InvalidDataException)
                {
                    rejected = true;
                }
                Require(rejected, "The released validator accepted an incompatible archive entry: " + unexpected);
                File.Delete(archive);
            }
        });
    }

    private static void Write(string root, string relative, byte[] content)
    {
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "Memoria.Updater.Compatibility", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            action(root);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private sealed class InjectedFailureException : Exception
    {
    }
}
