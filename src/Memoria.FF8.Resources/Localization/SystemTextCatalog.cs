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
            ["title.new-game"] = "NEW GAME",
            ["title.continue"] = "Continue",
            ["title.config"] = "Config",
            ["title.language"] = "Language",
            ["title.wordmark"] = "FINAL FANTASY VIII",
            ["language.help"] = "Expand a language to see its available editions.",
            ["language.expand"] = "Show editions",
            ["language.collapse"] = "Hide editions",
            ["language.original"] = "Original game",
            ["language.installed"] = "Installed",
            ["language.download"] = "Download",
            ["language.download-size"] = "Download size: {0}",
            ["language.unknown-size"] = "Unknown",
            ["language.selected"] = "Current choice",
            ["language.update-available"] = "Update available",
            ["language.source-installed-game"] = "Installed game",
            ["language.source-installed-mod"] = "Installed mod",
            ["language.edition-classic"] = "Classic",
            ["language.edition-remastered"] = "Remastered",
            ["language.provider-ilp-retranslation"] = "ILP retranslation",
            ["language.provider-succession"] = "Succession (story rewrite)",
            ["language.package-name.russian-ffrtt-opening"] = "FFRTT Opening Movie",
            ["language.native-base-or"] = "{0} or {1}",
            ["language.requires-native-base"] = "Requires {0} game data.",
            ["language.requires-engine-version"] = "Requires Memoria {0} or later.",
            ["language.unsupported-base"] = "This provider has no supported native base.",
            ["language.unknown-engine-version"] = "The installed Memoria engine version is unknown.",
            ["language.unreviewed-mapping"] = "The download has no verified mapping to a package language option.",
            ["language.junction-base"] = "Junction packages currently require Classic English game data.",
            ["language.missing-option"] = "The installed release does not declare this language option.",
            ["language.missing-dependencies"] = "Required language packages are missing or incompatible.",
            ["language.base"] = "Native resources: {0} · {1}",
            ["language.confirm"] = "Use this language edition?",
            ["language.apply"] = "Apply",
            ["language.download-apply"] = "Download and apply",
            ["language.update"] = "Update and apply",
            ["language.update-confirm"] = "Update this language edition?",
            ["language.remove"] = "Remove installed package",
            ["language.remove-confirm"] = "Remove this language package?",
            ["language.cancel"] = "Cancel",
            ["language.cancel-progress"] = "Cancel the language change.",
            ["language.cancelled"] = "Language change cancelled.",
            ["language.cancelling"] = "Cancelling…",
            ["language.changing"] = "Changing language",
            ["language.applying"] = "Preparing language resources…",
            ["language.downloading"] = "Downloading language packages…",
            ["language.verifying"] = "Verifying language package…",
            ["language.installing"] = "Installing language package…",
            ["language.applying-selection"] = "Applying language…",
            ["language.current-package"] = "Package: {0}",
            ["language.download-progress"] = "{0:N1} / {1:N1} MiB · {2:P1}",
            ["language.download-received"] = "{0:N1} MiB received",
            ["language.applied"] = "Language applied.",
            ["language.change-failed"] = "Could not change the language.",
            ["language.remove-failed"] = "Could not remove the language package.",
            ["language.refresh"] = "Refresh language catalog",
            ["language.refreshing"] = "Refreshing language catalog…",
            ["language.offer"] = "Use your display language?",
            ["language.offer.help"] = "Compatible editions are available for {0}.",
            ["language.offer.view"] = "View editions",
            ["language.offer.decline"] = "Keep current language",
            ["language.title-only"] = "Return to the title screen to apply a language edition.",
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
            ["setting.invincibility-once-per-battle"] = "Invincibility once per battle",
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
            ["setting.infinite-magic"] = "Limited magic",
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
        AddJunctionDefaults(defaults);
        AddBattleLoadoutDefaults(defaults);
        AddWorkbenchDefaults(defaults);
        InitializeChocoboDefaults(defaults);
        InitializeNewGamePlusDefaults(defaults);
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
        return LocalizationTextTrace.Attach(original, new(Key(id), original, original, original));
    }

    public static string Format(string template, string original, params object?[] arguments)
    {
        try
        {
            return LocalizationTextTrace.Formatted(template, string.Format(System.Globalization.CultureInfo.CurrentCulture, template, arguments), arguments);
        }
        catch (FormatException)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, original, arguments);
        }
    }

    public static IReadOnlyList<LocalizationSourceEntry> SourceEntries() => Defaults.Select(pair => new LocalizationSourceEntry("System", File, Key(pair.Key), pair.Value, "en", new Dictionary<string, string?> { ["Authored ID"] = pair.Key })).ToArray();
}
