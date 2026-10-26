namespace Memoria.FF8.Mods.Tests;

public sealed class ModResourceDiagnosticTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mod-resource-diagnostics-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RejectedResourceWarnsWithoutChangingActivationOrSchedulingRetry()
    {
        Write(1);
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        ModServiceView before = runtime.View;
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        ModDiagnostic rejected = new("resource-rejected", "The selected texture is incompatible; original pixels remain active.", package.Id, "textures/world/dat/texl/texl_001_0.png");

        await Task.Run(() => runtime.ReportResourceRejection(rejected), TestContext.Current.CancellationToken);
        runtime.ReportResourceRejection(rejected with { Message = "Another consumer rejected the same file." });

        Assert.Empty(before.Diagnostics);
        Assert.Single(runtime.Diagnostics);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.InstalledPackages).Status);
        Assert.Equal(ModUpdateOutcome.PublishedWithWarnings, runtime.View.Outcome);
        ModPackageView view = Assert.Single(runtime.View.Packages);
        Assert.Equal("Active", view.StatusText);
        Assert.True(view.NeedsAttention);
        Assert.Equal(rejected.Message, view.Problem);
        Assert.False(runtime.IsReloadPending);
        Assert.False(runtime.PreparePending(force: true));
    }

    [Fact]
    public void UnchangedDiscoveryRetainsWarningAndNewPublicationRecomputesIt()
    {
        Write(1);
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        string package = Assert.Single(runtime.Catalog.Packages).Id;
        runtime.ReportResourceRejection(new("resource-rejected", "Invalid resource.", package, "textures/world/dat/texl/texl_001_0.png"));

        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.Single(runtime.Diagnostics);
        Assert.Equal(ModUpdateOutcome.PublishedWithWarnings, runtime.View.Outcome);

        Write(2);
        runtime.Refresh();
        Publish(runtime);
        Assert.Empty(runtime.Diagnostics);
        Assert.Equal(ModUpdateOutcome.Idle, runtime.View.Outcome);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.Catalog.Packages).Status);

        runtime.ReportResourceRejection(new("resource-rejected", "Invalid resource.", package, "textures/world/dat/texl/texl_001_0.png"));
        Assert.True(runtime.SetEnabled(package, false).Success);
        Publish(runtime);
        Assert.Empty(runtime.Diagnostics);
        Assert.Equal("Disabled", Assert.Single(runtime.View.Packages).StatusText);
    }

    private void Write(byte value)
    {
        string path = Path.Combine(root, "Mods", "Demaster", "pack", "textures", "world", "dat", "texl", "texl_001_0.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [value]);
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
