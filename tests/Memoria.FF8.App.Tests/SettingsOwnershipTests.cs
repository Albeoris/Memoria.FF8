using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class SettingsOwnershipTests
{
    [Fact]
    public void Every_registry_entry_has_an_explicit_disposition_and_supported_owner()
    {
        Assert.Equal(37, MenuSettings.Definitions.Count);
        Assert.Equal(MenuSettings.Definitions.Select(setting => setting.Id).Order(), SettingsPlacement.All.Select(placement => placement.LegacyKey).Order());
        Assert.Equal(37, SettingsPlacement.All.Select(placement => placement.Identity).Distinct().Count());
        Assert.Equal(new[] { "Controller", "Sound" }, SettingsPlacement.All.Where(placement => placement.Audience == SettingsAudience.Excluded).Select(placement => placement.LegacyKey).Order());
        Assert.Equal(new[] { "ATB", "Battle control", "Battle message", "Battle speed", "Camera movement", "Scan" }, SettingsPlacement.All.Where(placement => placement.Audience == SettingsAudience.Fixture).Select(placement => placement.LegacyKey).Order());
        Assert.Equal("Battle policy", Assert.Single(SettingsPlacement.All, placement => placement.Audience == SettingsAudience.Developer).LegacyKey);
        Assert.All(SettingsPlacement.All.Where(placement => placement.Audience == SettingsAudience.Player), placement =>
        {
            Assert.StartsWith("config-", placement.Page);
            Assert.NotEmpty(placement.Target);
            Assert.NotEmpty(placement.Scope);
        });
        Assert.Equal("config-controller-settings", SettingsPlacement.All.Single(placement => placement.LegacyKey == "Vibration").Page);
    }

    [Fact]
    public void Live_editor_targets_are_unique_and_disjoint_across_the_two_trees()
    {
        MenuController menus = Connected();
        List<string> player = [];
        foreach (string page in SettingsPlacement.Pages.Keys.Append("config-controller-settings"))
        {
            menus.Open(page);
            foreach (MenuEntry entry in menus.Entries)
            {
                if (SettingsPlacement.All.FirstOrDefault(placement => placement.LegacyKey == entry.Id) is { } placement)
                {
                    Assert.Equal(SettingsAudience.Player, placement.Audience);
                    Assert.Equal(page, placement.Page);
                    player.Add(placement.Target + "/" + placement.Scope);
                }
                else if (entry.Action is { Kind: "controls-setting", SubjectId: "vibration" })
                {
                    SettingPlacement vibration = SettingsPlacement.All.Single(placement => placement.LegacyKey == "Vibration");
                    player.Add(vibration.Target + "/" + vibration.Scope);
                }
            }
        }
        Assert.Equal(player.Count, player.Distinct().Count());
        Assert.Equal(28, player.Count);
        menus.Open("debug-battle", true);
        List<string> developer = menus.Entries.Where(entry => SettingsPlacement.All.Any(placement => placement.LegacyKey == entry.Id)).Select(entry => SettingsPlacement.All.Single(placement => placement.LegacyKey == entry.Id)).Select(placement => placement.Target + "/" + placement.Scope).ToList();
        Assert.Single(developer);
        Assert.Empty(player.Intersect(developer));
        Assert.DoesNotContain(menus.Entries, entry => entry.Action?.SubjectId is "Difficulty" or "Random encounters" or "Battle ATB mode" or "Gunblade trigger");
        Assert.Single(player, target => target.StartsWith("ControlsProfile.Controller.VibrationEnabled/", StringComparison.Ordinal));
    }

    [Fact]
    public void Both_devices_cover_exactly_the_supported_player_and_developer_actions()
    {
        MenuController menus = Connected();
        foreach (ControlDevice device in Enum.GetValues<ControlDevice>())
        {
            string page = device == ControlDevice.Keyboard ? "config-keyboard" : "config-controller";
            menus.Open(page);
            HashSet<string> player = [];
            for (int group = 0; group < 4; group++)
            {
                player.UnionWith(menus.Entries.Where(entry => entry.Action?.Kind == "controls-select").Select(entry => entry.Action!.SubjectId));
                menus.Execute(new("controls-group"));
            }
            menus.Open("debug-shortcuts", true);
            string[] developer = menus.Entries.Where(entry => entry.Action?.Kind == "controls-select").Select(entry => entry.Action!.SubjectId).ToArray();
            Assert.Equal(23, player.Count);
            Assert.Equal(3, developer.Length);
            Assert.Empty(player.Intersect(developer));
            Assert.Equal(ControlsProfile.Definitions.Select(definition => definition.Action.ToString()).Order(), player.Concat(developer).Order());
            Assert.DoesNotContain(nameof(ControlAction.SkipMovie), player.Concat(developer));
            Assert.DoesNotContain(nameof(ControlAction.GuardianFormSkip), player.Concat(developer));
        }
    }

    [Fact]
    public void Nested_gallery_navigation_and_dispatch_do_not_write_live_owners()
    {
        int preferenceWrites = 0;
        int controlWrites = 0;
        int liveActions = 0;
        UiPreferences preferences = new();
        ControlsProfile controls = ControlsProfile.CreateDefault();
        MenuController menus = new(() => new MenuData(), _ => liveActions++);
        menus.ConnectPreferences(() => preferences, _ => preferenceWrites++);
        menus.ConnectControls(() => controls, _ => controlWrites++);
        CountingMods mods = new();
        menus.ConnectMods(mods);
        menus.Open("debug-gallery", true);
        menus.Push("main");
        Assert.True(menus.IsFixtureContext);
        menus.Push("config");
        menus.Push("config-text");
        menus.Execute(new("config", "UI font", Value: "Arial"));
        menus.Execute(new("host-config", "Text scale", Value: "150%"));
        menus.Push("config-keyboard");
        menus.Execute(new("controls-select", nameof(ControlAction.QuickSave), Value: nameof(ControlDevice.Keyboard)));
        menus.Execute(new("controls-clear"));
        menus.Execute(new("controls-setting", "vibration"));
        menus.Push("mods");
        menus.Execute(new("mod-auto"));
        menus.Execute(new("mod-refresh"));
        menus.Execute(new("mod-toggle", "fixture-package"));
        menus.Execute(new("battle-debug-outcome", Value: "Victory"));
        menus.Execute(new("new-game-start"));
        menus.Execute(new("demo-reset"));
        Assert.Equal(0, preferenceWrites);
        Assert.Equal(0, controlWrites);
        Assert.Equal(0, liveActions);
        Assert.Equal(0, mods.Mutations);
        Assert.Equal(Keys.F5, Assert.Single(controls.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard)).Keys.Single());
        Assert.True(controls.VibrationEnabled);
    }

    private static MenuController Connected()
    {
        MenuData data = new();
        foreach (MenuSetting setting in MenuSettings.Definitions)
        {
            data.Settings[setting.Id] = setting.Values[0];
            data.SupportedActions.Add("config:" + setting.Id);
        }
        MenuController menus = new(() => data, _ => { });
        menus.ConnectPreferences(() => new UiPreferences(), _ => { });
        menus.ConnectControls(ControlsProfile.CreateDefault, _ => { });
        return menus;
    }

    private sealed class CountingMods : IModService
    {
        public int Mutations { get; private set; }
        public ModCatalogSnapshot Catalog { get; } = new(0, [], []);
        public bool AutoEnableNewMods => true;
        public bool IsReloadPending => false;
        public IReadOnlyList<ModDiagnostic> Diagnostics => [];
        public ModOperationResult SetEnabled(string packageId, bool enabled)
        {
            Mutations++;
            return ModOperationResult.Completed;
        }
        public ModOperationResult SetAutoEnableNewMods(bool enabled)
        {
            Mutations++;
            return ModOperationResult.Completed;
        }
        public void Refresh()
        {
            Mutations++;
        }
        public IDisposable Subscribe(Action<ModCatalogSnapshot> changed) => new Subscription();
        private sealed class Subscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
