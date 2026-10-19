using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class FieldSceneModPublicationTests
{
    [Fact]
    public void InvalidInstalledAddressesKeepPreviousSceneTransactionAndDisableRestoresOriginals()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Optional installed field resources are required.");
        string root = Path.Combine(Path.GetTempPath(), "memoria-field-mod-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(root, "Memoria", "example", "fields");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "bghall_6.json");
        const string valid = """
            {"schemaVersion":1,"fieldId":175,"fieldName":"bghall_6","backgrounds":[{"layer":0,"moduleId":5,"state":0,"visible":false}],"actors":[{"objectId":8,"model":{"fieldId":175,"modelId":9},"animationId":1,"when":{"moduleId":5,"state":0}}]}
            """;
        try
        {
            File.WriteAllText(path, valid);
            using GameResourceHost host = new(game, watch: false, modsDirectory: root);
            Assert.Contains(host.Mods.Catalog.Packages, package => package.Id == "Memoria/example" && package.Status == ModEffectiveStatus.Active);
            FieldSceneMods original = host.SceneMods;
            long generation = host.Mods.Catalog.Generation;
            Assert.Single(original.Patches, patch => patch.PackageId == "Memoria/example");
            foreach (string invalid in new[]
            {
                valid.Replace("\"modelId\":9", "\"modelId\":60000", StringComparison.Ordinal),
                valid.Replace("\"objectId\":8", "\"objectId\":60000", StringComparison.Ordinal),
                valid.Replace("\"moduleId\":5", "\"moduleId\":254", StringComparison.Ordinal),
                valid.Replace("\"fieldId\":175", "\"fieldId\":174", StringComparison.Ordinal),
                valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":999", StringComparison.Ordinal)
            })
            {
                File.WriteAllText(path, invalid);
                host.Mods.RequestRefresh();
                Assert.False(host.Pump(force: true));
                Assert.Same(original, host.SceneMods);
                Assert.Equal(generation, host.Mods.Catalog.Generation);
                Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.PackageId == "Memoria/example");
            }
            File.WriteAllText(path, valid);
            Assert.True(host.Mods.SetEnabled("Memoria/example", false).Success);
            Assert.True(host.Pump(force: true));
            Assert.DoesNotContain(host.SceneMods.Patches, patch => patch.PackageId == "Memoria/example");
            Assert.True(host.Mods.SetEnabled("Memoria/example", true).Success);
            Assert.True(host.Pump(force: true));
            Assert.Contains(host.SceneMods.Patches, patch => patch.PackageId == "Memoria/example");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
