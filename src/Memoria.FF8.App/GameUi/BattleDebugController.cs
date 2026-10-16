using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed record BattleDebugEquipment(int Id, string Name, int? CharacterId = null);

/// <summary>Choices supplied by the installed-asset host; no original combat statistics are inferred by the UI.</summary>
public sealed record BattleDebugCatalog
{
    public BattleCombatantDefinition[] Players { get; init; } = [];
    public BattleCombatantDefinition[] Enemies { get; init; } = [];
    public BattleDebugEquipment[] Weapons { get; init; } = [];
    public BattleDebugEquipment[] Equipment { get; init; } = [];
    public int[] StageIds { get; init; } = [];
    public Dictionary<int, int[]> PlayerCostumes { get; init; } = [];
}

/// <summary>A separate editable scenario. Starting it never patches the running battle or research save.</summary>
public sealed class BattleDebugController
{
    private readonly BattleDebugCatalog catalog;
    private BattleDefinition initial;
    private readonly BattleDefinition defaults;
    private readonly BattleDebugPresets presets;
    private readonly HashSet<string> expanded = [];
    private readonly Action<BattleDefinition, BattleAtbMode> start;
    private readonly Action close;
    private readonly List<BattleCombatantDefinition> party;
    private readonly List<BattleCombatantDefinition> enemies;
    private readonly UiView view;
    private readonly List<BattleDebugRow> rows = [];
    private int? stageId;
    private int presetSlot;
    private bool toggleExpansion = true;

    public BattleDebugController(BattleDefinition initial, BattleDebugCatalog catalog, BattleAtbMode mode, Action<BattleDefinition, BattleAtbMode> start, Action close, BattleDebugPresets? presets = null)
    {
        this.catalog = catalog;
        this.initial = BattleSession.CloneDefinition(initial);
        defaults = BattleSession.CloneDefinition(initial);
        this.presets = presets ?? new BattleDebugPresets();
        stageId = initial.StageId;
        this.start = start;
        this.close = close;
        party = this.initial.Combatants.Where(actor => actor.IsPlayer).ToList();
        enemies = this.initial.Combatants.Where(actor => !actor.IsPlayer).ToList();
        Mode = mode;
        view = new UiView(new BattleDebugElement(this)) { DesignWidth = 960, DesignHeight = 672, Cancel = Close };
        RebuildRows();
        view.Resize(960, 672);
    }

    public BattleAtbMode Mode { get; private set; }
    public event Action<MenuSound>? SoundRequested;
    public int SelectedIndex { get; private set; }
    public string Message { get; private set; } = "Edit an isolated scenario. Current game settings are in Debug Room · Battle.";
    internal IReadOnlyList<BattleDebugRow> Rows => rows;
    public BattleDefinition Definition => initial with { Name = "Debug battle", StageId = stageId, Combatants = [.. party.Select((actor, index) => actor with { Id = $"party-{index}", IsPlayer = true }), .. enemies.Select((actor, index) => actor with { Id = $"enemy-{index}", IsPlayer = false })] };

    public static BattleDefinition InitializeParty(BattleDefinition scenario, IReadOnlyList<BattleCombatantDefinition> currentParty, BattleSession? currentBattle = null)
    {
        Dictionary<string, BattleCombatant>? current = currentBattle?.Combatants.Where(actor => actor.IsPlayer).ToDictionary(actor => actor.Id);
        return BattleSession.CloneDefinition(scenario with { Combatants = [.. currentParty.Select(actor => current is not null && current.TryGetValue(actor.Id, out BattleCombatant? live) ? actor with { CurrentHp = live.CurrentHp, MagicStocks = live.MagicStocks.ToArray() } : actor), .. scenario.Combatants.Where(actor => !actor.IsPlayer)] });
    }

    public UiView BuildView()
    {
        return view;
    }

    public void Navigate(MenuInput input)
    {
        if (input == MenuInput.Cancel)
        {
            Close();
        }
        else if (input is MenuInput.Up or MenuInput.Down or MenuInput.PageUp or MenuInput.PageDown)
        {
            int step = input is MenuInput.PageUp or MenuInput.PageDown ? 12 : 1;
            Select(SelectedIndex + (input is MenuInput.Up or MenuInput.PageUp ? -step : step), false);
        }
        else if (input is MenuInput.Left or MenuInput.Right or MenuInput.Confirm)
        {
            Adjust(input == MenuInput.Left ? -1 : 1, input == MenuInput.Confirm);
        }
    }

    public void Select(int index, bool adjust)
    {
        int next = Math.Clamp(index, 0, rows.Count - 1);
        if (SelectedIndex != next)
        {
            SoundRequested?.Invoke(MenuSound.Move);
        }
        SelectedIndex = next;
        if (adjust)
        {
            Adjust(1);
        }
    }

    public void Start()
    {
        try
        {
            start(Definition, Mode);
            SoundRequested?.Invoke(MenuSound.Confirm);
        }
        catch (Exception error) when (error is NotSupportedException or InvalidDataException or ArgumentException or IOException)
        {
            Message = "Cannot start scenario: " + error.Message;
            Console.Error.WriteLine(error);
        }
    }

    private void Close()
    {
        SoundRequested?.Invoke(MenuSound.Cancel);
        close();
    }

    private void Adjust(int direction, bool toggleDetails = true)
    {
        toggleExpansion = toggleDetails;
        BattleDebugRow selected = rows[SelectedIndex];
        try
        {
            selected.Adjust(direction);
            if (selected.Label is not ("Return" or "Start scenario"))
            {
                SoundRequested?.Invoke(MenuSound.Confirm);
            }
        }
        catch (Exception error) when (error is NotSupportedException or InvalidDataException or ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Message = "Cannot apply change: " + error.Message;
            Console.Error.WriteLine(error);
        }
        RebuildRows();
        SelectedIndex = Math.Clamp(SelectedIndex, 0, rows.Count - 1);
    }

    private void RebuildRows()
    {
        rows.Clear();
        rows.Add(new("Start scenario", "Start", "Create a fresh battle from this scenario. Existing research state is preserved.", _ => Start()));
        rows.Add(new("Preset slot", $"{presetSlot + 1} · {(presets.Read(presetSlot) is null ? "Empty" : "Saved")}", "Choose one of three local scenario presets. Save and Load below are explicit actions.", direction => presetSlot = (presetSlot + direction + 3) % 3));
        rows.Add(new("Save preset", $"Slot {presetSlot + 1}", "Store this party, enemies, stage, statistics and ATB mode. Replaces only the selected local preset.", _ =>
        {
            presets.Save(presetSlot, Definition, Mode);
            Message = $"Preset {presetSlot + 1} saved.";
        }));
        rows.Add(new("Load preset", $"Slot {presetSlot + 1}", "Restore the selected local preset into this editor. The running battle and game save are unchanged.", _ =>
        {
            BattleDebugPreset? preset = presets.Read(presetSlot);
            if (preset is null)
            {
                Message = $"Preset {presetSlot + 1} is empty.";
                return;
            }
            ReplaceScenario(preset.Definition);
            Mode = preset.Mode;
            Message = $"Preset {presetSlot + 1} loaded.";
        }));
        rows.Add(new("Current party + last enemy", "Reset", "Restore the current-party default captured when this editor opened. With no encounter history, the opponent is Dummy.", _ =>
        {
            ReplaceScenario(defaults);
            Message = "Current party and last enemy restored.";
        }));
        rows.Add(new("ATB mode", Mode.ToString(), "Normal runs ATB continuously. TurnBased holds idle gauges until Advance Turn; actions still animate.", _ =>
        {
            Mode = Mode == BattleAtbMode.Normal ? BattleAtbMode.TurnBased : BattleAtbMode.Normal;
        }));
        rows.Add(new("Battle stage", stageId?.ToString() ?? "None", "Cycle installed battle stage identities.", direction =>
        {
            if (catalog.StageIds.Length == 0)
            {
                Message = "No alternate installed stages in the catalog.";
                return;
            }
            int current = Array.IndexOf(catalog.StageIds, stageId ?? -1);
            stageId = catalog.StageIds[(Math.Max(0, current) + direction + catalog.StageIds.Length) % catalog.StageIds.Length];
        }));
        AddCount("Party members", party, catalog.Players, true, 3);
        AddCount("Enemies", enemies, catalog.Enemies, false, 8);
        for (int index = 0; index < party.Count; index++)
        {
            AddActor(party, index, catalog.Players, "Party");
        }
        for (int index = 0; index < enemies.Count; index++)
        {
            AddActor(enemies, index, catalog.Enemies, "Enemy");
        }
        rows.Add(new("Return", "Back", "Close the scenario builder without starting a new battle.", _ => Close()));
    }

    private void ReplaceScenario(BattleDefinition definition)
    {
        initial = BattleSession.CloneDefinition(definition);
        stageId = initial.StageId;
        party.Clear();
        party.AddRange(initial.Combatants.Where(actor => actor.IsPlayer));
        enemies.Clear();
        enemies.AddRange(initial.Combatants.Where(actor => !actor.IsPlayer));
        expanded.Clear();
    }

    private void AddCount(string label, List<BattleCombatantDefinition> actors, BattleCombatantDefinition[] choices, bool player, int maximum)
    {
        rows.Add(new(label, actors.Count.ToString(), $"Choose one to {maximum} actors. Catalog choices come from the installed game assets.", direction =>
        {
            if (direction < 0 && actors.Count > 1)
            {
                expanded.Remove($"{(player ? "Party" : "Enemy")} {actors.Count}");
                actors.RemoveAt(actors.Count - 1);
            }
            else if (direction > 0 && actors.Count < maximum)
            {
                BattleCombatantDefinition? prototype = choices.ElementAtOrDefault(actors.Count % Math.Max(1, choices.Length)) ?? actors.FirstOrDefault();
                if (prototype is null)
                {
                    Message = "No installed model choices are available for this side.";
                    return;
                }
                actors.Add(prototype with { IsPlayer = player });
            }
        }));
    }

    private void AddActor(List<BattleCombatantDefinition> actors, int index, BattleCombatantDefinition[] choices, string side)
    {
        BattleCombatantDefinition actor = actors[index];
        string key = $"{side} {index + 1}";
        bool isExpanded = expanded.Contains(key);
        rows.Add(new(key, $"{(isExpanded ? "−" : "+")} {actor.Name}", $"{actor.Name} · HP {actor.CurrentHp}/{actor.MaximumHp}. Confirm toggles details; Right expands and Left collapses. Edit the model, statistics, equipment and effects inside.", direction =>
        {
            if (direction < 0 || toggleExpansion && expanded.Contains(key))
            {
                expanded.Remove(key);
            }
            else
            {
                expanded.Add(key);
            }
        }));
        if (!isExpanded)
        {
            return;
        }
        string prefix = $"{side} {index + 1} · ";
        rows.Add(new(prefix + "Character / model", actor.Name, actor.IsPlayer ? "Cycle installed model choices. The selected prototype supplies explicit scenario statistics." : "Cycle installed enemy models. Enemy weapons belong to the model. The selected prototype supplies explicit scenario statistics.", direction =>
        {
            if (choices.Length == 0)
            {
                Message = "No alternate installed models in the catalog.";
                return;
            }
            int current = Array.FindIndex(choices, choice => choice.ModelId == actor.ModelId);
            int next = current < 0 ? direction > 0 ? 0 : choices.Length - 1 : (current + direction + choices.Length) % choices.Length;
            actors[index] = choices[next] with { IsPlayer = actor.IsPlayer };
        }));
        if (actor.IsPlayer)
        {
            rows.Add(new(prefix + "Costume", actor.Costume.ToString(), "Cycle installed costume indices for this character. These indices identify asset choices, without inferring native save mappings.", direction =>
            {
                if (!catalog.PlayerCostumes.TryGetValue(actor.ModelId, out int[]? costumes) || costumes.Length == 0)
                {
                    Message = "No alternate installed costumes in the catalog.";
                    return;
                }
                int current = Array.IndexOf(costumes, actor.Costume);
                actors[index] = actor with { Costume = costumes[(Math.Max(0, current) + direction + costumes.Length) % costumes.Length] };
            }));
        }
        AddNumber(prefix + "Maximum HP", actor.MaximumHp, 10, 9999, 10, value => actors[index] = actors[index] with { MaximumHp = value, CurrentHp = Math.Min(actors[index].CurrentHp, value) });
        AddNumber(prefix + "Current HP", actor.CurrentHp, 1, actor.MaximumHp, 10, value => actors[index] = actors[index] with { CurrentHp = value });
        AddNumber(prefix + "Speed", actor.Speed, 1, 255, 5, value => actors[index] = actors[index] with { Speed = value });
        AddNumber(prefix + "Attack", actor.Attack, 1, 255, 5, value => actors[index] = actors[index] with { Attack = value });
        AddNumber(prefix + "Defense", actor.Defense, 0, 255, 5, value => actors[index] = actors[index] with { Defense = value });
        AddNumber(prefix + "Initial ATB", actor.InitialAtb, 0, BattleSession.AtbMaximum, 1000, value => actors[index] = actors[index] with { InitialAtb = value });
        if (actor.IsPlayer)
        {
            BattleDebugEquipment[] weapons = catalog.Weapons.Where(weapon => weapon.CharacterId is null || weapon.CharacterId == actor.ModelId).ToArray();
            rows.Add(new(prefix + "Weapon", actor.WeaponName ?? actor.WeaponId?.ToString() ?? "None", "Choose a weapon for this character. Adjust Attack explicitly for the scenario; native equipment stat formulas are unproved.", direction =>
            {
                int current = actor.WeaponId.HasValue ? Array.FindIndex(weapons, weapon => weapon.Id == actor.WeaponId.Value) + 1 : 0;
                int next = (current + direction + weapons.Length + 1) % (weapons.Length + 1);
                BattleDebugEquipment? weapon = next == 0 ? null : weapons[next - 1];
                actors[index] = actor with { WeaponId = weapon?.Id, WeaponName = weapon?.Name };
            }));
        }
        rows.Add(new(prefix + "Equipment", actor.EquipmentIds.Length == 0 ? "None" : string.Join(", ", actor.EquipmentIds.Select(id => catalog.Equipment.FirstOrDefault(item => item.Id == id)?.Name ?? id.ToString())), "Select scenario equipment metadata. Adjust battle statistics explicitly above.", direction =>
        {
            int current = actor.EquipmentIds.Length == 0 ? 0 : Array.FindIndex(catalog.Equipment, item => item.Id == actor.EquipmentIds[0]) + 1;
            int next = (current + direction + catalog.Equipment.Length + 1) % (catalog.Equipment.Length + 1);
            actors[index] = actor with { EquipmentIds = next == 0 ? [] : [catalog.Equipment[next - 1].Id] };
        }));
        foreach (BattleAbility ability in Enum.GetValues<BattleAbility>())
        {
            BattleAbility selected = ability;
            bool enabled = actor.Abilities.Contains(ability);
            rows.Add(new(prefix + "Ability: " + ability, enabled ? "On" : "Off", ability == BattleAbility.Attack ? "Attack is required by this battle dispatcher." : "Toggle this explicit scenario command. Magic and Item require owned stock; GF requires junction and availability.", _ =>
            {
                if (selected != BattleAbility.Attack)
                {
                    actors[index] = actor with { Abilities = enabled ? actor.Abilities.Where(item => item != selected).ToArray() : [.. actor.Abilities, selected] };
                }
            }));
        }
        foreach (BattleStatusKind kind in new[] { BattleStatusKind.Poison, BattleStatusKind.Regen, BattleStatusKind.Haste, BattleStatusKind.Slow })
        {
            BattleStatusKind selected = kind;
            bool enabled = actor.StatusEffects.Any(effect => effect.Kind == kind);
            rows.Add(new(prefix + kind, enabled ? "On" : "Off", "Toggle the implemented scenario status hook. Duration and periodic parameters appear below when enabled.", _ =>
            {
                actors[index] = actor with { StatusEffects = enabled ? actor.StatusEffects.Where(effect => effect.Kind != selected).ToArray() : [.. actor.StatusEffects, new BattleStatusEffectDefinition { Kind = selected }] };
            }));
            if (enabled)
            {
                BattleStatusEffectDefinition effect = actor.StatusEffects.Single(effect => effect.Kind == kind);
                AddNumber(prefix + kind + " duration", effect.DurationFrames, 1, 18_000, 30, value => SetEffect(actors, index, effect with { DurationFrames = value }));
                if (kind is BattleStatusKind.Poison or BattleStatusKind.Regen)
                {
                    AddNumber(prefix + kind + " interval", effect.TickIntervalFrames, 1, 900, 5, value => SetEffect(actors, index, effect with { TickIntervalFrames = value }));
                    AddNumber(prefix + kind + " amount", effect.Magnitude, 0, 9999, 5, value => SetEffect(actors, index, effect with { Magnitude = value }));
                }
            }
        }
    }

    private void AddNumber(string label, int value, int min, int max, int step, Action<int> apply)
    {
        rows.Add(new(label, value.ToString(), "Left / Right changes the explicit debug scenario value.", direction => apply(Math.Clamp(value + direction * step, min, max))));
    }

    private static void SetEffect(List<BattleCombatantDefinition> actors, int index, BattleStatusEffectDefinition changed)
    {
        actors[index] = actors[index] with { StatusEffects = actors[index].StatusEffects.Select(effect => effect.Kind == changed.Kind ? changed : effect).ToArray() };
    }
}

internal sealed record BattleDebugRow(string Label, string Value, string Help, Action<int> Adjust);
