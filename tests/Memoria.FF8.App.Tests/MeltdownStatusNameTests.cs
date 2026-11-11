using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MeltdownStatusNameTests
{
    private const string MagicKey = "$en_kernel_magicspells_name_0049";
    private static readonly string StatusKey = SystemTextCatalog.Key("battle.status.vit-0");

    [Fact]
    public void HudShowsLocalizedMeltdownForTheNativeStatusBitWithoutChangingBattleState()
    {
        using InMemoryResourceSource source = new(new("test", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        resources.Prepare(null, textAliases: new Dictionary<string, LocalizedTextAlias> { [StatusKey] = new(MagicKey, "Meltdown") }).Commit();
        BattleSession battle = Battle(0x1000000);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        hud.ConfigureLocalization(new(resources));
        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.Contains("Meltdown", Draw(hud));
        resources.PrepareLocalization(new(new Dictionary<string, LocalizationValue> { [MagicKey] = new("\u041c\u0435\u043b\u0442\u0434\u0430\u0443\u043d", "test", "test") })).Commit();
        Assert.Contains("\u041c\u0435\u043b\u0442\u0434\u0430\u0443\u043d", Draw(hud));
        resources.PrepareLocalization(new(new Dictionary<string, LocalizationValue> { [MagicKey] = new("Localized second name", "test", "test") })).Commit();
        Assert.Contains("Localized second name", Draw(hud));
        Assert.Equal(MagicKey, resources.ResolveText(StatusKey, "Vit 0").Binding.Key);
        resources.Prepare(null, textAliases: new Dictionary<string, LocalizedTextAlias>()).Commit();
        Assert.Contains("Vit 0", Draw(hud));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void UnrelatedBitTwentyTwoDoesNotShowTheMeltdownStatusLabel()
    {
        BattleHudController hud = new(Battle(0x400000), (_, _) => { }, () => { }, () => { });
        Assert.DoesNotContain("Vit 0", Draw(hud));
        Assert.DoesNotContain("Meltdown", Draw(hud));
    }

    [Fact]
    public void InstalledHostPublishesNameReferenceAndKeepsPreviousNameWhenReferenceIsInvalid()
    {
        using IsolatedFieldGame game = new();
        string root = Path.Combine(Path.GetTempPath(), "ff8-status-name-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string payload = Path.Combine(mods, "Memoria", "StatusName", "gameplay", "behaviors.json");
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        File.WriteAllText(payload, "{\"schemaVersion\":2,\"meltdown\":{\"StatusName\":\"" + MagicKey + "\"}}");
        try
        {
            using GameResourceHost host = new(game.GameDirectory, Path.Combine(root, "Application"), watch: false, modsDirectory: mods);
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages).Status);
            Assert.Equal("Meltdown", host.Resources.Localize(StatusKey, "Vit 0"));
            File.WriteAllText(payload, "{\"schemaVersion\":2,\"meltdown\":{\"StatusName\":\"$en_kernel_magicspells_name_9999\"}}");
            host.Mods.RequestRefresh();
            Assert.False(host.Pump(true));
            Assert.Equal("Meltdown", host.Resources.Localize(StatusKey, "Vit 0"));
            Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidStatusNameReference" && diagnostic.PackageId == "Memoria/StatusName");
            Assert.True(host.Mods.SetEnabled("Memoria/StatusName", false).Success);
            Assert.True(host.Pump(true));
            Assert.Equal("Vit 0", host.Resources.Localize(StatusKey, "Vit 0"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static BattleSession Battle(uint status)
    {
        return new(new BattleDefinition
        {
            Combatants = [new() { Id = "party", Name = "Player", IsPlayer = true, NativeStatus1 = status }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
    }

    private static IReadOnlyList<string> Draw(BattleHudController hud)
    {
        BattleUiTests.TestCanvas canvas = new();
        hud.BuildView().Draw(canvas, new UiTheme());
        return canvas.Texts;
    }
}
