using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>Local presentation preferences and battle settings applied to the running game.</summary>
internal sealed record UiPreferences
{
    public int MusicVolume { get; init; } = 100;
    public int SoundVolume { get; init; } = 100;
    public int TextScale { get; init; } = 100;
    public string UiFontFamily { get; init; } = SystemFontCatalog.DefaultFamily;
    public int UiFontSize { get; init; } = 24;
    public string DialogueFontFamily { get; init; } = SystemFontCatalog.DefaultFamily;
    public int DialogueFontSize { get; init; } = 8;
    public BattleAtbMode AtbMode { get; init; } = BattleAtbMode.Normal;
    public BattleGunbladeMode GunbladeMode { get; init; } = BattleGunbladeMode.Automatic;
    public string BattlePolicy { get; init; } = "ResearchInstantVictory";
    [System.Text.Json.Serialization.JsonIgnore]
    public string BattlePolicyLabel => BattlePolicy == "NewGameFullCombat" ? "Full combat" : "Instant victory";
    public bool RandomEncounters { get; init; } = true;
    public bool RareCardMarkers { get; init; } = true;

    public BattleInput[] CreateBattleSettingsInputs()
    {
        Validate();
        return [new BattleInput(BattleInputKind.ConfigurePlaythrough, FullCombat: BattlePolicy == "NewGameFullCombat"), new BattleInput(BattleInputKind.ConfigureEncounters, RandomEncounters: RandomEncounters)];
    }

    public static UiPreferences Load(string path)
    {
        if (!File.Exists(path))
        {
            return new UiPreferences();
        }
        UiPreferences preferences = JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty UI preferences.");
        preferences.Validate();
        return preferences;
    }

    public UiPreferences WithSetting(string key, string value)
    {
        if (key is "UI font" or "Dialogue font")
        {
            string family = SystemFontCatalog.Families.FirstOrDefault(family => string.Equals(family, value, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Choose an installed OS font family.");
            return key == "UI font" ? this with { UiFontFamily = family } : this with { DialogueFontFamily = family };
        }
        if (key is "UI font size" or "Dialogue font size")
        {
            if (!int.TryParse(value, out int size) || size is < 6 or > 48)
            {
                throw new ArgumentException("Font size must be between 6 and 48 pixels.");
            }
            return key == "UI font size" ? this with { UiFontSize = size } : this with { DialogueFontSize = size };
        }
        if (key == "Rare card markers")
        {
            return value switch
            {
                "On" => this with { RareCardMarkers = true },
                "Off" => this with { RareCardMarkers = false },
                _ => throw new ArgumentException("Rare card markers requires On or Off.")
            };
        }
        if (key == "Random encounters")
        {
            return value switch
            {
                "On" => this with { RandomEncounters = true },
                "Off" => this with { RandomEncounters = false },
                _ => throw new ArgumentException("Random encounters requires On or Off.")
            };
        }
        if (key == "Battle ATB mode")
        {
            return value switch
            {
                "Normal" => this with { AtbMode = BattleAtbMode.Normal },
                "TurnBased" => this with { AtbMode = BattleAtbMode.TurnBased },
                _ => throw new ArgumentException("Unsupported battle ATB mode.")
            };
        }
        if (key == "Gunblade trigger")
        {
            return value switch
            {
                "Manual" => this with { GunbladeMode = BattleGunbladeMode.Manual },
                "Automatic" => this with { GunbladeMode = BattleGunbladeMode.Automatic },
                _ => throw new ArgumentException("Gunblade trigger requires Manual or Automatic.")
            };
        }
        if (key == "Battle policy")
        {
            return value switch
            {
                "Instant victory" or "ResearchInstantVictory" => this with { BattlePolicy = "ResearchInstantVictory" },
                "Full combat" or "NewGameFullCombat" => this with { BattlePolicy = "NewGameFullCombat" },
                _ => throw new ArgumentException("Unsupported battle policy.")
            };
        }
        if (!int.TryParse(value.TrimEnd('%'), out int number))
        {
            throw new ArgumentException("A presentation preference requires a percentage.");
        }
        return key switch
        {
            "Music volume" when number is >= 0 and <= 100 => this with { MusicVolume = number },
            "Effect volume" or "Sound volume" when number is >= 0 and <= 100 => this with { SoundVolume = number },
            "Text scale" when number is 100 or 125 or 150 => this with { TextScale = number },
            "Music volume" or "Effect volume" or "Sound volume" or "Text scale" => throw new ArgumentException("Presentation preference is outside its supported range."),
            _ => throw new NotSupportedException($"The host does not implement setting '{key}'.")
        };
    }

    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }

    private void Validate()
    {
        if (MusicVolume is < 0 or > 100 || SoundVolume is < 0 or > 100 || TextScale is not (100 or 125 or 150) || UiFontSize is < 6 or > 48 || DialogueFontSize is < 6 or > 48 || string.IsNullOrWhiteSpace(UiFontFamily) || string.IsNullOrWhiteSpace(DialogueFontFamily) || !Enum.IsDefined(AtbMode) || !Enum.IsDefined(GunbladeMode) || BattlePolicy is not ("ResearchInstantVictory" or "NewGameFullCombat"))
        {
            throw new InvalidDataException("UI presentation preferences are outside their supported ranges.");
        }
    }
}
