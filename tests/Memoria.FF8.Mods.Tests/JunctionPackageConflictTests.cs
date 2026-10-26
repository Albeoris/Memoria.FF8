namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionPackageConflictTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "junction-conflict-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Guid owner = Guid.NewGuid();
    private readonly Guid target = Guid.NewGuid();

    [Fact]
    public void PackageForbidsRetainDistinctGuidTargetsAlongsideRequirements()
    {
        string path = Write("Owner", owner, $"<Compatibility><Forbid ModID='{target}'>Resource conflict</Forbid><Forbid ModID='{target}'/><Require ModID='{owner}'/></Compatibility>");

        JunctionManifestMetadata metadata = JunctionModImporter.Open(path).Manifest;

        Assert.Equal(new[] { target }, metadata.ForbiddenMods);
        Assert.Equal(new[] { owner }, metadata.RequiredMods);
    }

    [Theory]
    [InlineData("<Forbid/>")]
    [InlineData("<Forbid ModID='invalid'/>")]
    [InlineData("<Forbid ModID=''/>")]
    [InlineData("<Forbid ModID='{0}' Versions='1.0'/>")]
    [InlineData("<Forbid ModID='{0}' Unknown='1'/>")]
    [InlineData("<Forbid ModID='{0}'><Nested/></Forbid>")]
    public void UnsupportedAndMalformedPackageForbidsAreRejected(string entry)
    {
        string path = Write("Owner", owner, "<Compatibility>" + string.Format(entry, target) + "</Compatibility>");

        Exception error = Assert.ThrowsAny<Exception>(() => JunctionModImporter.Open(path));

        Assert.True(error is InvalidDataException or NotSupportedException);
    }

    [Fact]
    public void ActiveConflictRejectsOnlyDeclaringContributionsAndRestoresAfterTargetDisabled()
    {
        Write("Owner", owner, Forbid(target));
        Write("Target", target);
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);

        Assert.Equal(ModEffectiveStatus.ReloadFailure, Package(runtime, "Owner").Status);
        Assert.Equal(ModEffectiveStatus.Active, Package(runtime, "Target").Status);
        Assert.Equal("Junction/Target", Assert.Single(runtime.Catalog.Files).PackageId);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-package-conflict" && diagnostic.PackageId == "Junction/Owner");

        Assert.True(runtime.SetEnabled("Junction/Target", false).Success);
        Publish(runtime);

        Assert.Equal(ModEffectiveStatus.Active, Package(runtime, "Owner").Status);
        Assert.Equal("Junction/Owner", Assert.Single(runtime.Catalog.Files).PackageId);
        Assert.DoesNotContain(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-package-conflict");
    }

    [Fact]
    public void MissingAndDisabledTargetsDoNotCreateRequirementsOrConflicts()
    {
        Write("Owner", owner, Forbid(target));
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.Active, Package(runtime, "Owner").Status);

        string path = Write("Target", target);
        File.WriteAllText(Path.Combine(path, "mod.ini"), "enabled = 0\n");
        runtime.RequestRefresh();
        Publish(runtime);

        Assert.Equal(ModEffectiveStatus.Active, Package(runtime, "Owner").Status);
        Assert.Equal(ModEffectiveStatus.Disabled, Package(runtime, "Target").Status);
        Assert.DoesNotContain(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-package-conflict");
    }

    [Fact]
    public void IncompatiblePackagesDoNotProduceConflicts()
    {
        Write("Owner", owner, Forbid(target));
        Write("Target", target);
        using ModRuntime runtime = new(root, isCompatible: _ => false, watch: false);
        Publish(runtime);

        Assert.All(runtime.Catalog.Packages, package => Assert.Equal(ModEffectiveStatus.IncompatibleEdition, package.Status));
        Assert.Empty(runtime.Catalog.Files);
        Assert.DoesNotContain(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-package-conflict");
    }

    [Fact]
    public void RequestedPackageWithoutPayloadStillMatchesForbiddenIdentity()
    {
        Write("Owner", owner, Forbid(target));
        string path = Write("Target", target);
        File.Delete(Path.Combine(path, "assets/movies/intro.cam"));
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);

        Assert.Equal(ModEffectiveStatus.NoUsableEntries, Package(runtime, "Target").Status);
        Assert.Equal(ModEffectiveStatus.ReloadFailure, Package(runtime, "Owner").Status);
        Assert.Empty(runtime.Catalog.Files);
    }

    [Fact]
    public void AmbiguousActiveIdentityStillConflictsAndRejectsDependentOwner()
    {
        Write("Owner", owner, Forbid(target));
        Write("TargetA", target);
        Write("TargetB", target);
        Write("Dependent", Guid.NewGuid(), $"<Compatibility><Require ModID='{owner}'/></Compatibility>");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);

        Assert.Equal(ModEffectiveStatus.ReloadFailure, Package(runtime, "Owner").Status);
        Assert.Equal(ModEffectiveStatus.ReloadFailure, Package(runtime, "Dependent").Status);
        Assert.Equal(new[] { "Junction/TargetA", "Junction/TargetB" }, runtime.Catalog.Files.Select(file => file.PackageId));
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-package-conflict" && diagnostic.PackageId == "Junction/Owner");
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-dependency-inactive" && diagnostic.PackageId == "Junction/Dependent");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MutualConflictsRejectBothDeclarationsRegardlessOfDiscoveryOrder(bool reverse)
    {
        Write(reverse ? "Z" : "A", owner, Forbid(target));
        Write(reverse ? "A" : "Z", target, Forbid(owner));
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);

        Assert.All(runtime.Catalog.Packages, package => Assert.Equal(ModEffectiveStatus.ReloadFailure, package.Status));
        Assert.Empty(runtime.Catalog.Files);
        Assert.Equal(2, runtime.Diagnostics.Count(diagnostic => diagnostic.Code == "junction-package-conflict"));
    }

    [Fact]
    public void SelfConflictRejectsItsOwnContributions()
    {
        Write("Owner", owner, Forbid(owner));
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);

        Assert.Equal(ModEffectiveStatus.ReloadFailure, Package(runtime, "Owner").Status);
        Assert.Empty(runtime.Catalog.Files);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-package-conflict" && diagnostic.PackageId == "Junction/Owner");
    }

    private static string Forbid(Guid id) => $"<Compatibility><Forbid ModID='{id}'>Conflicting package</Forbid></Compatibility>";

    private static ModPackage Package(ModRuntime runtime, string name) => runtime.Catalog.Packages.Single(package => package.Id == "Junction/" + name);

    private string Write(string name, Guid id, string relationships = "")
    {
        string path = Path.Combine(root, "Mods/Junction", name);
        Directory.CreateDirectory(Path.Combine(path, "assets/movies"));
        File.WriteAllText(Path.Combine(path, "mod.xml"), $"<ModInfo><ID>{id}</ID>{relationships}<ModFolder Folder='assets'/></ModInfo>");
        File.WriteAllText(Path.Combine(path, "assets/movies/intro.cam"), name);
        return path;
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
