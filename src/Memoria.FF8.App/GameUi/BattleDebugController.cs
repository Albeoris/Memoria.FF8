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

/// <summary>A separate editable scenario. Starting it never patches the running battle or game save.</summary>
public sealed class BattleDebugController
{
    internal Func<string, string, string>? LocalizeText { get; set; }

    internal string SystemText(string id, string original) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Translate(LocalizeText, id, original);

    internal string FormatSystemText(string id, string original, params object?[] arguments) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(SystemText(id, original), original, arguments);

    private readonly BattleDebugCatalog catalog;
    private BattleDefinition initial;
    private readonly BattleDefinition defaults;
    private readonly BattleDebugPresets presets;
    private string panel = "Scenario";
    private string? actorKey;
    private Func<string>? displayActorKey;
    private string actorGroup = "Identity";
    private int participantSelection;
    private readonly Action<BattleDefinition, BattleAtbMode> start;
    private readonly Action close;
    private readonly List<BattleCombatantDefinition> party;
    private readonly List<BattleCombatantDefinition> enemies;
    private readonly UiView view;
    private readonly List<BattleDebugRow> rows = [];
    private int? stageId;
    private int presetSlot;


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
        view = new UiView(new BattleDebugElement(this)) { DesignWidth = 960, DesignHeight = 672, Cancel = () => Navigate(MenuInput.Cancel) };
        RebuildRows();
        view.Resize(960, 672);
    }

    public BattleAtbMode Mode { get; private set; }
    public event Action<MenuSound>? SoundRequested;
    public int SelectedIndex { get; private set; }
    private string? message;
    private Func<string>? messageText;
    public string Message
    {
        get => messageText?.Invoke() ?? message ?? SystemText("debug.message.initial", "Edit an isolated scenario. Current game settings are in Debug Room · Battle.");
        private set
        {
            messageText = null;
            message = value;
        }
    }

    private void SetSystemMessage(Func<string> text)
    {
        messageText = text;
    }
    internal IReadOnlyList<BattleDebugRow> Rows => rows.Select(row => row with { Label = row.ReadLabel?.Invoke() ?? row.Label, Value = row.ReadValue?.Invoke() ?? row.Value, Help = row.ReadHelp?.Invoke() ?? row.Help }).ToArray();
    internal string PageTitle => actorKey is null ? PanelLabel : FormatSystemText("debug.page.actor", "Participants · {0} · {1}", displayActorKey?.Invoke() ?? actorKey, ActorGroupLabel);
    private string PanelLabel => panel switch
    {
        "Scenario" => SystemText("debug.page.scenario", "Scenario"),
        "Participants" => SystemText("debug.page.participants", "Participants"),
        "Presets" => SystemText("debug.page.presets", "Presets"),
        _ => panel
    };

    private string ActorGroupLabel => actorGroup switch
    {
        "Identity" => SystemText("debug.group.identity", "Identity"),
        "Vitals and stats" => SystemText("debug.group.vitals", "Vitals and stats"),
        "Equipment and abilities" => SystemText("debug.group.equipment", "Equipment and abilities"),
        "Status effects" => SystemText("debug.group.status", "Status effects"),
        _ => actorGroup
    };

    private string AbilityLabel(BattleAbility ability) => ability switch
    {
        BattleAbility.Attack => SystemText("debug.command.attack", "Attack"),
        BattleAbility.Magic => SystemText("debug.command.magic", "Magic"),
        BattleAbility.Draw => SystemText("debug.command.draw", "Draw"),
        BattleAbility.GuardianForce => SystemText("debug.command.guardian", "GF"),
        BattleAbility.Item => SystemText("debug.command.item", "Item"),
        BattleAbility.EnemyAbility => SystemText("debug.command.enemy", "Enemy ability"),
        BattleAbility.NativeCommand => SystemText("debug.command.native", "Native command"),
        BattleAbility.LimitBreak => SystemText("debug.command.limit", "Limit Break"),
        _ => ability.ToString()
    };

    private string StatusLabel(BattleStatusKind status) => status switch
    {
        BattleStatusKind.Poison => SystemText("debug.status.poison", "Poison"),
        BattleStatusKind.Regen => SystemText("debug.status.regen", "Regen"),
        BattleStatusKind.Haste => SystemText("debug.status.haste", "Haste"),
        BattleStatusKind.Slow => SystemText("debug.status.slow", "Slow"),
        _ => status.ToString()
    };

    private string DisplayActorKey(bool player, int index) => FormatSystemText("debug.actor.key", "{0} {1}", player ? SystemText("debug.actor.party", "Party") : SystemText("debug.actor.enemy", "Enemy"), index + 1);

    private string DisplayActorPrefix(bool player, int index) => FormatSystemText("debug.actor.prefix", "{0} · ", DisplayActorKey(player, index));

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
            if (actorKey is not null)
            {
                actorKey = null;
                RebuildRows();
                SelectedIndex = Math.Clamp(participantSelection, 0, rows.Count - 1);
            }
            else
            {
                Close();
            }
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
            SetSystemMessage(() => SystemText("debug.message.start.cannot.start.scenario", "Cannot start scenario: ") + error.Message);
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
        BattleDebugRow selected = rows[SelectedIndex];
        if (!toggleDetails && selected.Label is "Start scenario" or "Save preset" or "Load preset" or "Current party + last enemy" or "Return")
        {
            return;
        }
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
            SetSystemMessage(() => SystemText("debug.message.adjust.cannot.apply.change", "Cannot apply change: ") + error.Message);
            Console.Error.WriteLine(error);
        }
        RebuildRows();
        SelectedIndex = Math.Clamp(SelectedIndex, 0, rows.Count - 1);
    }

    private void RebuildRows()
    {
        rows.Clear();
        rows.Add(new("Start scenario", "Start", "Create a fresh battle from this scenario. Existing game state is preserved.", _ => Start()) { ReadLabel = () => SystemText("debug.rebuildrows.label.start.scenario", "Start scenario"), ReadValue = () => SystemText("debug.rebuildrows.value.start", "Start"), ReadHelp = () => SystemText("debug.rebuildrows.help.create.a.fresh.battle.from.this.scenario.existing", "Create a fresh battle from this scenario. Existing game state is preserved.") });
        rows.Add(new("Editor panel", panel, "Choose Scenario, Participants or Presets. All edits belong only to this laboratory scenario.", direction =>
        {
            string[] panels = ["Scenario", "Participants", "Presets"];
            panel = panels[(Array.IndexOf(panels, panel) + direction + panels.Length) % panels.Length];
            actorKey = null;
        }) { ReadLabel = () => SystemText("debug.rebuildrows.label.editor.panel", "Editor panel"), ReadValue = () => PanelLabel, ReadHelp = () => SystemText("debug.rebuildrows.help.choose.scenario.participants.or.presets.all.edits.belong", "Choose Scenario, Participants or Presets. All edits belong only to this laboratory scenario.") });
        if (panel == "Presets")
        {
            rows.Add(new("Preset slot", $"{presetSlot + 1} · {(presets.Read(presetSlot) is null ? "Empty" : "Saved")}", "Choose one of three local scenario presets. Save and Load below are explicit actions.", direction => presetSlot = (presetSlot + direction + 3) % 3) { ReadLabel = () => SystemText("debug.rebuildrows.label.preset.slot", "Preset slot"), ReadValue = () => FormatSystemText("debug.preset.summary", "{0} · {1}", presetSlot + 1, presets.Read(presetSlot) is null ? SystemText("debug.preset.empty", "Empty") : SystemText("debug.preset.saved", "Saved")), ReadHelp = () => SystemText("debug.rebuildrows.help.choose.one.of.three.local.scenario.presets.save", "Choose one of three local scenario presets. Save and Load below are explicit actions.") });
            rows.Add(new("Save preset", $"Slot {presetSlot + 1}", "Store this party, enemies, stage, statistics and ATB mode. Replaces only the selected local preset.", _ =>
            {
                presets.Save(presetSlot, Definition, Mode);
                SetSystemMessage(() => FormatSystemText("debug.message.rebuildrows.preset.saved", "Preset {0} saved.", presetSlot + 1));
            }) { ReadLabel = () => SystemText("debug.rebuildrows.label.save.preset", "Save preset"), ReadValue = () => FormatSystemText("debug.rebuildrows.value.slot.0", "Slot {0}", presetSlot + 1), ReadHelp = () => SystemText("debug.rebuildrows.help.store.this.party.enemies.stage.statistics.and.atb", "Store this party, enemies, stage, statistics and ATB mode. Replaces only the selected local preset.") });
            rows.Add(new("Load preset", $"Slot {presetSlot + 1}", "Restore the selected local preset into this editor. The running battle and game save are unchanged.", _ =>
            {
                BattleDebugPreset? preset = presets.Read(presetSlot);
                if (preset is null)
                {
                    SetSystemMessage(() => FormatSystemText("debug.message.rebuildrows.preset.is.empty", "Preset {0} is empty.", presetSlot + 1));
                    return;
                }
                ReplaceScenario(preset.Definition);
                Mode = preset.Mode;
                SetSystemMessage(() => FormatSystemText("debug.message.rebuildrows.preset.loaded", "Preset {0} loaded.", presetSlot + 1));
            }) { ReadLabel = () => SystemText("debug.rebuildrows.label.load.preset", "Load preset"), ReadValue = () => FormatSystemText("debug.rebuildrows.value.slot.0", "Slot {0}", presetSlot + 1), ReadHelp = () => SystemText("debug.rebuildrows.help.restore.the.selected.local.preset.into.this.editor", "Restore the selected local preset into this editor. The running battle and game save are unchanged.") });
        }
        if (panel == "Scenario")
        {
            rows.Add(new("Current party + last enemy", "Reset", "Restore the current-party default captured when this editor opened. With no encounter history, the opponent is Dummy.", _ =>
            {
                ReplaceScenario(defaults);
                SetSystemMessage(() => SystemText("debug.message.rebuildrows.current.party.and.last.enemy.restored", "Current party and last enemy restored."));
            }) { ReadLabel = () => SystemText("debug.rebuildrows.label.current.party.last.enemy", "Current party \u002B last enemy"), ReadValue = () => SystemText("debug.rebuildrows.value.reset", "Reset"), ReadHelp = () => SystemText("debug.rebuildrows.help.restore.the.current.party.default.captured.when.this", "Restore the current-party default captured when this editor opened. With no encounter history, the opponent is Dummy.") });
            rows.Add(new("Scenario ATB mode", Mode == BattleAtbMode.Normal ? "Classic" : "Turn Based", "Scenario only. Classic runs ATB continuously. Turn Based holds idle gauges until Advance Turn; actions still animate.", _ =>
            {
                Mode = Mode == BattleAtbMode.Normal ? BattleAtbMode.TurnBased : BattleAtbMode.Normal;
            }) { ReadLabel = () => SystemText("debug.rebuildrows.label.scenario.atb.mode", "Scenario ATB mode"), ReadValue = () => Mode == BattleAtbMode.Normal ? SystemText("debug.rebuildrows.value.classic", "Classic") : SystemText("debug.rebuildrows.value.turn.based", "Turn Based"), ReadHelp = () => SystemText("debug.rebuildrows.help.scenario.only.classic.runs.atb.continuously.turn.based", "Scenario only. Classic runs ATB continuously. Turn Based holds idle gauges until Advance Turn; actions still animate.") });
            rows.Add(new("Battle stage", stageId?.ToString() ?? "None", "Cycle installed battle stage identities.", direction =>
            {
                if (catalog.StageIds.Length == 0)
                {
                    SetSystemMessage(() => SystemText("debug.message.rebuildrows.no.alternate.installed.stages.in.the.catalog", "No alternate installed stages in the catalog."));
                    return;
                }
                int current = Array.IndexOf(catalog.StageIds, stageId ?? -1);
                stageId = catalog.StageIds[(Math.Max(0, current) + direction + catalog.StageIds.Length) % catalog.StageIds.Length];
            }) { ReadLabel = () => SystemText("debug.rebuildrows.label.battle.stage", "Battle stage"), ReadValue = () => stageId?.ToString() ?? "None", ReadHelp = () => SystemText("debug.rebuildrows.help.cycle.installed.battle.stage.identities", "Cycle installed battle stage identities.") });
            AddCount("Party members", party, catalog.Players, true, 3, readLabel: () => SystemText("debug.actor.label.party.members", "Party members"));
            AddCount("Enemies", enemies, catalog.Enemies, false, 8, readLabel: () => SystemText("debug.actor.label.enemies", "Enemies"));
        }
        if (panel == "Participants")
        {
            for (int index = 0; index < party.Count; index++)
            {
                AddActor(party, index, catalog.Players, "Party");
            }
            for (int index = 0; index < enemies.Count; index++)
            {
                AddActor(enemies, index, catalog.Enemies, "Enemy");
            }
        }
        rows.Add(new("Return", "Back", actorKey is null ? "Close the scenario builder without starting a new battle." : "Return to the participants list.", _ =>
        {
            if (actorKey is null)
            {
                Close();
            }
            else
            {
                actorKey = null;
                SelectedIndex = participantSelection;
            }
        }) { ReadLabel = () => SystemText("debug.rebuildrows.label.return", "Return"), ReadValue = () => SystemText("debug.rebuildrows.value.back", "Back"), ReadHelp = () => actorKey is null ? SystemText("debug.rebuildrows.help.close.the.scenario.builder.without.starting.a.new", "Close the scenario builder without starting a new battle.") : SystemText("debug.rebuildrows.help.return.to.the.participants.list", "Return to the participants list.") });
    }

    private void ReplaceScenario(BattleDefinition definition)
    {
        initial = BattleSession.CloneDefinition(definition);
        stageId = initial.StageId;
        party.Clear();
        party.AddRange(initial.Combatants.Where(actor => actor.IsPlayer));
        enemies.Clear();
        enemies.AddRange(initial.Combatants.Where(actor => !actor.IsPlayer));
        actorKey = null;
    }

    private void AddCount(string label, List<BattleCombatantDefinition> actors, BattleCombatantDefinition[] choices, bool player, int maximum, Func<string>? readLabel = null)
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
                    SetSystemMessage(() => SystemText("debug.message.addcount.no.installed.model.choices.are.available.for", "No installed model choices are available for this side."));
                    return;
                }
                actors.Add(prototype with { IsPlayer = player });
            }
        }) { ReadLabel = () => readLabel?.Invoke() ?? label, ReadValue = () => actors.Count.ToString(), ReadHelp = () => FormatSystemText("debug.addcount.help.choose.one.to.0.actors.catalog.choices.come", "Choose one to {0} actors. Catalog choices come from the installed game assets.", maximum) });
    }

    private void AddActor(List<BattleCombatantDefinition> actors, int index, BattleCombatantDefinition[] choices, string side)
    {
        BattleCombatantDefinition actor = actors[index];
        string key = $"{side} {index + 1}";
        if (actorKey is null)
        {
            rows.Add(new(key, actor.Name, $"{actor.Name} · HP {actor.CurrentHp}/{actor.MaximumHp}. Open Identity, Vitals and stats, Equipment and abilities, or Status effects.", _ =>
            {
                participantSelection = SelectedIndex;
                actorKey = key;
                displayActorKey = () => DisplayActorKey(actor.IsPlayer, index);
                actorGroup = "Identity";
            }) { ReadLabel = () => DisplayActorKey(actor.IsPlayer, index), ReadValue = () => actor.Name, ReadHelp = () => FormatSystemText("debug.addactor.help.0.hp.1.2.open.identity.vitals.and", "{0} \u00B7 HP {1}/{2}. Open Identity, Vitals and stats, Equipment and abilities, or Status effects.", actor.Name, actor.CurrentHp, actor.MaximumHp) });
            return;
        }
        if (actorKey != key)
        {
            return;
        }
        rows.Add(new("Actor details", actorGroup, "Choose a group for this actor. Back returns to the compact participants list.", direction =>
        {
            string[] groups = ["Identity", "Vitals and stats", "Equipment and abilities", "Status effects"];
            actorGroup = groups[(Array.IndexOf(groups, actorGroup) + direction + groups.Length) % groups.Length];
        }) { ReadLabel = () => SystemText("debug.addactor.label.actor.details", "Actor details"), ReadValue = () => ActorGroupLabel, ReadHelp = () => SystemText("debug.addactor.help.choose.a.group.for.this.actor.back.returns", "Choose a group for this actor. Back returns to the compact participants list.") });
        int firstDetail = rows.Count;
        string prefix = $"{side} {index + 1} · ";
        rows.Add(new(prefix + "Character / model", actor.Name, actor.IsPlayer ? "Cycle installed model choices. The selected prototype supplies explicit scenario statistics." : "Cycle installed enemy models. Enemy weapons belong to the model. The selected prototype supplies explicit scenario statistics.", direction =>
        {
            if (choices.Length == 0)
            {
                SetSystemMessage(() => SystemText("debug.message.addactor.no.alternate.installed.models.in.the.catalog", "No alternate installed models in the catalog."));
                return;
            }
            int current = Array.FindIndex(choices, choice => choice.ModelId == actor.ModelId);
            int next = current < 0 ? direction > 0 ? 0 : choices.Length - 1 : (current + direction + choices.Length) % choices.Length;
            actors[index] = choices[next] with { IsPlayer = actor.IsPlayer };
        }) { ReadLabel = () => FormatSystemText("debug.addactor.label.0.character.model", "{0}Character / model", DisplayActorPrefix(actor.IsPlayer, index)), ReadValue = () => actor.Name, ReadHelp = () => actor.IsPlayer ? SystemText("debug.addactor.help.cycle.installed.model.choices.the.selected.prototype.supplies", "Cycle installed model choices. The selected prototype supplies explicit scenario statistics.") : SystemText("debug.addactor.help.cycle.installed.enemy.models.enemy.weapons.belong.to", "Cycle installed enemy models. Enemy weapons belong to the model. The selected prototype supplies explicit scenario statistics.") });
        if (actor.IsPlayer)
        {
            rows.Add(new(prefix + "Costume", actor.Costume.ToString(), "Cycle installed costume indices for this character. These indices identify asset choices, without inferring native save mappings.", direction =>
            {
                if (!catalog.PlayerCostumes.TryGetValue(actor.ModelId, out int[]? costumes) || costumes.Length == 0)
                {
                    SetSystemMessage(() => SystemText("debug.message.addactor.no.alternate.installed.costumes.in.the.catalog", "No alternate installed costumes in the catalog."));
                    return;
                }
                int current = Array.IndexOf(costumes, actor.Costume);
                actors[index] = actor with { Costume = costumes[(Math.Max(0, current) + direction + costumes.Length) % costumes.Length] };
            }) { ReadLabel = () => FormatSystemText("debug.addactor.label.0.costume", "{0}Costume", DisplayActorPrefix(actor.IsPlayer, index)), ReadValue = () => actor.Costume.ToString(), ReadHelp = () => SystemText("debug.addactor.help.cycle.installed.costume.indices.for.this.character.these", "Cycle installed costume indices for this character. These indices identify asset choices, without inferring native save mappings.") });
        }
        AddNumber(prefix + "Maximum HP", actor.MaximumHp, 10, 9999, 10, value => actors[index] = actors[index] with { MaximumHp = value, CurrentHp = Math.Min(actors[index].CurrentHp, value) }, readLabel: () => FormatSystemText("debug.actor.label.0.maximum.hp", "{0}Maximum HP", DisplayActorPrefix(actor.IsPlayer, index)));
        AddNumber(prefix + "Current HP", actor.CurrentHp, 1, actor.MaximumHp, 10, value => actors[index] = actors[index] with { CurrentHp = value }, readLabel: () => FormatSystemText("debug.actor.label.0.current.hp", "{0}Current HP", DisplayActorPrefix(actor.IsPlayer, index)));
        AddNumber(prefix + "Speed", actor.Speed, 1, 255, 5, value => actors[index] = actors[index] with { Speed = value }, readLabel: () => FormatSystemText("debug.actor.label.0.speed", "{0}Speed", DisplayActorPrefix(actor.IsPlayer, index)));
        AddNumber(prefix + "Attack", actor.Attack, 1, 255, 5, value => actors[index] = actors[index] with { Attack = value }, readLabel: () => FormatSystemText("debug.actor.label.0.attack", "{0}Attack", DisplayActorPrefix(actor.IsPlayer, index)));
        AddNumber(prefix + "Defense", actor.Defense, 0, 255, 5, value => actors[index] = actors[index] with { Defense = value }, readLabel: () => FormatSystemText("debug.actor.label.0.defense", "{0}Defense", DisplayActorPrefix(actor.IsPlayer, index)));
        AddNumber(prefix + "Initial ATB", actor.InitialAtb, 0, BattleSession.AtbMaximum, 1000, value => actors[index] = actors[index] with { InitialAtb = value }, readLabel: () => FormatSystemText("debug.actor.label.0.initial.atb", "{0}Initial ATB", DisplayActorPrefix(actor.IsPlayer, index)));
        if (actor.IsPlayer)
        {
            BattleDebugEquipment[] weapons = catalog.Weapons.Where(weapon => weapon.CharacterId is null || weapon.CharacterId == actor.ModelId).ToArray();
            rows.Add(new(prefix + "Weapon", actor.WeaponName ?? actor.WeaponId?.ToString() ?? "None", "Choose a weapon for this character. Adjust Attack explicitly for the scenario; native equipment stat formulas are unproved.", direction =>
            {
                int current = actor.WeaponId.HasValue ? Array.FindIndex(weapons, weapon => weapon.Id == actor.WeaponId.Value) + 1 : 0;
                int next = (current + direction + weapons.Length + 1) % (weapons.Length + 1);
                BattleDebugEquipment? weapon = next == 0 ? null : weapons[next - 1];
                actors[index] = actor with { WeaponId = weapon?.Id, WeaponName = weapon?.Name };
            }) { ReadLabel = () => FormatSystemText("debug.addactor.label.0.weapon", "{0}Weapon", DisplayActorPrefix(actor.IsPlayer, index)), ReadValue = () => actor.WeaponName ?? actor.WeaponId?.ToString() ?? "None", ReadHelp = () => SystemText("debug.addactor.help.choose.a.weapon.for.this.character.adjust.attack", "Choose a weapon for this character. Adjust Attack explicitly for the scenario; native equipment stat formulas are unproved.") });
        }
        rows.Add(new(prefix + "Equipment", actor.EquipmentIds.Length == 0 ? "None" : string.Join(", ", actor.EquipmentIds.Select(id => catalog.Equipment.FirstOrDefault(item => item.Id == id)?.Name ?? id.ToString())), "Select scenario equipment metadata. Adjust battle statistics explicitly above.", direction =>
        {
            int current = actor.EquipmentIds.Length == 0 ? 0 : Array.FindIndex(catalog.Equipment, item => item.Id == actor.EquipmentIds[0]) + 1;
            int next = (current + direction + catalog.Equipment.Length + 1) % (catalog.Equipment.Length + 1);
            actors[index] = actor with { EquipmentIds = next == 0 ? [] : [catalog.Equipment[next - 1].Id] };
        }) { ReadLabel = () => FormatSystemText("debug.addactor.label.0.equipment", "{0}Equipment", DisplayActorPrefix(actor.IsPlayer, index)), ReadValue = () => actor.EquipmentIds.Length == 0 ? SystemText("debug.addactor.value.none", "None") : string.Join(", ", actor.EquipmentIds.Select(id => catalog.Equipment.FirstOrDefault(item => item.Id == id)?.Name ?? id.ToString())), ReadHelp = () => SystemText("debug.addactor.help.select.scenario.equipment.metadata.adjust.battle.statistics.explicitly", "Select scenario equipment metadata. Adjust battle statistics explicitly above.") });
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
            }) { ReadLabel = () => FormatSystemText("debug.addactor.label.0.ability.1", "{0}Ability: {1}", DisplayActorPrefix(actor.IsPlayer, index), AbilityLabel(ability)), ReadValue = () => enabled ? SystemText("debug.addactor.value.on", "On") : SystemText("debug.addactor.value.off", "Off"), ReadHelp = () => ability == BattleAbility.Attack ? SystemText("debug.addactor.help.attack.is.required.by.this.battle.dispatcher", "Attack is required by this battle dispatcher.") : SystemText("debug.addactor.help.toggle.this.explicit.scenario.command.magic.and.item", "Toggle this explicit scenario command. Magic and Item require owned stock; GF requires junction and availability.") });
        }
        foreach (BattleStatusKind kind in new[] { BattleStatusKind.Poison, BattleStatusKind.Regen, BattleStatusKind.Haste, BattleStatusKind.Slow })
        {
            BattleStatusKind selected = kind;
            bool enabled = actor.StatusEffects.Any(effect => effect.Kind == kind);
            rows.Add(new(prefix + kind, enabled ? "On" : "Off", "Toggle the implemented scenario status hook. Duration and periodic parameters appear below when enabled.", _ =>
            {
                actors[index] = actor with { StatusEffects = enabled ? actor.StatusEffects.Where(effect => effect.Kind != selected).ToArray() : [.. actor.StatusEffects, new BattleStatusEffectDefinition { Kind = selected }] };
            }) { ReadLabel = () => DisplayActorPrefix(actor.IsPlayer, index) + StatusLabel(kind), ReadValue = () => enabled ? SystemText("debug.addactor.value.on", "On") : SystemText("debug.addactor.value.off", "Off"), ReadHelp = () => SystemText("debug.addactor.help.toggle.the.implemented.scenario.status.hook.duration.and", "Toggle the implemented scenario status hook. Duration and periodic parameters appear below when enabled.") });
            if (enabled)
            {
                BattleStatusEffectDefinition effect = actor.StatusEffects.Single(effect => effect.Kind == kind);
                AddNumber(prefix + kind + " duration", effect.DurationFrames, 1, 18_000, 30, value => SetEffect(actors, index, effect with { DurationFrames = value }), readLabel: () => FormatSystemText("debug.actor.label.0.1.duration", "{0}{1} duration", DisplayActorPrefix(actor.IsPlayer, index), StatusLabel(kind)));
                if (kind is BattleStatusKind.Poison or BattleStatusKind.Regen)
                {
                    AddNumber(prefix + kind + " interval", effect.TickIntervalFrames, 1, 900, 5, value => SetEffect(actors, index, effect with { TickIntervalFrames = value }), readLabel: () => FormatSystemText("debug.actor.label.0.1.interval", "{0}{1} interval", DisplayActorPrefix(actor.IsPlayer, index), StatusLabel(kind)));
                    AddNumber(prefix + kind + " amount", effect.Magnitude, 0, 9999, 5, value => SetEffect(actors, index, effect with { Magnitude = value }), readLabel: () => FormatSystemText("debug.actor.label.0.1.amount", "{0}{1} amount", DisplayActorPrefix(actor.IsPlayer, index), StatusLabel(kind)));
                }
            }
        }
        BattleDebugRow[] details = rows.Skip(firstDetail).Where(row => InActorGroup(row.Label[prefix.Length..])).ToArray();
        rows.RemoveRange(firstDetail, rows.Count - firstDetail);
        rows.AddRange(details);

    }

    private bool InActorGroup(string label)
    {
        return actorGroup switch
        {
            "Identity" => label is "Character / model" or "Costume",
            "Vitals and stats" => label is "Maximum HP" or "Current HP" or "Speed" or "Attack" or "Defense" or "Initial ATB",
            "Equipment and abilities" => label is "Weapon" or "Equipment" || label.StartsWith("Ability:", StringComparison.Ordinal),
            _ => label.StartsWith("Poison", StringComparison.Ordinal) || label.StartsWith("Regen", StringComparison.Ordinal) || label.StartsWith("Haste", StringComparison.Ordinal) || label.StartsWith("Slow", StringComparison.Ordinal)
        };
    }

    private void AddNumber(string label, int value, int min, int max, int step, Action<int> apply, Func<string>? readLabel = null)
    {
        rows.Add(new(label, value.ToString(), "Adjust the explicit debug scenario value.", direction => apply(Math.Clamp(value + direction * step, min, max))) { ReadLabel = () => readLabel?.Invoke() ?? label, ReadValue = () => value.ToString(), ReadHelp = () => SystemText("debug.addnumber.help.adjust.the.explicit.debug.scenario.value", "Adjust the explicit debug scenario value.") });
    }

    private static void SetEffect(List<BattleCombatantDefinition> actors, int index, BattleStatusEffectDefinition changed)
    {
        actors[index] = actors[index] with { StatusEffects = actors[index].StatusEffects.Select(effect => effect.Kind == changed.Kind ? changed : effect).ToArray() };
    }
}

internal sealed record BattleDebugRow(string Label, string Value, string Help, Action<int> Adjust)
{
    internal Func<string>? ReadLabel { get; init; }
    internal Func<string>? ReadValue { get; init; }
    internal Func<string>? ReadHelp { get; init; }
}
