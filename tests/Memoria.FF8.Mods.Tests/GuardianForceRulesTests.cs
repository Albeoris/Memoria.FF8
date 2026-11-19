namespace Memoria.FF8.Mods.Tests;

public sealed class GuardianForceRulesTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "gf-rules-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void NativePackagesCombineDisjointRulesAndUseEnglishFallback()
    {
        WritePackage("ceiling", """{"schemaVersion":1,"defaultSummonDamageLimit":9999,"abilities":[{"id":72,"summonDamageLimit":"unlimited"}]}""");
        WritePackage("training", """{"schemaVersion":1,"abilities":[{"id":72,"apCost":200,"description":{"en":"Raises summon damage.","ru":"Повышает урон призыва."}},{"id":115,"apCost":255,"summonDamageLimit":20000}]}""");
        using ModRuntime runtime = new(root, prepare: _ => new(true, []), watch: false);
        Publish(runtime);

        Assert.Equal(2, runtime.Catalog.Packages.Count);
        Assert.All(runtime.Catalog.Packages, package => Assert.Equal(ModEffectiveStatus.Active, package.Status));
        GuardianForceRules rules = Selected(runtime.Catalog);
        Assert.Equal(9999, rules.DefaultSummonDamageLimit);
        Assert.Equal(200, rules.AbilityPointCosts[72]);
        Assert.Equal(255, rules.AbilityPointCosts[115]);
        Assert.Equal(int.MaxValue, rules.SummonDamageLimits[72]);
        Assert.Equal(20000, rules.SummonDamageLimits[115]);
        Assert.Equal("Повышает урон призыва.", rules.Description(72, "RU"));
        Assert.Equal("Raises summon damage.", rules.Description(72, "es"));
        Assert.Null(rules.Description(115, "en"));

        Assert.True(runtime.SetEnabled("Memoria/ceiling", false).Success);
        Publish(runtime);
        rules = Selected(runtime.Catalog);
        Assert.Null(rules.DefaultSummonDamageLimit);
        Assert.False(rules.SummonDamageLimits.ContainsKey(72));
        Assert.Equal(200, rules.AbilityPointCosts[72]);
    }

    [Fact]
    public void ConflictingPackageKeepsTheAcceptedGenerationUntilResolved()
    {
        WritePackage("first", """{"schemaVersion":1,"abilities":[{"id":72,"apCost":200}]}""");
        using ModRuntime runtime = new(root, watch: false);
        Publish(runtime);
        ModCatalogSnapshot committed = runtime.Catalog;

        WritePackage("second", """{"schemaVersion":1,"abilities":[{"id":72,"apCost":250}]}""");
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.False(runtime.CommitPending());
        Assert.Same(committed, runtime.Catalog);
        Assert.Equal(200, Selected(runtime.Catalog).AbilityPointCosts[72]);
        ModDiagnostic conflict = Assert.Single(runtime.Diagnostics, diagnostic => diagnostic.Code == "Memoria.ConflictingGuardianForceRules");
        Assert.Equal("Memoria/second", conflict.PackageId);
        Assert.Equal(GuardianForceRules.PayloadPath, conflict.RelativePath);
        Assert.Contains("Memoria/first", conflict.Message);
        Assert.Contains("ability 72 property 'apCost'", conflict.Message);

        WritePackage("second", """{"schemaVersion":1,"defaultSummonDamageLimit":"unlimited"}""");
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.Equal(200, Selected(runtime.Catalog).AbilityPointCosts[72]);
        Assert.Equal(int.MaxValue, Selected(runtime.Catalog).DefaultSummonDamageLimit);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[]}""")]
    [InlineData("""{"schemaVersion":2,"defaultSummonDamageLimit":9999}""")]
    [InlineData("""{"schemaVersion":1,"schemaVersion":1,"defaultSummonDamageLimit":9999}""")]
    [InlineData("""{"schemaVersion":1,"defaultSummonDamageLimit":"Uncapped"}""")]
    [InlineData("""{"schemaVersion":1,"defaultSummonDamageLimit":2147483648}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[{"id":0,"apCost":200}]}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[{"id":116,"apCost":200}]}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[{"id":72,"apCost":0}]}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[{"id":72,"apCost":256}]}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[{"id":72,"apCost":200},{"id":72,"summonDamageLimit":"unlimited"}]}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[{"id":72}]}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[{"id":72,"summonDamageLimit":0}]}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[{"id":72,"apCost":200,"unsupported":true}]}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[{"id":72,"description":{"ru":"Текст"}}]}""")]
    [InlineData("""{"schemaVersion":1,"abilities":[{"id":72,"description":{"en":"A","en":"B"}}]}""")]
    public void InvalidDocumentsRejectTheWholeCandidate(string content)
    {
        WritePackage("invalid", content);
        using ModRuntime runtime = new(root, watch: false);
        Assert.False(runtime.PreparePending(force: true));
        Assert.False(runtime.CommitPending());
        Assert.Equal(0, runtime.Catalog.Generation);
        ModDiagnostic error = Assert.Single(runtime.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidGuardianForceRules");
        Assert.Equal("Memoria/invalid", error.PackageId);
        Assert.Equal(GuardianForceRules.PayloadPath, error.RelativePath);
    }

    private static GuardianForceRules Selected(ModCatalogSnapshot catalog)
    {
        Assert.True(GuardianForceRules.TryPrepare(catalog, out GuardianForceRules rules, out IReadOnlyList<ModDiagnostic> diagnostics), string.Join(Environment.NewLine, diagnostics));
        return rules;
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true), string.Join(Environment.NewLine, runtime.Diagnostics));
        Assert.True(runtime.CommitPending());
    }

    private void WritePackage(string id, string content)
    {
        string package = Path.Combine(root, "Mods", "Memoria", id);
        string payload = Path.Combine(package, GuardianForceRules.PayloadPath);
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        File.WriteAllText(Path.Combine(package, NativeModDescriptor.FileName), $$$"""{"id":"{{{id}}}","name":"Example","package":"Memoria","description":{"en":"GF rule examples."}}""");
        File.WriteAllText(payload, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
