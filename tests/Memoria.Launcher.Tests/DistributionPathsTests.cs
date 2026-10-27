namespace Memoria.Launcher.Tests;

public sealed class DistributionPathsTests : IDisposable
{
    private readonly string directory = DistributionPaths.GetFullPath(Path.Combine(Path.GetTempPath(), "Memoria.DistributionPaths.Tests", Guid.NewGuid().ToString("N")));

    [Theory]
    [InlineData("var")]
    [InlineData("tmp")]
    [InlineData("etc")]
    public void MacSystemAliasesUseOnlyTheirExactPrivateTargets(string name)
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS system directory aliases.");
        string alias = "/" + name;
        string target = "/private/" + name;
        Assert.Equal(target, new DirectoryInfo(alias).ResolveLinkTarget(returnFinalTarget: false)!.FullName);
        Assert.Equal(target, DistributionPaths.GetFullPath(alias));
        Assert.Equal(target + "/Memoria/new.app", DistributionPaths.GetFullPath(alias + "/Memoria/new.app"));
        Assert.Equal(alias + "-other/Memoria", DistributionPaths.GetFullPath(alias + "-other/Memoria"));
        Assert.Equal(UpdateInstaller.MutexName(alias + "/Memoria"), UpdateInstaller.MutexName(target + "/Memoria"));
        UpdateInstaller.RejectLinks(alias + "/Memoria/new.app");
    }

    [Fact]
    public void CustomLinkedRootsAndDescendantsRemainRejected()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix links; Windows creation requires additional machine privileges.");
        Directory.CreateDirectory(directory);
        string outside = Directory.CreateDirectory(Path.Combine(directory, "outside")).FullName;
        string root = Directory.CreateDirectory(Path.Combine(directory, "installation")).FullName;
        string linkedRoot = Path.Combine(directory, "linked-root");
        Directory.CreateSymbolicLink(linkedRoot, outside);
        string linkedData = Path.Combine(root, "Data");
        Directory.CreateSymbolicLink(linkedData, outside);

        Assert.Equal(Path.GetFullPath(linkedRoot), DistributionPaths.GetFullPath(linkedRoot));
        Assert.Throws<IOException>(() => UpdateInstaller.RejectLinks(linkedRoot));
        Assert.Throws<IOException>(() => UpdateInstaller.RejectLinks(Path.Combine(linkedData, "missing")));
        Assert.Throws<InvalidDataException>(() => PackageManifest.LoadOptional(root));
        Assert.Throws<InvalidDataException>(() => PackageManifest.LoadOptional(linkedRoot));
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract("missing.zip", linkedRoot));
        Assert.Throws<InvalidDataException>(() => PackageManifest.Extract("missing.tar.gz", linkedRoot));
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    [Fact]
    public void LinkedStagingAndManifestFilesRemainRejected()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix links; Windows creation requires additional machine privileges.");
        Directory.CreateDirectory(directory);
        string outside = Directory.CreateDirectory(Path.Combine(directory, "outside")).FullName;
        string root = Directory.CreateDirectory(Path.Combine(directory, "installation")).FullName;
        string staging = Path.Combine(root, UpdateInstaller.WorkDirectory);
        Directory.CreateSymbolicLink(staging, outside);
        Assert.Throws<IOException>(() => UpdateInstaller.Prepare(root));
        string data = Directory.CreateDirectory(Path.Combine(root, "Data")).FullName;
        string target = Path.Combine(outside, "manifest.json");
        File.WriteAllText(target, "{}");
        File.CreateSymbolicLink(Path.Combine(data, "distribution.json"), target);
        Assert.Throws<InvalidDataException>(() => PackageManifest.Read(root));
        Assert.Equal("{}", File.ReadAllText(target));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
