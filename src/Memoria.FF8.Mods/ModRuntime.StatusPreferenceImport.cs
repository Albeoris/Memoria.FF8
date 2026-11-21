using System.Text.Json;

namespace Memoria.FF8.Mods;

public sealed partial class ModRuntime
{
    private const string LegacyStatusComponent = "Meltdown";
    private const string ImportedStatusMod = "memoria-meltdown";
    private string LegacyStatusDirectory => Path.Combine(legacyModsRoot, "Memoria", LegacyStatusComponent);

    private MemoriaSettings ImportStatusPreferences()
    {
        MemoriaSettings settings = ReadMemoriaSettings();
        if (!File.Exists(Path.Combine(bundledModsRoot, "Memoria", LegacyStatusComponent, NativeModDescriptor.FileName)))
        {
            return settings;
        }

        bool hasLegacyFiles = IsLegacyStatusPreferenceDirectory(LegacyStatusDirectory);
        byte[]? central = File.Exists(MemoriaSettingsPath) ? File.ReadAllBytes(MemoriaSettingsPath) : null;
        using JsonDocument? observed = central is not null ? JsonDocument.Parse(central) : null;
        if (observed?.RootElement.TryGetProperty("statusSettings", out _) == true || central is null && !hasLegacyFiles)
        {
            return settings;
        }

        using FileStream settingsLock = ModConfiguration.LockMemoriaSettings(MemoriaSettingsPath);
        central = File.Exists(MemoriaSettingsPath) ? File.ReadAllBytes(MemoriaSettingsPath) : null;
        using JsonDocument? previous = central is not null ? JsonDocument.Parse(central) : null;
        settings = central is not null ? ParseMemoriaSettings(central) : new();
        if (previous?.RootElement.TryGetProperty("statusSettings", out _) == true)
        {
            return settings;
        }

        string legacyIni = Path.Combine(LegacyStatusDirectory, "mod.ini");
        string legacyConfig = Path.Combine(LegacyStatusDirectory, "config.json");
        bool hadLegacyComponent = settings.Components.Remove(LegacyStatusComponent, out bool enabled);
        JsonElement centralStatus = default;
        bool hasCentralStatus = previous?.RootElement.TryGetProperty("meltdown", out centralStatus) == true;
        if (!hasLegacyFiles && !hadLegacyComponent && !hasCentralStatus)
        {
            return settings;
        }
        if (hasLegacyFiles)
        {
            ValidatePreferencePath(legacyIni);
            ValidatePreferencePath(legacyConfig);
            if (!hadLegacyComponent && ModConfiguration.ReadBoolean(legacyIni, "enabled") is bool legacyEnabled)
            {
                enabled = legacyEnabled;
                hadLegacyComponent = true;
            }
            if (File.Exists(legacyConfig))
            {
                using JsonDocument config = JsonDocument.Parse(File.ReadAllBytes(legacyConfig));
                if (config.RootElement.TryGetProperty("meltdown", out JsonElement legacyStatus))
                {
                    ImportLegacyStatusValues(legacyStatus, settings.StatusSettings);
                }
            }
        }
        if (hasCentralStatus)
        {
            ImportLegacyStatusValues(centralStatus, settings.StatusSettings);
        }
        if (hadLegacyComponent)
        {
            settings.Components.TryAdd(ImportedStatusMod, enabled);
        }

        WriteMemoriaSettings(settings);
        return settings;
    }

    private bool IsLegacyStatusPreferenceDirectory(string root)
    {
        if (!root.Equals(LegacyStatusDirectory, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(root)
            || !File.Exists(Path.Combine(bundledModsRoot, "Memoria", LegacyStatusComponent, NativeModDescriptor.FileName))
            || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
        {
            return false;
        }
        return Directory.EnumerateFileSystemEntries(root).All(path =>
        {
            string name = Path.GetFileName(path);
            return name is "config.json" or "mod.ini" && File.Exists(path)
                || name == "gameplay" && Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0
                    && Directory.EnumerateFileSystemEntries(path).All(child => Path.GetFileName(child) == "behaviors.json" && File.Exists(child));
        });
    }

    private static void ImportLegacyStatusValues(JsonElement values, Dictionary<string, int> preferences)
    {
        if (values.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Invalid legacy Meltdown preference object.");
        }
        foreach (JsonProperty property in values.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out int value) || value is < 0 or > 100)
            {
                throw new InvalidDataException($"Invalid legacy Meltdown preference '{property.Name}'.");
            }
            switch (property.Name)
            {
                case "defenseReductionPercent":
                    preferences[$"{ImportedStatusMod}/Vit0/Vitality"] = value;
                    preferences[$"{ImportedStatusMod}/Vit0/Spirit"] = value;
                    break;
                case "VitalityReductionPercent":
                    preferences[$"{ImportedStatusMod}/Vit0/Vitality"] = value;
                    break;
                case "SpiritReductionPercent":
                    preferences[$"{ImportedStatusMod}/Vit0/Spirit"] = value;
                    break;
                default:
                    throw new InvalidDataException($"Unknown legacy Meltdown preference '{property.Name}'.");
            }
        }
    }
}
