using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DeferredTextureImportTests
{
    [Fact]
    public void Deferred_candidate_survives_polling_and_blocks_background_refresh_until_ready()
    {
        using Fixture fixture = new();
        int polls = 0;
        int discarded = 0;
        int validated = 0;
        ModCatalogSnapshot? first = null;
        using ModRuntime runtime = new(modsDirectory: fixture.Root, watch: false, prepare: candidate =>
        {
            first ??= candidate;
            Assert.Same(first, candidate);
            Assert.False(File.Exists(fixture.Output));
            polls++;
            return polls < 3 ? new(false) { Deferred = true, Discard = () => discarded++ } : new(true);
        });
        Assert.Equal(TextureImportApplyResult.Deferred, runtime.TryApplyTextureImport(fixture.Transaction, fixture.Package, () => validated++));
        Assert.False(runtime.PreparePending(true));
        Assert.False(runtime.CommitPending());
        Assert.Equal(TextureImportApplyResult.Deferred, runtime.TryApplyTextureImport(fixture.Transaction, fixture.Package, () => validated++));
        Assert.Equal(0, discarded);
        Assert.Equal(0, validated);
        Assert.Equal(TextureImportApplyResult.Applied, runtime.TryApplyTextureImport(fixture.Transaction, fixture.Package, () => validated++));
        Assert.Equal(1, validated);
        Assert.True(File.Exists(fixture.Output));
        Assert.Equal(1, runtime.Catalog.Generation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancellation_or_source_refresh_discards_candidate_without_destination_writes(bool refresh)
    {
        using Fixture fixture = new();
        int discarded = 0;
        using ModRuntime runtime = new(modsDirectory: fixture.Root, watch: false, prepare: _ => new(false) { Deferred = true, Discard = () => discarded++ });
        Assert.Equal(TextureImportApplyResult.Deferred, runtime.TryApplyTextureImport(fixture.Transaction, fixture.Package));
        if (refresh)
        {
            runtime.RequestRefresh();
            Assert.Equal(TextureImportApplyResult.Failed, runtime.TryApplyTextureImport(fixture.Transaction, fixture.Package));
        }
        else
        {
            runtime.CancelTextureImport(fixture.Transaction);
        }
        Assert.Equal(1, discarded);
        Assert.False(File.Exists(fixture.Output));
        Assert.Equal(0, runtime.Catalog.Generation);
    }

    [Fact]
    public void Final_input_validation_failure_discards_ready_candidate_before_destination_write()
    {
        using Fixture fixture = new();
        int discarded = 0;
        using ModRuntime runtime = new(modsDirectory: fixture.Root, watch: false, prepare: _ => new(true) { Discard = () => discarded++ });
        Assert.Equal(TextureImportApplyResult.Failed, runtime.TryApplyTextureImport(fixture.Transaction, fixture.Package, () => throw new InvalidDataException("Metadata changed after review.")));
        Assert.Equal(1, discarded);
        Assert.False(File.Exists(fixture.Output));
        Assert.Equal(0, runtime.Catalog.Generation);
    }

    [Fact]
    public void Normal_watch_preparation_keeps_deferred_candidate_alive_until_commit()
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-watch-deferred-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            int polls = 0;
            int discarded = 0;
            ModCatalogSnapshot? first = null;
            using ModRuntime runtime = new(modsDirectory: root, watch: false, prepare: candidate =>
            {
                first ??= candidate;
                Assert.Same(first, candidate);
                polls++;
                return polls < 3 ? new(false) { Deferred = true, Discard = () => discarded++ } : new(true);
            });
            Assert.False(runtime.PreparePending(true));
            Assert.False(runtime.PreparePending(true));
            Assert.Equal(0, discarded);
            Assert.True(runtime.PreparePending(true));
            Assert.True(runtime.CommitPending());
            Assert.Equal(0, discarded);
            Assert.Equal(1, runtime.Catalog.Generation);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "memoria-texture-deferred-" + Guid.NewGuid().ToString("N"));
        public string Output { get; }
        public ModPackage Package { get; }
        public TextureModInstallTransaction Transaction { get; }
        public Fixture()
        {
            string destination = Path.Combine(Root, "sample");
            TextureTarget target = new("ff8", "menu/sample.tex", "atlas");
            using MagickImage image = new(MagickColors.Red, 2, 2);
            TextureModInstallPlan plan = TextureModInstaller.Plan([new(target, image.ToByteArray(MagickFormat.Png))]);
            Transaction = TextureModInstaller.PrepareInstall(Root, destination, plan);
            Output = Path.Combine(destination, target.CanonicalPath());
            Package = new("Memoria/sample", "Sample", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, destination);
        }
        public void Dispose()
        {
            Transaction.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
