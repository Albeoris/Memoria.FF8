using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>Local presentation preferences and battle settings applied to the running game.</summary>
internal sealed record UiPreferences
{
    public int WindowWidth { get; init; } = 1024;
    public int WindowHeight { get; init; } = 768;
    public bool VSync { get; init; } = true;
    public int FieldFps { get; init; } = 60;
    public int BattleFps { get; init; } = 60;
    public int MenuFps { get; init; } = 60;
    public int MusicVolume { get; init; } = 100;
    public int SoundVolume { get; init; } = 100;
    public int TextScale { get; init; } = 100;
    public string UiFontFamily { get; init; } = SystemFontCatalog.DefaultFamily;
    public int UiFontSize { get; init; } = 24;
    public string DialogueFontFamily { get; init; } = SystemFontCatalog.DefaultFamily;
    public int DialogueFontSize { get; init; } = 8;
    public BattleAtbMode AtbMode { get; init; } = BattleAtbMode.TurnBased;
    public BattleGunbladeMode GunbladeMode { get; init; } = BattleGunbladeMode.Automatic;
    public string BattlePolicy { get; init; } = "ResearchInstantVictory";
    [System.Text.Json.Serialization.JsonIgnore]
    public string BattlePolicyLabel => BattlePolicy == "NewGameFullCombat" ? "Full combat" : "Instant victory";
    public bool RandomEncounters { get; init; } = true;
    public bool RareCardMarkers { get; init; } = true;
    public bool CursorMemory { get; init; }
    public int FieldMessageSpeed { get; init; } = 3;

    public (int Width, int Height) WindowSize(LaunchOptions options)
    {
        return options.ResolutionSpecified ? (options.Width, options.Height) : (WindowWidth, WindowHeight);
    }

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
        if (key == "V-Sync")
        {
            return value switch
            {
                "On" => this with { VSync = true },
                "Off" => this with { VSync = false },
                _ => throw new ArgumentException("V-Sync requires On or Off.")
            };
        }
        if (key is "Field FPS" or "Battle FPS" or "Menu FPS")
        {
            int fps = value == "Unlimited" ? 0 : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            if (fps is not (0 or 15 or 30 or 60 or 90 or 120 or 144 or 240))
            {
                throw new ArgumentException("Unsupported presentation frame limit.");
            }
            return key switch
            {
                "Field FPS" => this with { FieldFps = fps },
                "Battle FPS" => this with { BattleFps = fps },
                _ => this with { MenuFps = fps }
            };
        }
        if (key == "Cursor")
        {
            return value switch
            {
                "Initial" => this with { CursorMemory = false },
                "Memory" => this with { CursorMemory = true },
                _ => throw new ArgumentException("Cursor requires Initial or Memory.")
            };
        }
        if (key == "Field message")
        {
            if (!int.TryParse(value, out int speed) || speed is < 1 or > 5)
            {
                throw new ArgumentException("Field message speed must be between 1 and 5.");
            }
            return this with { FieldMessageSpeed = speed };
        }
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
                "Classic" or "Normal" => this with { AtbMode = BattleAtbMode.Normal },
                "Turn Based" or "TurnBased" => this with { AtbMode = BattleAtbMode.TurnBased },
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

    public UiPreferences WithInstallationSetting(string key, string value, bool activeGame)
    {
        if (activeGame && key is "Battle ATB mode" or "Gunblade trigger" or "Battle policy" or "Random encounters")
        {
            return this;
        }
        return WithSetting(key, value);
    }

    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }

    private static bool ValidFps(int fps) => fps is 0 or 15 or 30 or 60 or 90 or 120 or 144 or 240;

    private void Validate()
    {
        if (WindowWidth < 1 || WindowHeight < 1)
        {
            throw new InvalidDataException("Window dimensions must be positive.");
        }
        if (!ValidFps(FieldFps) || !ValidFps(BattleFps) || !ValidFps(MenuFps) || MusicVolume is < 0 or > 100 || SoundVolume is < 0 or > 100 || TextScale is not (100 or 125 or 150) || FieldMessageSpeed is < 1 or > 5 || UiFontSize is < 6 or > 48 || DialogueFontSize is < 6 or > 48 || string.IsNullOrWhiteSpace(UiFontFamily) || string.IsNullOrWhiteSpace(DialogueFontFamily) || !Enum.IsDefined(AtbMode) || !Enum.IsDefined(GunbladeMode) || BattlePolicy is not ("ResearchInstantVictory" or "NewGameFullCombat"))
        {
            throw new InvalidDataException("UI presentation preferences are outside their supported ranges.");
        }
    }
}
