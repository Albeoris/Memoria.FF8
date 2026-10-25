namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionSettingConstraintTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "junction-setting-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly Guid Target = new("b3489bf9-5983-45c5-bfb5-c8e8c5c0f415");

    [Fact]
    public void ConditionalConstraintsUseEffectiveOptionsAndRestoreAfterACompatibleSelection()
    {
        Write("Target", $"<ModInfo><ID>{Target}</ID>{Option("mode", 1)}<ModFolder Folder='assets'/></ModInfo>");
        Write("Owner", $"<ModInfo>{Option("gate", 1)}<Compatibility><Setting><MyID>gate</MyID><MyValue>1</MyValue><ModID>{Target}</ModID><TheirID>mode</TheirID><Require>0</Require></Setting></Compatibility><ModFolder Folder='assets'/></ModInfo>");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.ReloadFailure, runtime.Catalog.Packages.Single(package => package.Id == "Junction/Owner").Status);
        Assert.Equal("Junction/Target", Assert.Single(runtime.Catalog.Files).PackageId);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-setting-conflict");
        File.WriteAllText(Path.Combine(root, "Mods/Junction/Target/mod.ini"), "option.mode = 0\n");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.All(runtime.Catalog.Packages, package => Assert.Equal(ModEffectiveStatus.Active, package.Status));
        File.WriteAllText(Path.Combine(root, "Mods/Junction/Target/mod.ini"), "option.mode = 1\n");
        File.WriteAllText(Path.Combine(root, "Mods/Junction/Owner/mod.ini"), "option.gate = 0\n");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.All(runtime.Catalog.Packages, package => Assert.Equal(ModEffectiveStatus.Active, package.Status));
    }

    [Fact]
    public void AbsentDisabledAndRemovedTargetOptionsDoNotCreateDependencies()
    {
        Write("Owner", $"<ModInfo><Compatibility><Setting><ModID>{Target}</ModID><TheirID>old-option</TheirID><Forbid>1</Forbid></Setting></Compatibility><ModFolder Folder='assets'/></ModInfo>");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.Catalog.Packages).Status);
        Write("Target", $"<ModInfo><ID>{Target}</ID>{Option("mode", 1)}<ModFolder Folder='assets'/></ModInfo>");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.All(runtime.Catalog.Packages, package => Assert.Equal(ModEffectiveStatus.Active, package.Status));
        Assert.True(runtime.SetEnabled("Junction/Target", false).Success);
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.Active, runtime.Catalog.Packages.Single(package => package.Id == "Junction/Owner").Status);
    }

    [Fact]
    public void ForbiddenValuesRejectOnlyTheDeclaringPackage()
    {
        Write("Target", $"<ModInfo><ID>{Target}</ID>{Option("mode", 1)}<ModFolder Folder='assets'/></ModInfo>");
        Write("Owner", $"<ModInfo><Compatibility><Setting><ModID>{Target}</ModID><TheirID>MODE</TheirID><Forbid>1</Forbid></Setting></Compatibility><ModFolder Folder='assets'/></ModInfo>");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.Active, runtime.Catalog.Packages.Single(package => package.Id == "Junction/Target").Status);
        Assert.Equal(ModEffectiveStatus.ReloadFailure, runtime.Catalog.Packages.Single(package => package.Id == "Junction/Owner").Status);
    }

    [Theory]
    [InlineData("<MyID>gate</MyID>")]
    [InlineData("<Require>bad</Require>")]
    [InlineData("<Require>0</Require><Require>1</Require>")]
    [InlineData("<Variable>live</Variable>")]
    public void InvalidAndDynamicConstraintsAreRejected(string fields)
    {
        Write("Owner", $"<ModInfo>{Option("gate", 1)}<Compatibility><Setting><ModID>{Target}</ModID><TheirID>mode</TheirID>{fields}</Setting></Compatibility></ModInfo>");
        Exception error = Assert.ThrowsAny<Exception>(() => JunctionModImporter.Open(Path.Combine(root, "Mods/Junction/Owner")));
        Assert.True(error is InvalidDataException or NotSupportedException);
    }

    private static string Option(string id, int value)
    {
        return $"<ConfigOption><Type>Bool</Type><ID>{id}</ID><Default>{value}</Default><Option Value='0'/><Option Value='1'/></ConfigOption>";
    }

    private void Write(string name, string manifest)
    {
        string package = Path.Combine(root, "Mods/Junction", name);
        Directory.CreateDirectory(Path.Combine(package, "assets/movies"));
        File.WriteAllText(Path.Combine(package, "mod.xml"), manifest);
        File.WriteAllText(Path.Combine(package, "assets/movies/intro.cam"), name);
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
