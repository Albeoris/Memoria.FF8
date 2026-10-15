using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>Local presentation preferences; never seed or alter game-owned saved state.</summary>
internal sealed record UiPreferences
{
    public int MusicVolume { get; init; } = 100;
    public int SoundVolume { get; init; } = 100;
    public int TextScale { get; init; } = 100;
    public BattleAtbMode AtbMode { get; init; } = BattleAtbMode.Normal;
    public string BattlePolicy { get; init; } = "ResearchInstantVictory";
    public bool ShowInitiative { get; init; }

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
        if (key == "Battle ATB mode")
        {
            return value switch
            {
                "Normal" => this with { AtbMode = BattleAtbMode.Normal },
                "TurnBased" => this with { AtbMode = BattleAtbMode.TurnBased },
                _ => throw new ArgumentException("Unsupported battle ATB mode.")
            };
        }
        if (key == "Battle policy")
        {
            return value is "ResearchInstantVictory" or "NewGameFullCombat" ? this with { BattlePolicy = value } : throw new ArgumentException("Unsupported battle policy.");
        }
        if (key == "Initiative bar")
        {
            return value switch
            {
                "On" => this with { ShowInitiative = true },
                "Off" => this with { ShowInitiative = false },
                _ => throw new ArgumentException("Initiative bar requires On or Off.")
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
        if (MusicVolume is < 0 or > 100 || SoundVolume is < 0 or > 100 || TextScale is not (100 or 125 or 150) || !Enum.IsDefined(AtbMode) || BattlePolicy is not ("ResearchInstantVictory" or "NewGameFullCombat"))
        {
            throw new InvalidDataException("UI presentation preferences are outside their supported ranges.");
        }
    }
}
