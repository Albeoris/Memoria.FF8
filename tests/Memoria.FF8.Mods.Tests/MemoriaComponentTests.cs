using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class MemoriaComponentTests : IDisposable
{
    private const string Group = ModRuntime.MemoriaPackageId;
    private const string Meltdown = "memoria-meltdown";
    private const string Vitality = "memoria-meltdown/Vit0/Vitality";
    private const string Spirit = "memoria-meltdown/Vit0/Spirit";
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-components-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CatalogGroupsBundledComponentsAndSavesChangesTogether()
    {
        WriteBundledMeltdown();
        Write("Mods/Memoria/Meltdown/config.json", """{"meltdown":{"VitalityReductionPercent":73,"SpiritReductionPercent":37}}""");
        Write("BundledMods/Memoria/Girl Next Door/mod.ini", "enabled = 1");
        Write("BundledMods/Memoria/Girl Next Door/GirlNextDoor.png", "art");
        Write("Mods/Memoria/User Mod/mod.ini", "enabled=0");
        using (ModRuntime runtime = new(root, watch: false))
        {
            Publish(runtime);
            Assert.Equal(new[] { Group, "Memoria/User Mod" }, runtime.View.Packages.Select(view => view.Package.Id));
            Assert.False(runtime.View.Packages[0].CanToggle);
            BehaviorModSetting book = Find(runtime, "Girl Next Door");
            BehaviorModSetting vitality = Find(runtime, Vitality);
            Assert.Equal(("Items", "toggle", 1), (book.Category, book.Control, book.Value));
            Assert.Contains(runtime.Catalog.Files, file => file.PackageId == "Memoria/Girl Next Door" && file.RelativePath == "GirlNextDoor.png");
            Assert.Equal((Meltdown, "slider", 73), (vitality.ParentComponent, vitality.Control, vitality.Value));
            Assert.False(runtime.SetBehaviorSettings(Group, [book with { Value = 0 }, vitality with { Value = 101 }]).Success);
            Assert.Equal(1, Find(runtime, "Girl Next Door").Value);
            Assert.True(runtime.SetBehaviorSettings(Group, [book with { Value = 0 }, vitality with { Value = 66 }]).Success);
            Publish(runtime);
            Assert.DoesNotContain(runtime.Catalog.Files, file => file.PackageId == "Memoria/Girl Next Door");
            Assert.True(StatusMods.TryPrepare(runtime.Catalog, out StatusMods statuses, out _));
            Assert.Equal(66, Assert.Single(statuses.Modifiers, rule => rule.Stat == BattleStatId.Vitality).Value);
            Assert.Equal(37, Assert.Single(statuses.Modifiers, rule => rule.Stat == BattleStatId.Spirit).Value);
            using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "Mods/Memoria/memoria-settings.json")));
            Assert.False(settings.RootElement.TryGetProperty("meltdown", out _));
        }
        using ModRuntime reopened = new(root, watch: false);
        Publish(reopened);
        Assert.Equal(0, Find(reopened, "Girl Next Door").Value);
        Assert.Equal(66, Find(reopened, Vitality).Value);
    }

    [Fact]
    public void ForcedRulesLockOnlyTheirOwnSettingsAndRestorePreferences()
    {
        WriteBundledMeltdown();
        Write("Mods/Memoria/Meltdown/mod.ini", "enabled=0");
        Write("Mods/Memoria/Meltdown/config.json", """{"meltdown":{"VitalityReductionPercent":73,"SpiritReductionPercent":37}}""");
        Write("BundledMods/Memoria/Level-Based Stat Bonuses/mod.ini", "enabled=0");
        Write("BundledMods/Memoria/Level-Based Stat Bonuses/README.md", "stat bonuses");
        Write("BundledMods/Memoria/FF7EnemyIntegration/extension.json", "extension");
        Write("Mods/Memoria/FF7EnemyIntegration/mod.ini", "enabled=0");
        Write("BundledMods/Memoria/Girl Next Door/mod.ini", "enabled=0");
        Write("BundledMods/Memoria/Girl Next Door/GirlNextDoor.png", "art");
        using ModRuntime runtime = new(root, watch: false);
        runtime.SetHardcoreMode(true);
        runtime.SetFf7BridgeEnemyIntegration(true);
        Publish(runtime);
        foreach (string component in new[] { Meltdown, "Level-Based Stat Bonuses", "FF7EnemyIntegration" })
        {
            Assert.Equal(1, Find(runtime, component).Value);
            Assert.NotNull(runtime.GetBehaviorSettingLockReason(Group, "component", component));
            Assert.False(runtime.SetBehaviorSetting(Group, "component", component, 0).Success);
            Assert.Contains(runtime.Catalog.Packages, package => package.Id == "Memoria/" + component && package.Status == ModEffectiveStatus.Active);
        }
        Assert.Equal(50, Find(runtime, Vitality).Value);
        Assert.Equal(25, Find(runtime, Spirit).Value);
        Assert.False(runtime.SetBehaviorSetting(Group, "status", Vitality, 70).Success);
        Assert.True(runtime.SetBehaviorSetting(Group, "component", "Girl Next Door", 1).Success);
        runtime.SetHardcoreMode(false);
        runtime.SetFf7BridgeEnemyIntegration(false);
        Publish(runtime);
        Assert.Equal(0, Find(runtime, Meltdown).Value);
        Assert.Equal(0, Find(runtime, "Level-Based Stat Bonuses").Value);
        Assert.Equal(0, Find(runtime, "FF7EnemyIntegration").Value);
        Assert.Equal(1, Find(runtime, "Girl Next Door").Value);
        Assert.Equal(73, Find(runtime, Vitality).Value);
        Assert.Equal(37, Find(runtime, Spirit).Value);
    }

    private void WriteBundledMeltdown()
    {
        Write("BundledMods/Memoria/Meltdown/mod.json", """{"id":"memoria-meltdown","name":"Meltdown","package":"Memoria","description":{"en":"Status modifiers"}}""");
        Write("BundledMods/Memoria/Meltdown/statuses/vit0.json", """{"id":"Vit0","statModifiers":{"Vitality":{"operation":"ReducePercent","value":{"default":50}},"Spirit":{"operation":"ReducePercent","value":{"default":25}}}}""");
    }

    private static BehaviorModSetting Find(ModRuntime runtime, string parameter)
    {
        return Assert.Single(runtime.ReadBehaviorSettings(Group), setting => setting.Parameter == parameter);
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true), string.Join(Environment.NewLine, runtime.Diagnostics));
        Assert.True(runtime.CommitPending());
    }

    private void Write(string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
