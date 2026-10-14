using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed record BattleDebugEquipment(int Id, string Name);

/// <summary>Choices supplied by the installed-asset host; no original combat statistics are inferred by the UI.</summary>
public sealed record BattleDebugCatalog
{
    public BattleCombatantDefinition[] Players { get; init; } = [];
    public BattleCombatantDefinition[] Enemies { get; init; } = [];
    public BattleDebugEquipment[] Weapons { get; init; } = [];
    public BattleDebugEquipment[] Equipment { get; init; } = [];
    public int[] StageIds { get; init; } = [];
}

/// <summary>A separate editable scenario. Starting it never patches the running battle or research save.</summary>
public sealed class BattleDebugController
{
    private readonly BattleDebugCatalog catalog;
    private readonly BattleDefinition initial;
    private readonly Action<BattleDefinition, BattleAtbMode> start;
    private readonly Action<BattleAtbMode, string, bool> settings;
    private readonly Action close;
    private readonly List<BattleCombatantDefinition> party;
    private readonly List<BattleCombatantDefinition> enemies;
    private readonly UiView view;
    private readonly List<BattleDebugRow> rows = [];
    private int? stageId;

    public BattleDebugController(BattleDefinition initial, BattleDebugCatalog catalog, BattleAtbMode mode, string policy, bool showInitiative, Action<BattleDefinition, BattleAtbMode> start, Action<BattleAtbMode, string, bool> settings, Action close)
    {
        this.catalog = catalog;
        this.initial = BattleSession.CloneDefinition(initial);
        stageId = initial.StageId;
        this.start = start;
        this.settings = settings;
        this.close = close;
        party = initial.Combatants.Where(actor => actor.IsPlayer).ToList();
        enemies = initial.Combatants.Where(actor => !actor.IsPlayer).ToList();
        Mode = mode;
        Policy = policy;
        ShowInitiative = showInitiative;
        view = new UiView(new BattleDebugElement(this)) { DesignWidth = 960, DesignHeight = 672, Cancel = close };
        RebuildRows();
        view.Resize(960, 672);
    }

    public BattleAtbMode Mode { get; private set; }
    public string Policy { get; private set; }
    public bool ShowInitiative { get; private set; }
    public int SelectedIndex { get; private set; }
    public string Message { get; private set; } = "Changes apply to a new debug scenario.";
    internal IReadOnlyList<BattleDebugRow> Rows => rows;
    public BattleDefinition Definition => initial with { Name = "Debug battle", StageId = stageId, Combatants = [.. party.Select((actor, index) => actor with { Id = $"party-{index}", IsPlayer = true }), .. enemies.Select((actor, index) => actor with { Id = $"enemy-{index}", IsPlayer = false })] };

    public UiView BuildView()
    {
        return view;
    }

    public void Navigate(MenuInput input)
    {
        if (input == MenuInput.Cancel)
        {
            close();
        }
        else if (input is MenuInput.Up or MenuInput.Down or MenuInput.PageUp or MenuInput.PageDown)
        {
            int step = input is MenuInput.PageUp or MenuInput.PageDown ? 12 : 1;
            SelectedIndex = Math.Clamp(SelectedIndex + (input is MenuInput.Up or MenuInput.PageUp ? -step : step), 0, rows.Count - 1);
        }
        else if (input is MenuInput.Left or MenuInput.Right or MenuInput.Confirm)
        {
            Adjust(input == MenuInput.Left ? -1 : 1);
        }
    }

    public void Select(int index, bool adjust)
    {
        SelectedIndex = Math.Clamp(index, 0, rows.Count - 1);
        if (adjust)
        {
            Adjust(1);
        }
    }

    public void Start()
    {
        start(Definition, Mode);
    }

    private void Adjust(int direction)
    {
        rows[SelectedIndex].Adjust(direction);
        RebuildRows();
        SelectedIndex = Math.Clamp(SelectedIndex, 0, rows.Count - 1);
    }

    private void RebuildRows()
    {
        rows.Clear();
        rows.Add(new("Start scenario", "Start", "Create a fresh battle from this scenario. Existing research state is preserved.", _ => Start()));
        rows.Add(new("ATB mode", Mode.ToString(), "Normal runs ATB continuously. TurnBased holds idle gauges until Advance Turn; actions still animate.", _ =>
        {
            Mode = Mode == BattleAtbMode.Normal ? BattleAtbMode.TurnBased : BattleAtbMode.Normal;
            settings(Mode, Policy, ShowInitiative);
        }));
        rows.Add(new("Next New Game policy", Policy, "NewGameFullCombat enables combat for the next New Game. ResearchInstantVictory preserves the research default.", _ =>
        {
            Policy = Policy == "ResearchInstantVictory" ? "NewGameFullCombat" : "ResearchInstantVictory";
            settings(Mode, Policy, ShowInitiative);
        }));
        rows.Add(new("Initiative bar", ShowInitiative ? "On" : "Off", "Display all living actors on the shared ATB rail.", _ =>
        {
            ShowInitiative = !ShowInitiative;
            settings(Mode, Policy, ShowInitiative);
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
        rows.Add(new("Return to battle", "Back", "Close the scenario builder without starting a new battle.", _ => close()));
    }

    private void AddCount(string label, List<BattleCombatantDefinition> actors, BattleCombatantDefinition[] choices, bool player, int maximum)
    {
        rows.Add(new(label, actors.Count.ToString(), $"Choose one to {maximum} actors. Catalog choices come from the installed game assets.", direction =>
        {
            if (direction < 0 && actors.Count > 1)
            {
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
        string prefix = $"{side} {index + 1} · ";
        rows.Add(new(prefix + "Character / model", actor.Name, "Cycle installed model choices. The selected prototype supplies explicit scenario statistics.", direction =>
        {
            if (choices.Length == 0)
            {
                Message = "No alternate installed models in the catalog.";
                return;
            }
            int current = Array.FindIndex(choices, choice => choice.ModelId == actor.ModelId);
            actors[index] = choices[(Math.Max(0, current) + direction + choices.Length) % choices.Length] with { IsPlayer = actor.IsPlayer };
        }));
        AddNumber(prefix + "Maximum HP", actor.MaximumHp, 10, 9999, 10, value => actors[index] = actors[index] with { MaximumHp = value, CurrentHp = Math.Min(actors[index].CurrentHp, value) });
        AddNumber(prefix + "Current HP", actor.CurrentHp, 1, actor.MaximumHp, 10, value => actors[index] = actors[index] with { CurrentHp = value });
        AddNumber(prefix + "Speed", actor.Speed, 1, 255, 5, value => actors[index] = actors[index] with { Speed = value });
        AddNumber(prefix + "Attack", actor.Attack, 1, 255, 5, value => actors[index] = actors[index] with { Attack = value });
        AddNumber(prefix + "Defense", actor.Defense, 0, 255, 5, value => actors[index] = actors[index] with { Defense = value });
        AddNumber(prefix + "Initial ATB", actor.InitialAtb, 0, BattleSession.AtbMaximum, 1000, value => actors[index] = actors[index] with { InitialAtb = value });
        rows.Add(new(prefix + "Weapon", actor.WeaponName ?? actor.WeaponId?.ToString() ?? "None", "Weapon identity and presentation. Adjust Attack explicitly for the scenario; native equipment stat formulas are unproved.", direction =>
        {
            int current = actor.WeaponId.HasValue ? Array.FindIndex(catalog.Weapons, weapon => weapon.Id == actor.WeaponId.Value) + 1 : 0;
            int next = (current + direction + catalog.Weapons.Length + 1) % (catalog.Weapons.Length + 1);
            BattleDebugEquipment? weapon = next == 0 ? null : catalog.Weapons[next - 1];
            actors[index] = actor with { WeaponId = weapon?.Id, WeaponName = weapon?.Name };
        }));
        rows.Add(new(prefix + "Equipment", actor.EquipmentIds.Length == 0 ? "None" : string.Join(", ", actor.EquipmentIds.Select(id => catalog.Equipment.FirstOrDefault(item => item.Id == id)?.Name ?? id.ToString())), "Select scenario equipment metadata. Adjust battle statistics explicitly above.", direction =>
        {
            int current = actor.EquipmentIds.Length == 0 ? 0 : Array.FindIndex(catalog.Equipment, item => item.Id == actor.EquipmentIds[0]) + 1;
            int next = (current + direction + catalog.Equipment.Length + 1) % (catalog.Equipment.Length + 1);
            actors[index] = actor with { EquipmentIds = next == 0 ? [] : [catalog.Equipment[next - 1].Id] };
        }));
        foreach (BattleAbility ability in Enum.GetValues<BattleAbility>())
        {
            BattleAbility selected = ability;
            rows.Add(new(prefix + "Ability: " + ability, ability == BattleAbility.Attack ? "On" : "Unsupported", ability == BattleAbility.Attack ? "Attack is required by this battle dispatcher." : "Ability extension hook. Only Attack has an executable command in this implementation.", _ =>
            {
                if (selected != BattleAbility.Attack)
                {
                    Message = $"{selected} has no implemented battle command.";
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
