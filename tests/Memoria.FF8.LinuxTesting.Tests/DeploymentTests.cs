using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.LinuxTesting;
using Xunit;

namespace Memoria.FF8.LinuxTesting.Tests;

public sealed class DeploymentTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-deployment-tests-" + Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(directory, "target");
    private string Source => Path.Combine(directory, "source");

    public DeploymentTests()
    {
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Source);
    }

    [Fact]
    public void ManifestUsesRelativeSlashPathsAndSha256()
    {
        Write(Source, "nested/test.txt", "abc");
        Dictionary<string, string> manifest = Deployment.Manifest(Source);
        Assert.Single(manifest);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", manifest["nested/test.txt"]);
    }

    [Fact]
    public void InitializationRefusesUnownedNonemptyAndUnrecognizedOwnedDirectories()
    {
        Write(Root, "keep.txt", "personal");
        Assert.Throws<InvalidDataException>(() => Deployment.Initialize(Root));
        Write(Root, ".memoria-linux-test-owner", "different owner");
        Assert.Throws<InvalidDataException>(() => Deployment.Initialize(Root));
        Assert.Equal("personal", File.ReadAllText(Path.Combine(Root, "keep.txt")));
    }

    [Fact]
    public void InitializationAcceptsExistingReadOnlyOwnershipMarker()
    {
        Deployment.Initialize(Root);
        string marker = Path.Combine(Root, ".memoria-linux-test-owner");
        FileAttributes attributes = File.GetAttributes(marker);
        File.SetAttributes(marker, attributes | FileAttributes.ReadOnly);
        try
        {
            Deployment.Initialize(Root);
            Assert.Equal("memoria-linux-test-v1", File.ReadAllText(marker));
            Assert.True((File.GetAttributes(marker) & FileAttributes.ReadOnly) != 0);
        }
        finally
        {
            File.SetAttributes(marker, attributes);
        }
    }

    [Fact]
    public void SyncIsIdempotentAndRemovesOnlyObsoleteOwnedFiles()
    {
        Write(Source, "old.txt", "old");
        Write(Source, "unchanged.txt", "same");
        Deployment.Sync(Root, Deployment.Manifest(Source), Source);
        Write(Root, "runs/result.txt", "keep");
        File.Delete(Path.Combine(Source, "old.txt"));
        Write(Source, "nested/new.txt", "new");
        Dictionary<string, string> incoming = Deployment.Manifest(Source);
        Deployment.Sync(Root, incoming, Source);
        Deployment.Sync(Root, incoming, Source);
        Deployment.Commit(Root, incoming);
        Assert.False(File.Exists(Path.Combine(Root, "payload/old.txt")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Root, "payload/nested/new.txt")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Root, "runs/result.txt")));
        Assert.False(File.Exists(Path.Combine(Root, "deployment.json")));
    }

    [Fact]
    public void UnknownAndEditedPayloadFilesArePreserved()
    {
        DeployOld();
        Write(Root, "payload/unknown.txt", "personal");
        Assert.Throws<InvalidDataException>(() => Deployment.Prepare(Root, new()));
        Assert.Equal("personal", File.ReadAllText(Path.Combine(Root, "payload/unknown.txt")));
        File.Delete(Path.Combine(Root, "payload/unknown.txt"));
        Write(Root, "payload/app.txt", "edited");
        Assert.Throws<InvalidDataException>(() => Deployment.Sync(Root, new(), Source));
        Assert.Equal("edited", File.ReadAllText(Path.Combine(Root, "payload/app.txt")));
    }

    [Fact]
    public void MissingOwnedFilesCanBeRestored()
    {
        Dictionary<string, string> incoming = DeployOld();
        File.Delete(Path.Combine(Root, "payload/app.txt"));
        Deployment.Sync(Root, incoming, Source);
        Assert.Equal("old", File.ReadAllText(Path.Combine(Root, "payload/app.txt")));
    }

    [Fact]
    public void UnsafeManifestPathsAndDigestsFailBeforeWriting()
    {
        string[] paths = ["../outside", "/absolute", "a//b", "a/./b", "a\\b", "C:/drive", "", "a/../b"];
        foreach (string path in paths)
        {
            Assert.Throws<InvalidDataException>(() => Deployment.Prepare(Root, new() { [path] = Digest("x") }));
        }
        Assert.Throws<InvalidDataException>(() => Deployment.Prepare(Root, new() { ["file"] = "invalid" }));
        Assert.Throws<InvalidDataException>(() => Deployment.Prepare(Root, new() { ["file"] = Digest("x").ToUpperInvariant() }));
        Assert.Throws<InvalidDataException>(() => Deployment.Prepare(Root, new() { ["a"] = Digest("x"), ["a/b"] = Digest("x") }));
        Assert.False(Directory.Exists(Root));
    }

    [Fact]
    public void ChangedSourceAndIncompleteCommitPreserveActivePayload()
    {
        DeployOld();
        Write(Source, "app.txt", "new");
        Dictionary<string, string> incoming = Deployment.Manifest(Source);
        Write(Source, "app.txt", "changed after manifest");
        Assert.Throws<InvalidDataException>(() => Deployment.Sync(Root, incoming, Source));
        Assert.Throws<InvalidDataException>(() => Deployment.Commit(Root, incoming));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Root, "payload/app.txt")));
    }

    [Fact]
    public void EditedActivePayloadAfterPrepareIsNeverReplaced()
    {
        DeployOld();
        Dictionary<string, string> incoming = new() { ["app.txt"] = Digest("new") };
        Deployment.Prepare(Root, incoming);
        Write(Root, "deployment-staging/app.txt", "new");
        Write(Root, "payload/app.txt", "personal edit");
        Assert.Throws<InvalidDataException>(() => Deployment.Commit(Root, incoming));
        Assert.Equal("personal edit", File.ReadAllText(Path.Combine(Root, "payload/app.txt")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Root, "deployment-staging/app.txt")));
    }

    [Fact]
    public void RecoverCompletesSwapInterruptedAfterBackupRename()
    {
        DeployOld();
        Dictionary<string, string> incoming = new() { ["app.txt"] = Digest("new") };
        Deployment.Prepare(Root, incoming);
        Write(Root, "deployment-staging/app.txt", "new");
        Directory.Move(Path.Combine(Root, "payload"), Path.Combine(Root, "deployment-backup"));
        Deployment.Recover(Root);
        Deployment.Recover(Root);
        Assert.Equal("new", File.ReadAllText(Path.Combine(Root, "payload/app.txt")));
        Assert.False(Directory.Exists(Path.Combine(Root, "deployment-backup")));
        Assert.False(File.Exists(Path.Combine(Root, "deployment.json")));
        Assert.Equal(incoming, JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Root, "payload-manifest.json"))));
    }

    [Fact]
    public void RecoverCompletesSwapInterruptedBeforeReceiptWrite()
    {
        DeployOld();
        Dictionary<string, string> incoming = new() { ["app.txt"] = Digest("new") };
        Deployment.Prepare(Root, incoming);
        Write(Root, "deployment-staging/app.txt", "new");
        Directory.Move(Path.Combine(Root, "payload"), Path.Combine(Root, "deployment-backup"));
        Directory.Move(Path.Combine(Root, "deployment-staging"), Path.Combine(Root, "payload"));
        Deployment.Recover(Root);
        Deployment.Commit(Root, incoming);
        Assert.Equal("new", File.ReadAllText(Path.Combine(Root, "payload/app.txt")));
        Assert.False(File.Exists(Path.Combine(Root, "deployment.json")));
    }

    [Fact]
    public void RecoverRestoresOriginalWhenInterruptedStageIsIncomplete()
    {
        DeployOld();
        Deployment.Prepare(Root, new() { ["app.txt"] = Digest("new") });
        Directory.Move(Path.Combine(Root, "payload"), Path.Combine(Root, "deployment-backup"));
        Assert.Throws<InvalidDataException>(() => Deployment.Recover(Root));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Root, "payload/app.txt")));
        Assert.True(File.Exists(Path.Combine(Root, "deployment.json")));
    }

    [Fact]
    public void RecoverPreservesEditedBackupAndIncomingPayload()
    {
        DeployOld();
        Deployment.Prepare(Root, new() { ["app.txt"] = Digest("new") });
        Write(Root, "deployment-staging/app.txt", "new");
        Directory.Move(Path.Combine(Root, "payload"), Path.Combine(Root, "deployment-backup"));
        Directory.Move(Path.Combine(Root, "deployment-staging"), Path.Combine(Root, "payload"));
        Write(Root, "deployment-backup/app.txt", "personal");
        Assert.Throws<InvalidDataException>(() => Deployment.Recover(Root));
        Assert.Equal("personal", File.ReadAllText(Path.Combine(Root, "deployment-backup/app.txt")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Root, "payload/app.txt")));
    }

    [Fact]
    public void DifferentPendingDeploymentIsArchivedAndEditedStageIsPreserved()
    {
        DeployOld();
        Deployment.Prepare(Root, new() { ["app.txt"] = Digest("first") });
        Write(Root, "deployment-staging/app.txt", "first");
        Deployment.Prepare(Root, new() { ["app.txt"] = Digest("second") });
        string abandoned = Assert.Single(Directory.EnumerateDirectories(Root, "deployment-abandoned-*"));
        Assert.Equal("first", File.ReadAllText(Path.Combine(abandoned, "app.txt")));
        Write(Root, "deployment-staging/app.txt", "personal");
        Assert.Throws<InvalidDataException>(() => Deployment.Prepare(Root, new()));
        Assert.Equal("personal", File.ReadAllText(Path.Combine(Root, "deployment-staging/app.txt")));
    }

    [Fact]
    public void ArchiveIsFullyVerifiedBeforeStagedFilesAreReplaced()
    {
        DeployOld();
        Dictionary<string, string> incoming = new() { ["app.txt"] = Digest("new"), ["nested/data"] = Digest("data") };
        Deployment.Prepare(Root, incoming);
        string archive = Archive(("app.txt", "new"), ("nested/data", "wrong"));
        Assert.Throws<InvalidDataException>(() => Deployment.Accept(Root, incoming, archive));
        Assert.Empty(Deployment.Manifest(Path.Combine(Root, "deployment-staging")));
        Assert.True(File.Exists(archive));
        File.Delete(archive);
        archive = Archive(("app.txt", "new"), ("nested/data", "data"));
        Deployment.Accept(Root, incoming, archive);
        Deployment.Commit(Root, incoming);
        Assert.False(File.Exists(archive));
        Assert.Equal("data", File.ReadAllText(Path.Combine(Root, "payload/nested/data")));
    }

    [Fact]
    public void ArchiveRejectsDuplicateUnexpectedTraversalAndSymlinkEntries()
    {
        Deployment.Initialize(Root);
        Dictionary<string, string> incoming = new() { ["app.txt"] = Digest("new") };
        foreach (string name in new[] { "../outside", "unexpected", "app.txt/" })
        {
            string archive = Archive((name, "new"));
            Assert.Throws<InvalidDataException>(() => Deployment.Accept(Root, incoming, archive));
            File.Delete(archive);
        }
        string duplicate = Archive(("app.txt", "new"), ("app.txt", "new"));
        Assert.Throws<InvalidDataException>(() => Deployment.Accept(Root, incoming, duplicate));
        File.Delete(duplicate);
        string link = Archive(("app.txt", "new"));
        using (ZipArchive zip = ZipFile.Open(link, ZipArchiveMode.Update))
        {
            zip.Entries[0].ExternalAttributes = 0xA1FF << 16;
        }
        Assert.Throws<InvalidDataException>(() => Deployment.Accept(Root, incoming, link));
        Assert.Empty(Deployment.Manifest(Path.Combine(Root, "deployment-staging")));
    }

    [Fact]
    public void ArchiveOutsideInboxIsPreserved()
    {
        Deployment.Initialize(Root);
        string archive = Path.Combine(directory, "outside.zip");
        File.WriteAllText(archive, "keep");
        Assert.Throws<InvalidDataException>(() => Deployment.Accept(Root, new(), archive));
        Assert.Equal("keep", File.ReadAllText(archive));
    }

    [Fact]
    [Trait("Platform", "Linux")]
    public void InitializationPreservesUnrelatedAssetAndUserDirectoryLinks()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Symbolic link deployment validation requires Linux.");
        }
        Deployment.Initialize(Root);
        Write(Source, "personal", "keep");
        string assets = Path.Combine(Root, "assets");
        string unrelated = Path.Combine(Root, "user-files");
        Directory.CreateSymbolicLink(assets, Source);
        Directory.CreateSymbolicLink(unrelated, Source);
        try
        {
            Deployment.Initialize(Root);
            Deployment.Sync(Root, new(), Source);
            Assert.Equal(Source, new DirectoryInfo(assets).LinkTarget);
            Assert.Equal(Source, new DirectoryInfo(unrelated).LinkTarget);
            Assert.Equal("keep", File.ReadAllText(Path.Combine(Source, "personal")));
        }
        finally
        {
            Directory.Delete(assets);
            Directory.Delete(unrelated);
        }
    }

    [Fact]
    [Trait("Platform", "Linux")]
    public void SymlinkValidationPreservesExternalData()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Symbolic link deployment validation requires Linux.");
        }
        Deployment.Initialize(Root);
        Write(Source, "personal", "keep");
        File.CreateSymbolicLink(Path.Combine(Root, "Memoria.FF8.LinuxTesting"), Path.Combine(Source, "personal"));
        Assert.Throws<InvalidDataException>(() => Deployment.Initialize(Root));
        Assert.Throws<InvalidDataException>(() => Deployment.Manifest(Root));
        File.Delete(Path.Combine(Root, "Memoria.FF8.LinuxTesting"));
        Directory.CreateSymbolicLink(Path.Combine(Root, "payload"), Source);
        Assert.Throws<InvalidDataException>(() => Deployment.Prepare(Root, new()));
        Directory.Delete(Path.Combine(Root, "payload"));
        File.CreateSymbolicLink(Path.Combine(Root, "deployment.json"), Path.Combine(directory, "missing"));
        Assert.Throws<InvalidDataException>(() => Deployment.Recover(Root));
        File.Delete(Path.Combine(Root, "deployment.json"));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Source, "personal")));
    }

    private Dictionary<string, string> DeployOld()
    {
        Write(Source, "app.txt", "old");
        Dictionary<string, string> incoming = Deployment.Manifest(Source);
        Deployment.Sync(Root, incoming, Source);
        return incoming;
    }

    private string Archive(params (string Name, string Content)[] entries)
    {
        string inbox = Path.Combine(Root, "inbox");
        Directory.CreateDirectory(inbox);
        string path = Path.Combine(inbox, Guid.NewGuid().ToString("N") + ".zip");
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entry.Name).Open());
            writer.Write(entry.Content);
        }
        return path;
    }

    private static string Digest(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static void Write(string root, string relative, string content)
    {
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }
}
