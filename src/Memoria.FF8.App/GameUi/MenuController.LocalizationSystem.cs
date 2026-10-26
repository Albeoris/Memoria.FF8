using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<string, string, string>? localizeInterface;

    internal void ConnectLocalizationText(Func<string, string, string> localize)
    {
        localizeInterface = localize;
        _view = null;
    }

    internal string SystemText(string id, string original) => localizeInterface?.Invoke(SystemTextCatalog.Key(id), original) ?? original;

    internal string FormatSystemText(string id, string original, params object?[] arguments)
    {
        return SystemTextCatalog.Format(SystemText(id, original), original, arguments);
    }

    private MenuScreenDefinition LocalizeSystemScreen(MenuScreenDefinition definition)
    {
        if (localizeInterface is null)
        {
            return definition;
        }
        return new MenuScreenDefinition
        {
            Id = definition.Id,
            Title = definition.TitleText?.Invoke(this) ?? SystemText("screen." + definition.Id + ".title", definition.Title),
            Help = definition.HelpText?.Invoke(this) ?? SystemText("screen." + definition.Id + ".help", definition.Help),
            TitleText = definition.TitleText,
            HelpText = definition.HelpText,
            Layout = definition.Layout,
            Columns = definition.Columns,
            MainCommand = definition.MainCommand,
            GalleryEligible = definition.GalleryEligible,
            Entries = definition.Entries,
            Details = definition.Details,
            BuildContent = definition.BuildContent
        };
    }

    internal string MagicText(string id) => SystemText("magic." + id, MagicUiText.Get(id));

    internal string FormatMagicText(string id, params object?[] arguments) => FormatSystemText("magic." + id, MagicUiText.Get(id), arguments);

    internal string StatText(string stat) => stat switch
    {
        "HP" => SystemText("stat.hp", "HP"),
        "Str" => SystemText("stat.strength", "Str"),
        "Vit" => SystemText("stat.vitality", "Vit"),
        "Mag" => SystemText("stat.magic", "Mag"),
        "Spr" => SystemText("stat.spirit", "Spr"),
        "Spd" => SystemText("stat.speed", "Spd"),
        "Eva" => SystemText("stat.evasion", "Eva"),
        "Hit" => SystemText("stat.hit", "Hit"),
        "Luck" => SystemText("stat.luck", "Luck"),
        "Elem-Atk" => SystemText("junction.slot.element-attack", "Elem-Atk"),
        "Elem-Def 1" => FormatSystemText("junction.slot.element-defense", "Elem-Def {0}", 1),
        "Elem-Def 2" => FormatSystemText("junction.slot.element-defense", "Elem-Def {0}", 2),
        "Elem-Def 3" => FormatSystemText("junction.slot.element-defense", "Elem-Def {0}", 3),
        "Elem-Def 4" => FormatSystemText("junction.slot.element-defense", "Elem-Def {0}", 4),
        "ST-Atk" => SystemText("junction.slot.status-attack", "ST-Atk"),
        "ST-Def 1" => FormatSystemText("junction.slot.status-defense", "ST-Def {0}", 1),
        "ST-Def 2" => FormatSystemText("junction.slot.status-defense", "ST-Def {0}", 2),
        "ST-Def 3" => FormatSystemText("junction.slot.status-defense", "ST-Def {0}", 3),
        "ST-Def 4" => FormatSystemText("junction.slot.status-defense", "ST-Def {0}", 4),
        _ => stat
    };

    private string JunctionTabText(int tab) => tab switch
    {
        0 => SystemText("junction.tab.junction", "Junction"),
        1 => SystemText("junction.tab.off", "Off"),
        2 => SystemText("junction.tab.auto", "Auto"),
        3 => SystemText("junction.tab.ability", "Ability"),
        _ => throw new ArgumentOutOfRangeException(nameof(tab))
    };

    private string LocalizeSettingHelp(string setting, string original)
    {
        string? identity = SettingsPlacement.All.FirstOrDefault(item => item.LegacyKey == setting)?.Identity;
        return identity is null ? original : SystemText("setting." + identity + ".help", original);
    }

    private string LocalizeSettingValue(MenuSetting setting, int index) => LocalizeSettingValue(setting.Id, setting.Values[index]);

    private string LocalizeSettingValue(string setting, string original)
    {
        string? identity = SettingsPlacement.All.FirstOrDefault(item => item.LegacyKey == setting)?.Identity;
        MenuSetting? definition = MenuSettings.Definitions.FirstOrDefault(item => item.Id == setting);
        int index = definition is null ? -1 : Array.IndexOf(definition.Values, original);
        return identity is null || index < 0 ? original : SystemText("setting." + identity + ".option." + index.ToString(System.Globalization.CultureInfo.InvariantCulture), original);
    }

    private string LocalizeSettingLabel(string setting, string original)
    {
        string? identity = SettingsPlacement.All.FirstOrDefault(item => item.LegacyKey == setting)?.Identity;
        return identity is not null && SystemTextCatalog.Defaults.ContainsKey("setting." + identity) ? SystemText("setting." + identity, original) : original;
    }

    private IReadOnlyList<MenuEntry> LocalizeSystemEntries(IReadOnlyList<MenuEntry> entries)
    {
        if (localizeInterface is null)
        {
            return entries;
        }
        return entries.Select(entry =>
        {
            string? id = SystemEntryIdentity(ScreenId, entry.Id);
            return id is null ? entry : entry with { Label = SystemText(id, entry.Label) };
        }).ToArray();
    }

    private static string? SystemEntryIdentity(string screen, string entry)
    {
        if (screen == "main")
        {
            return entry switch
            {
                "junction" => "main.junction",
                "item" => "main.item",
                "magic" => "main.magic",
                "gf" => "main.guardian",
                "ability" => "main.ability",
                "status" => "main.status",
                "switch" => "main.party",
                "card" => "main.card",
                "config" => "main.config",
                "tutorial" => "main.tutorial",
                "save" => "main.save",
                "mods" => "main.mods", _ => null
            };
        }
        if (screen == "config" || screen.StartsWith("config-", StringComparison.Ordinal))
        {
            string id = "config." + entry.Replace("config-", "", StringComparison.Ordinal);
            return SystemTextCatalog.Defaults.ContainsKey(id) ? id : null;
        }
        if (screen is "mods" or "mods-actions" or "mods-create")
        {
            return entry switch
            {
                "installed-tab" or "mods-installed-tab" => "mods.installed",
                "create-tab" or "create" => "mods.create",
                "mod-search" or "search" => "mods.search",
                "mod-refresh" or "refresh" => "mods.refresh",
                "inspect" => "mods.details",
                "selection" => "mods.toggle",
                "clear" => "mods.clearfilters",
                "mod-auto" or "policy" => "mods.autoenable",
                "diagnostics" => "mods.diagnostics",
                "gf-review" => "mods.reviewguardian",
                "mods-create-new" => "mods.newguardian",
                "mods-edit-existing" => "mods.editguardian",
                "mods-edit-playthrough" => "mods.editplaythrough",
                "mods-localization-new" => "localization.new",
                "mods-localization-convert" => "localization.convert",
                "mods-localization-stack" => "localization.stack",
                "controls" => "config.controls", _ => null
            };
        }
        if (screen.StartsWith("localization-", StringComparison.Ordinal))
        {
            return entry switch
            {
                "preview" => "localization.preview",
                "write" => "localization.write",
                "cancel" when screen == "localization-review" => "localization.canceloperation",
                "apply" => "localization.applystack",
                "back" => "common.back",
                "cancel" => "common.cancel",
                "done" => "common.done",
                "delete" => "common.delete",
                "clear" => "common.clear", _ => null
            };
        }
        return null;
    }
}
