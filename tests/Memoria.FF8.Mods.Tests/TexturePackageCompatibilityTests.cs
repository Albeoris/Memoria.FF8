using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class TexturePackageCompatibilityTests
{
    [Fact]
    public void Remastered_texture_package_discovers_toggles_and_survives_refresh_without_changing_priority()
    {
        using Fixture fixture = new();
        fixture.Write(fixture.Target.CanonicalPath(), fixture.Png);
        fixture.Write("mod.ini", System.Text.Encoding.UTF8.GetBytes("enabled=1\npriority=37\n"));
        fixture.Write("README.md", [65]);
        using ModRuntime runtime = fixture.Runtime();
        Assert.True(Pump(runtime));
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        Assert.True(package.TextureContentOnly);
        Assert.Equal(ModEffectiveStatus.Active, package.Status);
        Assert.True(runtime.SetEnabled(package.Id, false).Success);
        Assert.True(Pump(runtime));
        Assert.Equal(ModEffectiveStatus.Disabled, Assert.Single(runtime.Catalog.Packages).Status);
        Assert.True(runtime.SetEnabled(package.Id, true).Success);
        Assert.True(Pump(runtime));
        runtime.RequestRefresh();
        Pump(runtime);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.Catalog.Packages).Status);
        Assert.Contains("priority=37", File.ReadAllText(Path.Combine(fixture.PackageRoot, "mod.ini")));
    }

    [Theory]
    [InlineData("gameplay.json")]
    [InlineData("README.json")]
    [InlineData("textures/patch.dll")]
    public void Mixed_gameplay_and_unsupported_contributions_remain_incompatible(string unsupported)
    {
        using Fixture fixture = new();
        fixture.Write(fixture.Target.CanonicalPath(), fixture.Png);
        fixture.Write(unsupported, [1]);
        using ModRuntime runtime = fixture.Runtime();
        Pump(runtime);
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        Assert.False(package.TextureContentOnly);
        Assert.Equal(ModEffectiveStatus.IncompatibleEdition, package.Status);
        Assert.False(runtime.SetEnabled(package.Id, true).Success);
        using TextureModInstallTransaction transaction = fixture.Transaction();
        Assert.Equal(TextureImportApplyResult.Failed, runtime.TryApplyTextureImport(transaction, package with { TextureContentOnly = true }));
        Assert.Equal(fixture.Png, File.ReadAllBytes(Path.Combine(fixture.PackageRoot, fixture.Target.CanonicalPath())));
    }

    [Fact]
    public void New_texture_import_is_eligible_through_forced_normal_rediscovery()
    {
        using Fixture fixture = new();
        using ModRuntime runtime = fixture.Runtime();
        Pump(runtime);
        using (TextureModInstallTransaction transaction = fixture.Transaction(newDescriptor: true))
        {
            Assert.Equal(TextureImportApplyResult.Applied, runtime.TryApplyTextureImport(transaction, fixture.Package));
            Assert.True(Assert.Single(runtime.Catalog.Packages).TextureContentOnly);
        }
        runtime.RequestRefresh();
        Pump(runtime);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.Catalog.Packages).Status);
        Assert.True(Assert.Single(runtime.Catalog.Packages).TextureContentOnly);
    }

    [Fact]
    public void Disabled_mixed_package_saves_import_without_enabling_or_publishing_it()
    {
        using Fixture fixture = new();
        fixture.Write("mod.ini", System.Text.Encoding.UTF8.GetBytes("enabled=0\n"));
        fixture.Write("gameplay.json", [1]);
        using ModRuntime runtime = fixture.Runtime();
        Pump(runtime);
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        using (TextureModInstallTransaction transaction = fixture.Transaction())
        {
            long generation = runtime.Catalog.Generation;
            Assert.Equal(TextureImportApplyResult.Applied, runtime.TryApplyTextureImport(transaction, package));
            Assert.Equal(generation, runtime.Catalog.Generation);
        }
        Pump(runtime);
        Assert.False(Assert.Single(runtime.Catalog.Packages).RequestedEnabled);
        Assert.Equal(ModEffectiveStatus.IncompatibleEdition, Assert.Single(runtime.Catalog.Packages).Status);
        Assert.Equal("enabled=0\n", File.ReadAllText(Path.Combine(fixture.PackageRoot, "mod.ini")));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Texture_localization_and_documentation_paths_are_bounded(bool manifest, bool texture)
    {
        using Fixture fixture = new();
        if (texture)
        {
            fixture.Write(fixture.Target.CanonicalPath(), fixture.Png);
        }
        if (manifest)
        {
            fixture.Write("Localization/localization.manifest.json", [123, 125]);
        }
        fixture.Write("Localization/text/messages.json", [123, 125]);
        fixture.Write("docs/editing.md", [65]);
        fixture.Write("LICENSE", [65]);
        using ModRuntime runtime = fixture.Runtime();
        Pump(runtime);
        Assert.Equal(manifest && texture, Assert.Single(runtime.Catalog.Packages).TextureContentOnly);
    }

    private static bool Pump(ModRuntime runtime)
    {
        runtime.PreparePending(true);
        return runtime.CommitPending();
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "memoria-texture-compat-" + Guid.NewGuid().ToString("N"));
        public string ModsRoot => Path.Combine(Root, "Mods");
        public string NativeRoot => Path.Combine(ModsRoot, "Memoria");
        public string PackageRoot => Path.Combine(NativeRoot, "sample");
        public TextureTarget Target { get; } = new("ff8", "menu/sample.tex", "atlas");
        public byte[] Png { get; }
        public ModPackage Package => new("Memoria/sample", "Sample", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, PackageRoot);
        public Fixture()
        {
            using MagickImage image = new(MagickColors.Red, 2, 2);
            Png = image.ToByteArray(MagickFormat.Png);
            Directory.CreateDirectory(ModsRoot);
        }
        public ModRuntime Runtime() => new(applicationBaseDirectory: Root, modsDirectory: ModsRoot, watch: false, isCompatible: format => format != ModFormat.Memoria, edition: "Remastered");
        public void Write(string relative, byte[] bytes)
        {
            string path = Path.Combine(PackageRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }
        public TextureModInstallTransaction Transaction(bool newDescriptor = false)
        {
            TextureModInstallPlan plan = TextureModInstaller.Plan([new(Target, Png)]);
            if (newDescriptor)
            {
                plan = TextureModInstaller.WithNewDescriptor(plan, "sample", "Sample");
            }
            return TextureModInstaller.PrepareInstall(NativeRoot, PackageRoot, plan);
        }
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
