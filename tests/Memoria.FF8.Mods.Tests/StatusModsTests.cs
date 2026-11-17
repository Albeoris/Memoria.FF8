using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class StatusModsTests : IDisposable
{
    private const string Vitality = "status-example/Vit0/Vitality";
    private const string Meltdown = "Memoria/memoria-meltdown";
    private const string Vit0 = """
        {
          "id": "Vit0",
          "name": "${LANG}_kernel_magicspells_name_0049",
          "statModifiers": {
            "Vitality": {
              "operation": "ReducePercent",
              "value": {
                "default": 50,
                "min": 20,
                "max": 80,
                "label": { "en": "Physical defence", "ru": "Физическая защита" },
                "description": { "en": "Reduces Vitality.", "ru": "Снижает физическую защиту." }
              }
            },
            "Spirit": { "operation": "ReducePercent", "value": 25 }
          }
        }
        """;
    private readonly string root = Path.Combine(Path.GetTempPath(), "status-mods-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RecursivelyDiscoveredStatusesUseStableIdsAndLocalizedScalars()
    {
        const string folder = "Mods/Memoria/Arbitrary Folder";
        WriteDescriptor(folder, "status-example", "Collection", "Example", """{"en":"English","ru":"Русский"}""");
        Write(folder + "/statuses/nested/anything.json", Vit0);
        Write(folder + "/statuses/poison.json", """{"id":"Poison","statModifiers":{"Strength":{"operation":"ReducePercent","value":12}}}""");
        Write("BundledMods/Memoria/Depixelation/mod.ini", "enabled=0");
        Write("BundledMods/Memoria/Depixelation/art.txt", "built-in");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        var settings = runtime.ReadBehaviorSettings("Memoria:Collection");
        Assert.Equal(2, settings.Count);
        BehaviorModSetting toggle = Assert.Single(settings, setting => setting.Behavior == "component");
        Assert.Equal("Русский", toggle.Descriptor!.DescriptionForLanguage("ru"));
        Assert.Equal("English", toggle.Descriptor.DescriptionForLanguage("es"));
        BehaviorModSetting scalar = Assert.Single(settings, setting => setting.Behavior == "status");
        Assert.Equal(Vitality, scalar.Parameter);
        Assert.Equal("status-example", scalar.ParentComponent);
        Assert.Equal(("Физическая защита", "Снижает физическую защиту."), (scalar.LabelForLanguage("ru"), scalar.DescriptionForLanguage("ru")));
        Assert.Equal(("Physical defence", "Reduces Vitality."), (scalar.LabelForLanguage("ja"), scalar.DescriptionForLanguage("ja")));
        Assert.Equal((20, 80, 1, "%", 50), (scalar.Min, scalar.Max, scalar.Step, scalar.Unit, scalar.DefaultValue));
        Assert.Null(Assert.Single(runtime.ReadBehaviorSettings("Memoria/status-example")).ParentComponent);
        Assert.False(runtime.SetBehaviorSetting("Memoria:Collection", "status", Vitality, 81).Success);
        Assert.True(runtime.SetBehaviorSetting("Memoria/status-example", "status", Vitality, 73).Success);
        Publish(runtime);

        StatusMods selection = Selected(runtime.Catalog);
        Assert.Equal(73, Value(selection, NativeStatusId.Vit0, BattleStatId.Vitality));
        Assert.Equal(25, Value(selection, NativeStatusId.Vit0, BattleStatId.Spirit));
        Assert.Equal(12, Value(selection, NativeStatusId.Poison, BattleStatId.Strength));
        StatusNameOverride name = Assert.Single(selection.Names);
        Assert.Equal("$fr_kernel_magicspells_name_0049", name.Reference.ResolveKey("fr"));
        Assert.Equal("statuses/nested/anything.json", name.Source.RelativePath);

        const string renamed = "Mods/Memoria/Depixelation";
        Directory.Move(Path.Combine(root, folder), Path.Combine(root, renamed));
        WriteDescriptor(renamed, "status-example", "Memoria", "Renamed Example");
        File.Move(Path.Combine(root, renamed, "statuses/nested/anything.json"), Path.Combine(root, renamed, "statuses/renamed.json"));
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Empty(runtime.ReadBehaviorSettings("Memoria:Collection"));
        Assert.Equal(73, Assert.Single(runtime.ReadBehaviorSettings("Memoria"), setting => setting.Parameter == Vitality).Value);
        Assert.Contains(runtime.Catalog.Packages, package => package.Id == "Memoria/Depixelation" && package.Status == ModEffectiveStatus.Disabled);
        Assert.Equal("statuses/renamed.json", Selected(runtime.Catalog).ModifierSource(NativeStatusId.Vit0, BattleStatId.Vitality)!.RelativePath);
    }

    [Fact]
    public void ActivePackagesResolveByPriorityPerPropertyAndPreserveProvenance()
    {
        const string bundled = "BundledMods/Memoria/Base";
        WriteDescriptor(bundled, "a-base");
        Write(bundled + "/statuses/defence.json", Vit0);
        const string installed = "Mods/Memoria/Experiment";
        WriteDescriptor(installed, "z-experiment");
        Write(installed + "/mod.ini", "priority=8");
        Write(installed + "/statuses/different.json", """{"id":"Vit0","statModifiers":{"Vitality":{"operation":"ReducePercent","value":0}}}""");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);

        StatusMods selection = Selected(runtime.Catalog);
        Assert.Equal(0, Value(selection, NativeStatusId.Vit0, BattleStatId.Vitality));
        Assert.Equal(25, Value(selection, NativeStatusId.Vit0, BattleStatId.Spirit));
        Assert.Equal("Memoria/z-experiment", selection.ModifierSource(NativeStatusId.Vit0, BattleStatId.Vitality)!.PackageId);
        Assert.Equal("Memoria/a-base", Assert.Single(selection.Names).Source.PackageId);

        Assert.True(runtime.SetEnabled("Memoria/z-experiment", false).Success);
        Publish(runtime);
        Assert.Equal(50, Value(Selected(runtime.Catalog), NativeStatusId.Vit0, BattleStatId.Vitality));
    }

    [Fact]
    public void InstalledDescriptorReplacesBundledCopyByIdAndKeepsPreferencesAcrossFallback()
    {
        const string bundled = "BundledMods/Memoria/Meltdown";
        const string installed = "Mods/Memoria/memoria-meltdown";
        const string setting = "memoria-meltdown/Vit0/Vitality";
        WriteDescriptor(bundled, "memoria-meltdown", name: "Bundled Meltdown");
        Write(bundled + "/statuses/vit0.json", Vit0);
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        Assert.True(runtime.SetBehaviorSetting(Meltdown, "status", setting, 65).Success);
        Publish(runtime);
        Assert.True(Assert.Single(runtime.Catalog.Packages).IsBundled);

        WriteDescriptor(installed, "memoria-meltdown", name: "Installed Meltdown");
        Write(installed + "/statuses/reduction.json", Vit0.Replace("\"default\": 50", "\"default\": 55", StringComparison.Ordinal));
        runtime.RequestRefresh();
        Publish(runtime);
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal("Installed Meltdown", package.Name);
        Assert.False(package.IsBundled);
        Assert.DoesNotContain(runtime.Diagnostics, diagnostic => diagnostic.Code == "duplicate-package");
        Assert.Single(runtime.ReadBehaviorSettings(ModRuntime.MemoriaPackageId), row => row.Parameter == "memoria-meltdown");
        Assert.Equal(65, Value(Selected(runtime.Catalog), NativeStatusId.Vit0, BattleStatId.Vitality));
        Assert.True(runtime.SetHardcoreMode(true));
        Publish(runtime);
        Assert.Equal(55, Value(Selected(runtime.Catalog), NativeStatusId.Vit0, BattleStatId.Vitality));
        Assert.False(runtime.SetEnabled(Meltdown, false).Success);

        Directory.Delete(Path.Combine(root, installed), recursive: true);
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.True(Assert.Single(runtime.Catalog.Packages).IsBundled);
        Assert.Equal(50, Value(Selected(runtime.Catalog), NativeStatusId.Vit0, BattleStatId.Vitality));
        Assert.True(runtime.SetHardcoreMode(false));
        Publish(runtime);
        Assert.Equal(65, Value(Selected(runtime.Catalog), NativeStatusId.Vit0, BattleStatId.Vitality));
    }

    [Theory]
    [InlineData("""{"id":"Vit0","statModifiers":{"Spirit":{"operation":"ReducePercent","value":{"min":70,"default":60}}}}""")]
    [InlineData("""{"id":"Vit0","statModifiers":{"Spirit":{"operation":"ReducePercent","value":{"min":80,"max":60,"default":70}}}}""")]
    [InlineData("""{"id":"Vit0","statModifiers":{"Spirit":{"operation":"ReducePercent","value":{"default":60,"max":200}}}}""")]
    [InlineData("""{"id":"Vit0","statModifiers":{"Spirit":{"operation":"ReducePercent","value":101}}}""")]
    [InlineData("""{"id":"Vit0","statModifiers":{"Spirit":{"operation":"reducePercent","value":1}}}""")]
    [InlineData("""{"id":"40","statModifiers":{"Spirit":{"operation":"ReducePercent","value":1}}}""")]
    [InlineData("""{"id":"Vit0","statModifiers":{"Spirt":{"operation":"ReducePercent","value":1}}}""")]
    [InlineData("""{"id":"Vit0","name":"../some-file.txt"}""")]
    [InlineData("""{"id":"Vit0","statModifiers":{"Spirit":{"operation":"ReducePercent","value":{"default":40,"description":{"ru":"Нет английского текста."}}}}}""")]
    public void InvalidStatusDocumentsRetainLastCommittedGeneration(string invalid)
    {
        const string folder = "Mods/Memoria/Example";
        WriteDescriptor(folder, "status-example");
        Write(folder + "/statuses/valid.json", Vit0);
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        ModCatalogSnapshot committed = runtime.Catalog;

        Write(folder + "/statuses/valid.json", invalid);
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.Same(committed, runtime.Catalog);
        Assert.Equal(50, Value(Selected(runtime.Catalog), NativeStatusId.Vit0, BattleStatId.Vitality));
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidStatusOverride");
        Assert.Equal(50, Assert.Single(runtime.ReadBehaviorSettings("Memoria/status-example")).Value);
    }

    [Fact]
    public void DuplicateStatusAndSavedValuesOutsideDeclaredRangeAreRejected()
    {
        const string folder = "Mods/Memoria/Example";
        WriteDescriptor(folder, "status-example");
        Write(folder + "/statuses/valid.json", Vit0);
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        ModCatalogSnapshot committed = runtime.Catalog;

        string duplicate = Write(folder + "/statuses/x/y/duplicate.json", """{"id":"Vit0","name":"$fr_kernel_magicspells_name_0049"}""");
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.Same(committed, runtime.Catalog);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Message.Contains("more than one override document", StringComparison.Ordinal));

        File.Delete(duplicate);
        Write("Mods/Memoria/memoria-settings.json", $$$"""{"statusSettings":{"{{{Vitality}}}":81}}""");
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.Same(committed, runtime.Catalog);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Message.Contains("Saved status setting", StringComparison.Ordinal));
    }

    [Fact]
    public void DifficultyPolicyLocksDeclaredDefaultsAndRestoresUserPreferences()
    {
        const string installed = "Mods/Memoria/Unrelated Directory Name";
        WriteDescriptor(installed, "memoria-meltdown");
        Write(installed + "/statuses/renamed.json", Vit0.Replace("\"default\": 50", "\"default\": 60", StringComparison.Ordinal));
        Write(installed + "/mod.ini", "enabled=0");
        Write("Mods/Memoria/memoria-settings.json", """{"statusSettings":{"memoria-meltdown/Vit0/Vitality":73}}""");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        Assert.Empty(Selected(runtime.Catalog).Modifiers);

        runtime.SetHardcoreMode(true);
        Publish(runtime);
        Assert.Equal(60, Value(Selected(runtime.Catalog), NativeStatusId.Vit0, BattleStatId.Vitality));
        BehaviorModSetting setting = Assert.Single(runtime.ReadBehaviorSettings(Meltdown));
        Assert.Equal(60, setting.Value);
        Assert.Contains("Hardcore", runtime.GetBehaviorSettingLockReason("Memoria", setting.Behavior, setting.Parameter)!);
        Assert.False(runtime.SetBehaviorSetting(Meltdown, setting.Behavior, setting.Parameter, 80).Success);
        Assert.False(runtime.SetEnabled(Meltdown, false).Success);

        runtime.SetHardcoreMode(false);
        Publish(runtime);
        Assert.Empty(Selected(runtime.Catalog).Modifiers);
        Assert.Equal(73, Assert.Single(runtime.ReadBehaviorSettings(Meltdown)).Value);
        Assert.True(runtime.SetEnabled(Meltdown, true).Success);
        Publish(runtime);
        Assert.Equal(73, Value(Selected(runtime.Catalog), NativeStatusId.Vit0, BattleStatId.Vitality));

        runtime.SetHardcoreMode(true);
        Directory.Delete(Path.Combine(root, installed), recursive: true);
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Empty(Selected(runtime.Catalog).Modifiers);
    }

    private static StatusMods Selected(ModCatalogSnapshot catalog)
    {
        Assert.True(StatusMods.TryPrepare(catalog, out StatusMods selection, out IReadOnlyList<ModDiagnostic> diagnostics), string.Join(Environment.NewLine, diagnostics));
        return selection;
    }

    private static int Value(StatusMods selection, NativeStatusId status, BattleStatId stat)
    {
        return Assert.Single(selection.Modifiers, modifier => modifier.Status == status && modifier.Stat == stat).Value;
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true), string.Join(Environment.NewLine, runtime.Diagnostics));
        Assert.True(runtime.CommitPending());
    }

    private void WriteDescriptor(string folder, string id, string group = "Memoria", string name = "Status Example", string description = """{"en":"Modify status contributions."}""")
    {
        Write(folder + "/mod.json", $$$"""{"id":"{{{id}}}","name":"{{{name}}}","package":"{{{group}}}","description":{{{description}}}}""");
    }

    private string Write(string relative, string content)
    {
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
