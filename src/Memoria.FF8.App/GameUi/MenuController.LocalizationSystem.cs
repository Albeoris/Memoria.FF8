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

    private string SystemText(string id, string original) => localizeInterface?.Invoke(SystemTextCatalog.Key(id), original) ?? original;

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
