using System.Text.Json;

namespace Memoria.FF8.GameUi;

/// <summary>Local presentation preferences; never seed or alter game-owned saved state.</summary>
internal sealed record UiPreferences
{
    public int MusicVolume { get; init; } = 100;
    public int SoundVolume { get; init; } = 100;

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
        if (!int.TryParse(value.TrimEnd('%'), out int number) || number is < 0 or > 100)
        {
            throw new ArgumentException("Audio volume must be between 0 and 100 percent.");
        }
        return key switch
        {
            "Music volume" => this with { MusicVolume = number },
            "Effect volume" or "Sound volume" => this with { SoundVolume = number },
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
        if (MusicVolume is < 0 or > 100 || SoundVolume is < 0 or > 100)
        {
            throw new InvalidDataException("UI audio preferences must be between 0 and 100 percent.");
        }
    }
}
