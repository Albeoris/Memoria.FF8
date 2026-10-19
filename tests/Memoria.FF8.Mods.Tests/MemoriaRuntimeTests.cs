using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class MemoriaRuntimeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-memoria-mods-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void NativePackagesInheritAutomaticEnablementAndUseTheSharedConfiguration()
    {
        Write(root, "Memoria/Example/fields/sample.json", "{}");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal("Memoria/Example", package.Id);
        Assert.Equal(ModFormat.Memoria, package.Format);
        Assert.Null(package.ExplicitEnabled);
        Assert.Equal(ModEffectiveStatus.Active, package.Status);
        Assert.Single(runtime.Catalog.FindAll(ModFormat.Memoria, "FIELDS/SAMPLE.JSON"));
        string configuration = Path.Combine(root, "Mods/Memoria/Example/mod.ini");
        Assert.False(File.Exists(configuration));
        Assert.True(runtime.SetEnabled(package.Id, false).Success);
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.Disabled, Assert.Single(runtime.Catalog.Packages).Status);
        Assert.Empty(runtime.Catalog.Files);
        Assert.Contains("enabled = 0", File.ReadAllText(configuration));
    }

    [Fact]
    public void GlobalPolicyAndExplicitNativeEnablementHaveTheSamePrecedence()
    {
        Write(root, "mods.ini", "auto_enable_new_mods = 0\n");
        Write(root, "Memoria/Inherited/fields/sample.json", "{}");
        Write(root, "Memoria/Disabled/fields/sample.json", "{}");
        Write(root, "Memoria/Disabled/mod.ini", "enabled = 0\n");
        Write(root, "Memoria/Enabled/fields/sample.json", "{}");
        Write(root, "Memoria/Enabled/mod.ini", "enabled = 1\n");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        Assert.Equal("Memoria/Enabled", Assert.Single(runtime.Catalog.Files).PackageId);
        Assert.True(runtime.SetAutoEnableNewMods(true).Success);
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.Disabled, runtime.Catalog.Packages.Single(package => package.Name == "Disabled").Status);
        Assert.Equal(ModEffectiveStatus.Active, runtime.Catalog.Packages.Single(package => package.Name == "Inherited").Status);
    }

    [Fact]
    public void EarlierGameDirectoryOwnsTheEntireNativePackageIgnoringItsCase()
    {
        string game = Path.Combine(root, "game");
        Write(game, "Memoria/example/fields/sample.json", "game");
        Write(root, "Memoria/EXAMPLE/fields/sample.json", "application");
        Write(root, "Memoria/EXAMPLE/fields/other.json", "application-only");
        using ModRuntime runtime = new(root, watch: false, additionalBaseDirectories: [game]);
        Publish(runtime);
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal(Path.Combine(game, "Mods", "Memoria", "example"), package.RootDirectory);
        Assert.Equal("game", System.Text.Encoding.UTF8.GetString(Assert.Single(runtime.Catalog.Files).ReadBytes()));
        Assert.Single(runtime.Diagnostics, diagnostic => diagnostic.Code == "duplicate-package");
        Assert.Empty(runtime.Catalog.FindAll(ModFormat.Memoria, "fields/other.json"));
    }

    [Theory]
    [InlineData(GameDataEdition.Classic, true)]
    [InlineData(GameDataEdition.Remastered, false)]
    public void NativePackagesExposeContentOnlyForSupportedEditions(GameDataEdition edition, bool compatible)
    {
        Write(root, "Memoria/Example/fields/sample.json", "{}");
        using ModRuntime runtime = new(root, format => ModCompatibility.IsCompatible(edition, format), watch: false);
        Publish(runtime);
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal(compatible, ModCompatibility.IsCompatible(edition, ModFormat.Memoria));
        Assert.Equal(compatible ? ModEffectiveStatus.Active : ModEffectiveStatus.IncompatibleEdition, package.Status);
        Assert.Equal(compatible ? 1 : 0, runtime.Catalog.Files.Count);
    }

    [Theory]
    [InlineData("Data", true, true, true)]
    [InlineData("Data", false, true, false)]
    [InlineData("Data", true, false, false)]
    [InlineData("Other", true, true, false)]
    public void DirectPackagedApplicationUsesLauncherAdjacentModsOnlyForACompletePackageLayout(string applicationName, bool hasLauncher, bool hasPackageMods, bool usesPackageMods)
    {
        string application = Path.Combine(root, applicationName);
        Write(application, "Memoria/Adjacent/fields/sample.json", "adjacent");
        if (hasPackageMods)
        {
            Write(root, "Memoria/Package/fields/sample.json", "package");
        }
        if (hasLauncher)
        {
            File.WriteAllText(Path.Combine(root, "Memoria.Launcher.exe"), "launcher");
        }
        using ModRuntime runtime = new(application, watch: false);
        Publish(runtime);
        Assert.Equal(Path.Combine(usesPackageMods ? root : application, "Mods"), Assert.Single(runtime.ModsDirectories));
        Assert.Equal(usesPackageMods ? "Memoria/Package" : "Memoria/Adjacent", Assert.Single(runtime.Catalog.Packages).Id);
    }

    [Fact]
    public void ExplicitModDirectoryOverridesThePackagedDefault()
    {
        string application = Path.Combine(root, "Data");
        string custom = Path.Combine(root, "Custom");
        Write(root, "Memoria/Package/fields/sample.json", "package");
        Write(custom, "Memoria/Explicit/fields/sample.json", "custom");
        File.WriteAllText(Path.Combine(root, "Memoria.Launcher.exe"), "launcher");
        using ModRuntime runtime = new(application, watch: false, modsDirectory: Path.Combine(custom, "Mods"));
        Publish(runtime);
        Assert.Equal(Path.Combine(custom, "Mods"), Assert.Single(runtime.ModsDirectories));
        Assert.Equal("Memoria/Explicit", Assert.Single(runtime.Catalog.Packages).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BundledDefaultsLoadWithoutUserModsAndYieldToGameAndSelectedRoots(bool explicitRoot)
    {
        string application = Path.Combine(root, "Data");
        string bundled = Path.Combine(application, "BundledMods");
        string game = Path.Combine(root, "game");
        string selected = explicitRoot ? Path.Combine(root, "custom") : root;
        string path = Path.Combine(bundled, "Memoria", "Example", "fields", "sample.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "bundled");
        Directory.CreateDirectory(Path.Combine(root, "Mods"));
        File.WriteAllText(Path.Combine(root, "Memoria.Launcher.exe"), "launcher");
        using ModRuntime runtime = new(application, watch: false, additionalBaseDirectories: [game], modsDirectory: explicitRoot ? Path.Combine(selected, "Mods") : null);
        Publish(runtime);
        Assert.Equal("bundled", System.Text.Encoding.UTF8.GetString(Assert.Single(runtime.Catalog.Files).ReadBytes()));
        Assert.Equal(bundled, runtime.ModsDirectories[^1]);
        Assert.True(runtime.SetEnabled("Memoria/Example", false).Success);
        Publish(runtime);
        Assert.Empty(runtime.Catalog.Files);
        Assert.True(runtime.SetEnabled("Memoria/Example", true).Success);
        Publish(runtime);
        Write(selected, "Memoria/Example/fields/sample.json", "selected");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Equal("selected", System.Text.Encoding.UTF8.GetString(Assert.Single(runtime.Catalog.Files).ReadBytes()));
        Write(game, "Memoria/Example/fields/sample.json", "game");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Equal("game", System.Text.Encoding.UTF8.GetString(Assert.Single(runtime.Catalog.Files).ReadBytes()));
    }

    private static void Write(string baseDirectory, string relative, string content)
    {
        string path = Path.Combine(baseDirectory, "Mods", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending());
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
