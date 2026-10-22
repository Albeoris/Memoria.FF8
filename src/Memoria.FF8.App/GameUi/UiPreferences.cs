using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>Local presentation preferences and battle settings applied to the running game.</summary>
internal sealed record UiPreferences
{
    public MenuPresentation MenuPresentation { get; init; } = MenuPresentation.Vector;
    public GraphicsPreferences Graphics { get; init; } = new();
    public int WindowWidth { get; init; } = 1024;
    public int WindowHeight { get; init; } = 768;
    public bool VSync { get; init; } = true;
    public bool ShowFps { get; init; }
    public int FieldFps { get; init; } = 60;
    public int WorldFps { get; init; } = 60;
    public int BattleFps { get; init; } = 60;
    public int MenuFps { get; init; } = 60;
    public int MusicVolume { get; init; } = 100;
    public int SoundVolume { get; init; } = 100;
    public int TextScale { get; init; } = 100;
    public int QuickSaveCount { get; init; } = 9;
    private static readonly IMenuPresentation ClassicFontDefaults = new ClassicMenuPresentation();
    private static readonly IMenuPresentation VectorFontDefaults = new VectorMenuPresentation();
    private string legacyUiFontFamily = SystemFontCatalog.DefaultFamily;

    // The original shared setting used Arial directly in Classic and as a placeholder for Vector's default.
    // Preserve other shared choices for both skins until each skin receives its own override.
    [System.Text.Json.Serialization.JsonPropertyName("UiFontFamily")]
    public string LegacyUiFontFamily
    {
        get => legacyUiFontFamily;
        init => legacyUiFontFamily = value;
    }
    public string? ClassicUiFontFamily { get; init; }
    public string? VectorUiFontFamily { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string UiFontFamily
    {
        get => GetUiFontFamily(MenuPresentation);
        init => legacyUiFontFamily = value;
    }

    public string GetUiFontFamily(MenuPresentation presentation)
    {
        string? family = presentation == MenuPresentation.Vector ? VectorUiFontFamily : ClassicUiFontFamily;
        if (family is not null)
        {
            return family;
        }
        if (!string.Equals(legacyUiFontFamily, SystemFontCatalog.DefaultFamily, StringComparison.OrdinalIgnoreCase))
        {
            return legacyUiFontFamily;
        }
        IMenuPresentation provider = presentation == MenuPresentation.Vector ? VectorFontDefaults : ClassicFontDefaults;
        return provider.PreferredFontFamily;
    }
    public int UiFontSize { get; init; } = 24;
    public string DialogueFontFamily { get; init; } = SystemFontCatalog.DefaultFamily;
    public int DialogueFontSize { get; init; } = 10;
    public BattleDifficulty Difficulty { get; init; } = BattleDifficulty.Normal;
    public BattleAtbMode AtbMode { get; init; } = BattleAtbMode.TurnBased;
    public BattleGunbladeMode GunbladeMode { get; init; } = BattleGunbladeMode.Automatic;
    public string BattlePolicy { get; init; } = "ResearchInstantVictory";
    [System.Text.Json.Serialization.JsonIgnore]
    public string BattlePolicyLabel => BattlePolicy == "NewGameFullCombat" ? "Full combat" : "Instant victory";
    public bool GuardianFormsEnabled { get; init; }
    public bool RandomEncounters { get; init; } = true;
    public bool RareCardMarkers { get; init; }
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
        preferences = preferences with
        {
            FieldFps = preferences.FieldFps switch
            {
                15 => 30,
                0 or 90 or 120 or 144 or 240 => 60,
                _ => preferences.FieldFps
            }
        };
        preferences.Validate();
        return preferences;
    }

    public UiPreferences WithSetting(string key, string value)
    {
        if (key == "UI presentation")
        {
            return value switch
            {
                "Vector" => this with { MenuPresentation = MenuPresentation.Vector },
                "Classic" => this with { MenuPresentation = MenuPresentation.Classic },
                _ => throw new ArgumentException("UI presentation requires Vector or Classic.")
            };
        }
        if (key == "Anti-aliasing")
        {
            int samples = value switch
            {
                "Off" => 0,
                "MSAA 2x" => 2,
                "MSAA 4x" => 4,
                "MSAA 8x" => 8,
                _ => throw new ArgumentException("Anti-aliasing requires Off, MSAA 2x, MSAA 4x or MSAA 8x.")
            };
            return this with { Graphics = Graphics with { MsaaSamples = samples } };
        }
        if (key == "Show FPS")
        {
            return value switch
            {
                "On" => this with { ShowFps = true },
                "Off" => this with { ShowFps = false },
                _ => throw new ArgumentException("Show FPS requires On or Off.")
            };
        }
        if (key == "V-Sync")
        {
            return value switch
            {
                "On" => this with { VSync = true },
                "Off" => this with { VSync = false },
                _ => throw new ArgumentException("V-Sync requires On or Off.")
            };
        }
        if (key is "Battle FPS" or "Menu FPS")
        {
            int fps = value == "Unlimited" ? 0 : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            if (fps is not (0 or 15 or 30 or 60 or 90 or 120 or 144 or 240))
            {
                throw new ArgumentException("Unsupported presentation frame limit.");
            }
            return key == "Battle FPS" ? this with { BattleFps = fps } : this with { MenuFps = fps };
        }
        if (key is "Field FPS" or "World FPS")
        {
            if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int fps) || fps is not (30 or 60))
            {
                throw new ArgumentException($"{key} must be 30 or 60.");
            }
            return key == "Field FPS" ? this with { FieldFps = fps } : this with { WorldFps = fps };
        }
        if (key == "Quick saves")
        {
            if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int count) || count < 1)
            {
                throw new ArgumentException("Quick saves must be a positive integer.");
            }
            return this with { QuickSaveCount = count };
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
            if (key == "Dialogue font")
            {
                return this with { DialogueFontFamily = family };
            }
            return MenuPresentation == MenuPresentation.Vector ? this with { VectorUiFontFamily = family } : this with { ClassicUiFontFamily = family };
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
        if (key == "GF Transformations")
        {
            return value switch
            {
                "On" => this with { GuardianFormsEnabled = true },
                "Off" => this with { GuardianFormsEnabled = false },
                _ => throw new ArgumentException("GF Transformations requires On or Off.")
            };
        }
        if (key == "Difficulty")
        {
            return value switch
            {
                "Normal" => this with { Difficulty = BattleDifficulty.Normal },
                "Hard" => this with { Difficulty = BattleDifficulty.Hard },
                _ => throw new ArgumentException("Difficulty requires Normal or Hard.")
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
        if (activeGame && key is "Difficulty" or "Battle ATB mode" or "Gunblade trigger" or "Battle policy" or "Random encounters" or "GF Transformations")
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
        if (Graphics is null)
        {
            throw new InvalidDataException("Graphics preferences must be an object.");
        }
        Graphics.Validate();
        if (WindowWidth < 1 || WindowHeight < 1)
        {
            throw new InvalidDataException("Window dimensions must be positive.");
        }
        if (!Enum.IsDefined(MenuPresentation) || QuickSaveCount < 1 || FieldFps is not (30 or 60) || WorldFps is not (30 or 60) || !ValidFps(BattleFps) || !ValidFps(MenuFps) || MusicVolume is < 0 or > 100 || SoundVolume is < 0 or > 100 || TextScale is not (100 or 125 or 150) || FieldMessageSpeed is < 1 or > 5 || UiFontSize is < 6 or > 48 || DialogueFontSize is < 6 or > 48 || string.IsNullOrWhiteSpace(LegacyUiFontFamily) || ClassicUiFontFamily is not null && string.IsNullOrWhiteSpace(ClassicUiFontFamily) || VectorUiFontFamily is not null && string.IsNullOrWhiteSpace(VectorUiFontFamily) || string.IsNullOrWhiteSpace(DialogueFontFamily) || !Enum.IsDefined(Difficulty) || !Enum.IsDefined(AtbMode) || !Enum.IsDefined(GunbladeMode) || BattlePolicy is not ("ResearchInstantVictory" or "NewGameFullCombat"))
        {
            throw new InvalidDataException("UI presentation preferences are outside their supported ranges.");
        }
    }
}
