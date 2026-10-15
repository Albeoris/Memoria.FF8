using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed record BattleDebugPreset(BattleDefinition Definition, BattleAtbMode Mode);

/// <summary>Explicit local laboratory presets, separate from authentic game saves.</summary>
public sealed class BattleDebugPresets
{
    private readonly string? path;
    private readonly BattleDebugPreset?[] slots;

    public BattleDebugPresets(string? path = null)
    {
        this.path = path;
        slots = path is not null && File.Exists(path) ? JsonSerializer.Deserialize<BattleDebugPreset?[]>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty battle presets.") : new BattleDebugPreset?[3];
        if (slots.Length != 3)
        {
            throw new InvalidDataException("Battle presets require three slots.");
        }
        foreach (BattleDebugPreset? preset in slots)
        {
            if (preset is not null)
            {
                _ = new BattleSession(preset.Definition, preset.Mode);
            }
        }
    }

    public BattleDebugPreset? Read(int slot)
    {
        BattleDebugPreset? preset = slots[slot];
        return preset is null ? null : preset with { Definition = BattleSession.CloneDefinition(preset.Definition) };
    }

    public void Save(int slot, BattleDefinition definition, BattleAtbMode mode)
    {
        _ = new BattleSession(definition, mode);
        BattleDebugPreset?[] next = (BattleDebugPreset?[])slots.Clone();
        next[slot] = new BattleDebugPreset(BattleSession.CloneDefinition(definition), mode);
        if (path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        slots[slot] = next[slot];
    }
}
