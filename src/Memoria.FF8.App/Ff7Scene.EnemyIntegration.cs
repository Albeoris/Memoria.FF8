using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private ModRuntime? enemyIntegrationMods;
    private string? enemyIntegrationModsDirectory;
    private Ff7EnemyExtensionCatalog enemyExtensionCatalog = Ff7EnemyExtensionCatalog.Empty;

    internal void ConfigureEnemyIntegrationMods(string? modsDirectory)
    {
        enemyIntegrationModsDirectory = modsDirectory;
    }

    private void AttachEnemyIntegration()
    {
        bool initialLoad = enemyIntegrationMods is null;
        if (enemyIntegrationMods is null)
        {
            enemyIntegrationMods = new ModRuntime(ApplicationInstallation.DataDirectory, format => format == ModFormat.Memoria, candidate =>
            {
                if (!Ff7EnemyExtensionMods.TryPrepare(candidate, out Ff7EnemyExtensionCatalog prepared, out IReadOnlyList<ModDiagnostic> diagnostics))
                {
                    return new(false, Diagnostics: diagnostics);
                }
                return new(true, Diagnostics: diagnostics, Commit: () => enemyExtensionCatalog = prepared);
            }, modsDirectory: enemyIntegrationModsDirectory);
        }
        UpdateEnemyIntegrationBridge(initialLoad);
        ApplyEnemyIntegration();
    }

    private void UpdateEnemyIntegrationBridge(bool initialLoad = false)
    {
        Ff7EnemyIntegrationContext integration = envelope.EnemyIntegration ?? new();
        ModRuntime? mods = enemyIntegrationMods;
        if (mods is not null && (mods.SetFf7BridgeEnemyIntegration(integration.EnteredFromFf8 && integration.Preference == Ff7EnemyIntegrationPreference.Automatic) || initialLoad) && mods.PreparePending(true))
        {
            mods.CommitPending();
        }
    }

    private void ApplyEnemyIntegration()
    {
        bool enabled = (envelope.EnemyIntegration ?? new()).Effective && enemyExtensionCatalog.Count > 0;
        if (enabled && Session is { Battle: null } session)
        {
            session.SetFullCombatEnabled(true);
        }
        Session?.ConfigureEnemyExtensions(enemyExtensionCatalog, enabled, ReadEnemyIntegrationPartyLevels, ReadEnemyIntegrationLevelRules);
    }

    private Ff7EnemyLevelRules ReadEnemyIntegrationLevelRules()
    {
        GameplaySaveComponentDocument? campaign = envelope.Ff8PayloadFormat == CrossoverSaveAdapter.Ff8Codec && envelope.Ff8Payload is { Length: > 0 }
            ? CrossoverSaveAdapter.ReadFf8(envelope)
            : null;
        BattleDifficulty difficulty = campaign?.Progression?.Rules.Preferences.Difficulty ?? PresentationPreferences.Difficulty;
        GameplayLevelRules rules = campaign?.NewGamePlus?.LevelRules ?? GameplayLevelRules.ForDifficulty(difficulty);
        return new(rules.MaximumEnemyLevel, rules.MaximumCharacterLevel, rules.AdditionalPartyLevel);
    }

    private IReadOnlyList<int> ReadEnemyIntegrationPartyLevels()
    {
        Ff7Session session = Session ?? throw new InvalidOperationException("Enemy levels require an active FFVII party.");
        Dictionary<int, int> guests = mixedFieldVitals?.ReadAll().ToDictionary(guest => guest.StoryCharacter, guest => guest.Level) ?? [];
        return session.Progress.Party.Where(id => id != 255).Select(id => guests.TryGetValue(id, out int level) ? level : session.Progress.Characters.Single(character => character.Id == Ff7NativeCharacterIdentity.RecordId(id)).Level).ToArray();
    }

    private void PumpEnemyIntegration()
    {
        if (enemyIntegrationMods?.PreparePending() == true && enemyIntegrationMods.CommitPending())
        {
            ApplyEnemyIntegration();
        }
    }

    private void RegisterEnemyIntegrationMenu(MenuController menu)
    {
        if (enemyIntegrationMods is not null)
        {
            menu.ConnectMods(enemyIntegrationMods);
        }
        menu.Register(new()
        {
            Id = "ff7-enemy-integration",
            Title = "Mods · FFVII Enemy Integration",
            Help = "Campaign preference is retained through world travel. Changes apply to the next encounter.",
            Entries = _ => Enum.GetValues<Ff7EnemyIntegrationPreference>().Select(preference => new MenuEntry(preference.ToString(), preference.ToString(), "Apply this preference to the next encounter.", Action: new("ff7-enemy-integration", Value: preference.ToString()))).Append(new("packages", "Installed packages", "Manage installed mod packages.", "mods")).ToArray(),
            Details = _ =>
            [
                $"Preference: {(envelope.EnemyIntegration ?? new()).Preference}",
                $"Effective for the next encounter: {((envelope.EnemyIntegration ?? new()).Effective && enemyExtensionCatalog.Count > 0 ? "Enabled" : "Disabled")}",
                enemyExtensionCatalog.Count == 0 ? "No active FFVII enemy extension package is available. Check installed packages and diagnostics." : $"Prepared enemy extensions: {enemyExtensionCatalog.Count}",
                (envelope.EnemyIntegration ?? new()).EnteredFromFf8 ? "This FFVII segment was entered from FFVIII." : "No FFVIII entry is recorded for this segment.",
                "The current encounter retains its captured rules."
            ]
        });
    }

    private bool ApplyEnemyIntegrationMenuAction(MenuAction action)
    {
        if (action.Kind != "ff7-enemy-integration")
        {
            return false;
        }
        if (!Enum.TryParse(action.Value, out Ff7EnemyIntegrationPreference preference) || !Enum.IsDefined(preference))
        {
            throw new InvalidDataException("Unknown FFVII enemy integration preference.");
        }
        envelope = envelope with { EnemyIntegration = (envelope.EnemyIntegration ?? new()) with { Preference = preference } };
        UpdateEnemyIntegrationBridge();
        ApplyEnemyIntegration();
        return true;
    }
}
