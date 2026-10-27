using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionGameplayBehaviorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-gameplay-import-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("BE5EB0792F0FB229FC231D8DC38978EBE2FC79CD2D4253AB905D81EEFB7D4221", JunctionGameplaySetting.MaximumDamage, 65535)]
    [InlineData("4E9556DB57270D0F0D3CFBAD21A80FCAFD258CF8DF326C0600547030C3DF4DC9", JunctionGameplaySetting.DrawBonus, 100)]
    [InlineData("7D4722302FE67F9492AA402004DD99BCA28EE4C04D804627BCC34306E88202F7", JunctionGameplaySetting.DrawBonus, 100)]
    [InlineData("F758412928297C413B32B23F4E4A4A466142261577569AC2DF6C07D687E08D2D", JunctionGameplaySetting.DrawBonus, 100)]
    [InlineData("1E15E923336B093412C9E4B96EABD9CDEA11624A32137CDE87690DCF6417FAB6", JunctionGameplaySetting.DrawBonus, 100)]
    [InlineData("1388735540B8FACF638FDB02298A62B34CD5622C8C707C94CD435A3202B7A4CE", JunctionGameplaySetting.DrawBonus, 100)]
    [InlineData("A59DAB355E117451CDDE231D980C32871637CFEC3CD04CB62EE7D31D52E21EDC", JunctionGameplaySetting.UnlimitedMagic, 1)]
    [InlineData("04114651C4527D7C86D56C33E42AC2F247D34C24412DC1EA4254A8C4D3A62CFB", JunctionGameplaySetting.ApMultiplier, 2)]
    [InlineData("D6F1A2AC603E3D6C0212E7404A571A66D759E7A85D585D850EC63CEC1AC36ABE", JunctionGameplaySetting.ApMultiplier, 4)]
    [InlineData("37DF230EF5EAEB29876AA3DD910EDA621BBDD7138EEF95B5B9FEEF0EC543F6AD", JunctionGameplaySetting.ApMultiplier, 8)]
    [InlineData("F70590AC0315799D0B56DA438EC2A689339861877BDB10521C8F2BFC4FDA81A4", JunctionGameplaySetting.ExperienceMultiplier, 0)]
    [InlineData("288F33FA4EEB02356B582A8ADE98EAF243C91EAB7BF88B0D2A58B70AE3D6B302", JunctionGameplaySetting.ExperienceMultiplier, 0)]
    [InlineData("893B6A35AE196E41249FE299B3C70028A05D64C6A91640276929F72F20EF7FD0", JunctionGameplaySetting.ExperienceMultiplier, 2)]
    [InlineData("A738D02E02359FA3181EF41E9C0074069D2A719FB50EE29B89B572B531AC5C6A", JunctionGameplaySetting.ExperienceMultiplier, 4)]
    [InlineData("A9545DDA038E6E8DEFE85A34024639A15617AC7143876B5A3611ABC420099854", JunctionGameplaySetting.ExperienceMultiplier, 8)]
    [InlineData("3180E5B37EBD6C42B1C5796BC6C01A1CD63E3A259C1B23D23D27FCA724603758", JunctionGameplaySetting.ExperienceMultiplier, 16)]
    [InlineData("5FEBC6B68104AF43C71CFEA1E4FFDECB32BE386D098DE16CF2D4287901F8A542", JunctionGameplaySetting.UnrestrictedScanRotation, 1)]
    public void Approved_payloads_expose_numeric_rules_only_when_selected(string hash, JunctionGameplaySetting setting, int value)
    {
        byte[] bytes = Payload(hash);
        WritePackage([bytes]);
        JunctionModPackage enabled = JunctionModImporter.Open(root);
        Assert.Equal(new JunctionGameplayOverride(setting, value), Assert.Single(enabled.Manifest.GameplayOverrides));
        Assert.Empty(enabled.Files);
        Assert.Empty(enabled.Manifest.Behaviors);
        Assert.Empty(JunctionModImporter.Open(root, new Dictionary<string, int> { ["option0"] = 0 }).Manifest.GameplayOverrides);
        bytes[0] ^= 1;
        File.WriteAllBytes(Path.Combine(root, "Selection0/hext/ff8/en_nv/neutral.hext"), bytes);
        Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(root, new Dictionary<string, int> { ["option0"] = 0 }));
    }

    [Theory]
    [InlineData("4AE6F4BFF59DDEC9D863C3F3C80E0C713083CC500B31517815BC8A8395B3EE1C")]
    [InlineData("7B392B63153209CF4EB43866409241A7F8DA29577E6F597547D63978BCCAC196")]
    [InlineData("B8A19FDB7A27FE8D7830EEE9194302AEE123C905823524D99F8F7DE61FE67801")]
    [InlineData("69B542DB62A24F792DE1FB233FFB8CBACDBE82F561EDA91C165A4458AA167375")]
    [InlineData("CF5D09B4ED568097FE4BC53B8D38B2ABD979A147FB85FE87BAEED24B81402494")]
    [InlineData("6CF13A6DA31581CDD05BB6477F50B3308AD497F70DC5339970DDB4D3CAF624A5")]
    [InlineData("23929F707190078B02431D92D4F5A82B323F88940A926F00EDD1D501F5CEFF4B")]
    [InlineData("6D565374CC49E03145313B0FF91DAA760D6970605FD79595C072BC0BD9F5CEE4")]
    [InlineData("5C73BAE0D1F5E7479B354F86AFE0FAF993CEC9C16107E9F773CDD605287052DD")]
    public void Denied_reviewed_payloads_are_allowed_unselected_and_fail_with_reason_when_selected(string hash)
    {
        WritePackage([Payload(hash)]);
        JunctionModPackage inactive = JunctionModImporter.Open(root, new Dictionary<string, int> { ["option0"] = 0 });
        Assert.Empty(inactive.Files);
        Assert.Empty(inactive.Manifest.GameplayOverrides);
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => JunctionModImporter.Open(root));
        Assert.Contains("Reviewed Junction behavior is unavailable", error.Message);
    }

    [Theory]
    [InlineData("25095ECB387DD4B987696F0D916A35D67FF5C7B26FF640E669919F8DCC718D03", JunctionBehavior.SaveAnywhere)]
    [InlineData("86169F41F49645F3C3C9CA0C0CEFE17493D1A52C8B785403F171FFC3B7759801", JunctionBehavior.DreamJunctionCarry)]
    public void Intrinsic_behavior_removes_its_exclusive_option_and_cannot_be_disabled(string hash, JunctionBehavior behavior)
    {
        WritePackage([Payload(hash)]);
        JunctionModPackage package = JunctionModImporter.Open(root, new Dictionary<string, int> { ["option0"] = 0 });
        Assert.Equal(behavior, Assert.Single(package.Manifest.Behaviors));
        Assert.Empty(package.Manifest.Options);
        Assert.Empty(package.Files);
        Assert.Empty(package.Manifest.GameplayOverrides);
    }

    [Fact]
    public void Rare_item_rule_is_exact_and_conflicting_numeric_selections_fail()
    {
        WritePackage([Payload("4D2B44E846637A02542A70DFF04F66CBB810E566515C3DE6D8B4B8E23C2ABD26")]);
        Assert.Equal(new decimal[] { 46.875m, 31.25m, 11.71875m, 10.15625m }, JunctionModImporter.Open(root).Manifest.GameplayOverrides.Select(rule => rule.Value));
        Directory.Delete(root, recursive: true);
        WritePackage([Payload("04114651C4527D7C86D56C33E42AC2F247D34C24412DC1EA4254A8C4D3A62CFB"), Payload("D6F1A2AC603E3D6C0212E7404A571A66D759E7A85D585D850EC63CEC1AC36ABE")]);
        Assert.Throws<InvalidDataException>(() => JunctionModImporter.Open(root));
    }

    [Fact]
    public void Intrinsic_folder_with_cosmetic_replacement_keeps_its_resource_option()
    {
        WritePackage([Payload("25095ECB387DD4B987696F0D916A35D67FF5C7B26FF640E669919F8DCC718D03")]);
        string texture = Path.Combine(root, "Selection0/textures/image.png");
        Directory.CreateDirectory(Path.GetDirectoryName(texture)!);
        File.WriteAllBytes(texture, [1, 2, 3]);
        JunctionModPackage enabled = JunctionModImporter.Open(root);
        Assert.Single(enabled.Files);
        Assert.Equal(1, enabled.Manifest.Options["option0"]);
        JunctionModPackage inactive = JunctionModImporter.Open(root, new Dictionary<string, int> { ["option0"] = 0 });
        Assert.Empty(inactive.Files);
        Assert.Equal(0, inactive.Manifest.Options["option0"]);
        Assert.Contains(JunctionBehavior.SaveAnywhere, inactive.Manifest.Behaviors);
    }

    [Fact]
    public void Original_manifest_defaults_import_cosmetics_and_rules_as_an_active_package()
    {
        string source = ArchivePath();
        JunctionModPackage imported = JunctionModImporter.Open(source);
        Assert.NotEmpty(imported.Files);
        Assert.Contains(imported.Manifest.GameplayOverrides, rule => rule.Setting == JunctionGameplaySetting.UnrestrictedScanRotation && rule.Value == 1);
        Assert.Equal(4, imported.Manifest.GameplayOverrides.Count(rule => rule.Setting is JunctionGameplaySetting.RareItemFirst or JunctionGameplaySetting.RareItemSecond or JunctionGameplaySetting.RareItemThird or JunctionGameplaySetting.RareItemFourth));
        Assert.Contains(JunctionBehavior.SaveAnywhere, imported.Manifest.Behaviors);
        Assert.Contains(JunctionBehavior.DreamJunctionCarry, imported.Manifest.Behaviors);
        string destination = Path.Combine(root, "Mods/Junction/Gameplay.iroj");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.Catalog.Packages).Status);
        Assert.NotEmpty(runtime.Catalog.Files);
        Assert.DoesNotContain(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-import-failed");
    }

    private static string ArchivePath()
    {
        string? catalogPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(catalogPath) && File.Exists(catalogPath), "Configure an optional external fixture catalog.");
        Dictionary<string, string> catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalogPath))!;
        Assert.SkipUnless(catalog.TryGetValue("junction-gameplay", out string? path), "External fixture catalog has no gameplay behavior archive.");
        string basePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURES") ?? Path.GetDirectoryName(Path.GetFullPath(catalogPath))!;
        string archivePath = Path.GetFullPath(path!, basePath);
        Assert.SkipUnless(File.Exists(archivePath), "Optional gameplay behavior archive is unavailable.");
        return archivePath;
    }

    private static byte[] Payload(string hash)
    {
        string archivePath = ArchivePath();
        foreach (JunctionArchiveEntry entry in new JunctionArchive(archivePath).Entries.Where(entry => entry.RelativePath.EndsWith(".hext", StringComparison.OrdinalIgnoreCase)))
        {
            using Stream input = entry.OpenRead();
            using MemoryStream bytes = new();
            input.CopyTo(bytes);
            byte[] payload = bytes.ToArray();
            if (Convert.ToHexString(SHA256.HashData(payload)) == hash)
            {
                return payload;
            }
        }
        throw new InvalidDataException("Optional archive does not contain the requested reviewed payload.");
    }

    private void WritePackage(byte[][] payloads)
    {
        Directory.CreateDirectory(root);
        XElement manifest = new("ModInfo", new XElement("Name", "Gameplay behavior package"));
        for (int index = 0; index < payloads.Length; index++)
        {
            manifest.Add(new XElement("ConfigOption", new XElement("Type", "Bool"), new XElement("ID", "option" + index), new XElement("Default", 1)));
            manifest.Add(new XElement("ModFolder", new XAttribute("Folder", "Selection" + index), new XAttribute("ActiveWhen", $"option{index} = 1")));
            string folder = Path.Combine(root, "Selection" + index, "hext/ff8/en_nv");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "neutral.hext"), payloads[index]);
        }
        File.WriteAllText(Path.Combine(root, "mod.xml"), manifest.ToString(SaveOptions.DisableFormatting));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
