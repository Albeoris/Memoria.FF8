namespace Memoria.FF8.Resources.Localization;

/// <summary>Authored interface text with semantic identities independent of screen and source ordering.</summary>
public static partial class SystemTextCatalog
{
    public const string File = "interface_system";
    public static IReadOnlyDictionary<string, string> Defaults { get; } = CreateDefaults();
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> semanticKeys = Defaults.Keys.ToDictionary(id => id, id => (IReadOnlyList<string>)Array.AsReadOnly(AuthoredPrimaryId(id) is { } primary ? new[] { Key(id), Key(primary) } : new[] { Key(id) }), StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> catalogAliases = semanticKeys.Values.Where(keys => keys.Count > 1).ToDictionary(keys => keys[0], StringComparer.Ordinal);
    private const string MissingTranslation = "\0memoria-system-text-missing\0";

    private static IReadOnlyDictionary<string, string> CreateDefaults()
    {
        Dictionary<string, string> defaults = new(StringComparer.Ordinal)
        {
            ["main.menu"] = "Menu",
            ["main.junction"] = "Junction",
            ["main.item"] = "Item",
            ["main.magic"] = "Magic",
            ["main.guardian"] = "GF",
            ["main.ability"] = "Ability",
            ["main.status"] = "Status",
            ["main.party"] = "Switch",
            ["main.card"] = "Card",
            ["main.config"] = "Config",
            ["main.tutorial"] = "Tutorial",
            ["main.save"] = "Save",
            ["main.mods"] = "Mods",
            ["config.graphics"] = "Graphics",
            ["config.audio"] = "Audio",
            ["config.controls"] = "Controls",
            ["config.interface"] = "Interface",
            ["config.gameplay"] = "Gameplay",
            ["config.mods"] = "Mods",
            ["config.combat"] = "Combat",
            ["config.magic"] = "Magic and Guardian Forces",
            ["config.exploration"] = "Exploration and saving",
            ["config.chocobo"] = "Chocobo World",
            ["config.text"] = "Text and fonts",
            ["config.frame-limits"] = "Frame rate limits",
            ["config.keyboard"] = "Keyboard bindings",
            ["config.controller"] = "Controller bindings",
            ["config.controller-settings"] = "Controller settings",
            ["setting.frame-counter"] = "Frame-rate counter",
            ["setting.window-mode"] = "Window mode",
            ["setting.vsync"] = "V-Sync",
            ["setting.msaa"] = "Anti-aliasing",
            ["setting.field-cap"] = "Field and movies",
            ["setting.world-cap"] = "World map",
            ["setting.battle-cap"] = "Battle",
            ["setting.menu-cap"] = "Menus and title",
            ["setting.quick-save-retention"] = "Quick saves to keep",
            ["setting.cursor-memory"] = "Battle cursor memory",
            ["setting.native-sound"] = "Sound",
            ["setting.native-controller"] = "Controller",
            ["setting.native-atb"] = "ATB",
            ["setting.limit-once-per-turn"] = "Limit once per turn",
            ["setting.selphie-card-slots"] = "Selphie card Slots",
            ["setting.card-mod-once"] = "Card Mod once",
            ["setting.weapon-magazine-required"] = "Weapon magazine required",
            ["setting.weapon-upgrade-order"] = "Weapon upgrade order",
            ["setting.draw-once-per-spell"] = "Draw once per spell",
            ["setting.chocobo-world"] = "Chocobo World",
            ["setting.difficulty"] = "Difficulty",
            ["setting.atb-mode"] = "ATB mode",
            ["setting.shared-magic"] = "Shared magic stock",
            ["setting.infinite-magic"] = "Infinite magic",
            ["setting.smart-attack"] = "Smart Attack",
            ["setting.guardian-forms"] = "GF Transformations",
            ["setting.gunblade"] = "Gunblade trigger",
            ["setting.encounter-execution"] = "Battle policy",
            ["mods.installed"] = "Installed Mods",
            ["mods.create"] = "Create Mods",
            ["mods.search"] = "Search mods",
            ["mods.refresh"] = "Refresh",
            ["mods.details"] = "Package details",
            ["mods.toggle"] = "Enable / Disable selected mod",
            ["mods.clearfilters"] = "Clear filters",
            ["mods.autoenable"] = "Automatically enable new mods",
            ["mods.diagnostics"] = "View diagnostics",
            ["mods.reviewguardian"] = "Review GF changes",
            ["mods.newguardian"] = "Create new mod",
            ["mods.editguardian"] = "Edit existing GF mod",
            ["mods.editplaythrough"] = "Edit this playthrough",
            ["mods.authoringtitle"] = "Gameplay and localization",
            ["mods.authoringhelp"] = "Create GF ability contributions or translation projects.",
            ["localization.new"] = "New localization",
            ["localization.convert"] = "Convert localization",
            ["localization.stack"] = "Localization stack",
            ["localization.preview"] = "Prepare preview",
            ["localization.write"] = "Write package",
            ["localization.canceloperation"] = "Cancel operation",
            ["localization.applystack"] = "Apply selected stack",
            ["common.back"] = "Back",
            ["common.cancel"] = "Cancel",
            ["common.done"] = "Done",
            ["common.delete"] = "Delete",
            ["common.clear"] = "Clear"
        };
        AddAuthoredDefaults(defaults);
        AddGuardianForceEditorDefaults(defaults);
        InitializeChocoboDefaults(defaults);
        return defaults;
    }

    public static string Key(string id) => "$en_" + File + "_" + id;

    public static IReadOnlyList<string> KeysForSemanticId(string id) => semanticKeys.TryGetValue(id, out IReadOnlyList<string>? keys) ? keys : new[] { Key(id) };

    public static IReadOnlyList<string>? KeysForCatalogKey(string key) => catalogAliases.TryGetValue(key, out IReadOnlyList<string>? keys) ? keys : null;

    public static string Translate(Func<string, string, string>? localize, string id, string original)
    {
        if (localize is null)
        {
            return original;
        }
        foreach (string key in KeysForSemanticId(id))
        {
            string translated = localize(key, MissingTranslation);
            if (translated != MissingTranslation)
            {
                return translated;
            }
        }
        return original;
    }

    public static string Format(string template, string original, params object?[] arguments)
    {
        try
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, template, arguments);
        }
        catch (FormatException)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, original, arguments);
        }
    }

    public static IReadOnlyList<LocalizationSourceEntry> SourceEntries() => Defaults.Select(pair => new LocalizationSourceEntry("System", File, Key(pair.Key), pair.Value, "en", new Dictionary<string, string?> { ["Authored ID"] = pair.Key })).ToArray();
}
