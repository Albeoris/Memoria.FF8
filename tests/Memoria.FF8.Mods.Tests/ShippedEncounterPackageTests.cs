using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class ShippedEncounterPackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-encounter-package-" + Guid.NewGuid().ToString("N"));
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "EncounterExample", name);

    [Fact]
    public void ActualShippedPayloadParsesComposesAndKeepsApprovedArrivalEndpoints()
    {
        EncounterPatchDocument document = EncounterPatchDocument.Parse(File.ReadAllText(Fixture("payload.json")));
        EncounterPatchComposition result = Compose(document);
        Assert.Equal("memoria.edea-mirrors.disc1", document.DocumentId);
        Assert.Equal("Classic", document.SourceProfile);
        Assert.Equal(3, document.RequiredActorCapacity);
        Assert.Equal(2, result.ActorTemplates.Count);
        Assert.Equal(3, result.Policies.Count);
        Assert.Equal(12, result.CameraProperties["elevationDegrees"]);
        Assert.Single(result.DamageRoutes);
        EncounterPatchOperation arrival = Assert.Single(result.Presentations).Value;
        Assert.Equal(8, arrival.StartFrame);
        Assert.Equal(28, arrival.EndFrame);
        Assert.Equal("Confuse", result.Policies[document.DocumentId + "/assault-policy"].Rules.Single(rule => rule.Id == "confuse").Action.Spell);
    }

    [Fact]
    public void IndependentHpAndProducerBoundNamedRuleEditComposeWithActualPackage()
    {
        EncounterPatchDocument document = EncounterPatchDocument.Parse(File.ReadAllText(Fixture("payload.json")));
        EncounterPatchDocument refinement = EncounterPatchDocument.Parse("""
            {"schemaVersion":1,"documentId":"example.refinement","encounter":"ff8.disc1.edea","atomic":true,"operations":[
              {"id":"hp","kind":"SetActorProperty","actor":"original","property":"stats.maximumHpMultiplier","expectedValue":1,"value":1.25},
              {"id":"spell","kind":"SetRuleProperty","producer":{"documentId":"memoria.edea-mirrors.disc1","operationId":"define-support-policy"},"policy":"support-policy","rule":"regen","property":"action.spell","expectedValue":"Regen","value":"Haste"}
            ]}
            """);
        EncounterPatchComposition result = EncounterPatchComposer.Compose(new("ff8.disc1.edea", "Classic", ["original"]), [new("refinement", "gameplay/encounters/edea-disc1.json", refinement), new("package", "gameplay/encounters/edea-disc1.json", document)]);
        Assert.Equal(1.25, result.ActorProperties["original/stats.maximumHpMultiplier"]);
        EncounterPatchPolicy support = result.Policies[document.DocumentId + "/support-policy"];
        Assert.Equal("Haste", support.Rules.Single(rule => rule.Id == "regen").Action.Spell);
        Assert.Equal("Shell", support.Rules.Single(rule => rule.Id == "shell").Action.Spell);
        Assert.Equal(2, result.ActorTemplates.Count);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PackageInheritsGlobalPolicyAndBundledUpdatesRetainTheSavedUserChoice(bool enabled, bool autoEnable)
    {
        string application = Path.Combine(root, "Data");
        string bundle = Path.Combine(application, "BundledMods", "Memoria", "EdeaMirrors");
        string payload = Path.Combine(bundle, "gameplay", "encounters", "edea-disc1.json");
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        File.Copy(Fixture("payload.json"), payload);
        File.Copy(Fixture("mod.ini"), Path.Combine(bundle, "mod.ini"));
        string mods = Path.Combine(root, "Mods");
        using (ModRuntime runtime = new(application, watch: false, modsDirectory: mods))
        {
            Assert.True(runtime.SetAutoEnableNewMods(autoEnable).Success);
            Publish(runtime);
            ModPackage package = Assert.Single(runtime.Catalog.Packages);
            Assert.Null(package.ExplicitEnabled);
            Assert.Equal(autoEnable ? ModEffectiveStatus.Active : ModEffectiveStatus.Disabled, package.Status);
            Assert.Equal(autoEnable ? 1 : 0, runtime.Catalog.Files.Count);
            Assert.True(runtime.SetEnabled(package.Id, enabled).Success);
            Publish(runtime);
            Assert.Equal(enabled ? ModEffectiveStatus.Active : ModEffectiveStatus.Disabled, Assert.Single(runtime.Catalog.Packages).Status);
        }
        File.Copy(Fixture("payload.json"), payload, true);
        File.Copy(Fixture("mod.ini"), Path.Combine(bundle, "mod.ini"), true);
        using ModRuntime reopened = new(application, watch: false, modsDirectory: mods);
        Publish(reopened);
        ModPackage retained = Assert.Single(reopened.Catalog.Packages);
        Assert.Equal(enabled, retained.ExplicitEnabled);
        Assert.Equal(enabled ? ModEffectiveStatus.Active : ModEffectiveStatus.Disabled, retained.Status);
        Assert.True(reopened.SetAutoEnableNewMods(!autoEnable).Success);
        Publish(reopened);
        Assert.Equal(enabled, Assert.Single(reopened.Catalog.Packages).ExplicitEnabled);
        Assert.Equal(enabled ? ModEffectiveStatus.Active : ModEffectiveStatus.Disabled, Assert.Single(reopened.Catalog.Packages).Status);
        if (enabled)
        {
            Assert.True(EncounterMods.TryPrepare(reopened.Catalog, out EncounterMods prepared, out IReadOnlyList<ModDiagnostic> diagnostics));
            Assert.Empty(diagnostics);
            Assert.Single(prepared.Layers);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigurationOnlyUserDirectoryInheritsTheGlobalPolicy(bool autoEnable)
    {
        string application = Path.Combine(root, "Data");
        string bundle = Path.Combine(application, "BundledMods", "Memoria", "EdeaMirrors");
        string payload = Path.Combine(bundle, "gameplay", "encounters", "edea-disc1.json");
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        File.Copy(Fixture("payload.json"), payload);
        File.Copy(Fixture("mod.ini"), Path.Combine(bundle, "mod.ini"));
        string mods = Path.Combine(root, "Mods");
        string configuration = Path.Combine(mods, "Memoria", "EdeaMirrors", "mod.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(configuration)!);
        File.WriteAllText(configuration, "title = Local preferences\n");
        using ModRuntime runtime = new(application, watch: false, modsDirectory: mods);
        Assert.True(runtime.SetAutoEnableNewMods(autoEnable).Success);
        Publish(runtime);
        Assert.Equal(autoEnable, runtime.AutoEnableNewMods);
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        Assert.Null(package.ExplicitEnabled);
        Assert.Equal(autoEnable ? ModEffectiveStatus.Active : ModEffectiveStatus.Disabled, package.Status);
        Assert.Equal(autoEnable ? 1 : 0, runtime.Catalog.Files.Count);
        Assert.Equal("title = Local preferences\n", File.ReadAllText(configuration));
    }
    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private static EncounterPatchComposition Compose(EncounterPatchDocument document) => EncounterPatchComposer.Compose(new("ff8.disc1.edea", "Classic", ["original"]), [new("package", "gameplay/encounters/edea-disc1.json", document)]);

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
    }
}
