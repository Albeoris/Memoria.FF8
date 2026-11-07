using System.Globalization;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>4BE100's original WORD program, presented through semantic menu targets and an exclusively owned tutorial party.</summary>
internal sealed partial class NativeTutorialPlayer
{
    // Tutorial presentation uses the menu clock, independently of the field simulation.
    internal const int CallbacksPerSecond = 60;
    private const int MenuActionCallbacks = CallbacksPerSecond * 3 / 10;
    private readonly NativeTutorialCatalog catalog;
    private readonly LiveMenuCatalog menuCatalog;
    private readonly NativeTutorialDefinition definition;
    private readonly IReadOnlyDictionary<int, NativeTutorialMenuStep> steps;
    private readonly IReadOnlySet<int> visibleRows;
    private NativeCharacterVitals party;
    private int pc;
    private long frame;
    private int delay;
    private int actionDelay;
    private int textId;
    private int centerX = 192;
    private int centerY = 104;
    private bool messageVisible;
    private bool inputPrepared;
    private bool complete;
    private readonly Dictionary<string, string> settings = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> names;
    private readonly string messageOccurrenceSession = Guid.NewGuid().ToString("N");
    private long messagePresentationId;
    private long observedPresentationId = -1;
    private int? observedTextId;
    private long messageOccurrenceSequence;

    public NativeTutorialPlayer(NativeTutorialCatalog catalog, LiveMenuCatalog menuCatalog, int tutorialId, JsonElement? saved = null, IReadOnlyDictionary<int, string>? names = null)
    {
        this.catalog = catalog;
        this.menuCatalog = menuCatalog;
        this.names = names is null ? [] : new Dictionary<int, string>(names);
        definition = catalog.Get(tutorialId);
        party = MapTutorialCommandAbilities(catalog.CreateIsolatedParty(tutorialId));
        visibleRows = party.CombatParty.Where(row => row != 255).Select(row => (int)row).Concat(Enumerable.Range(0, party.Records.Length).Where(row => (party.Records[row].MemberFlags & 1) != 0)).ToHashSet();
        steps = tutorialId <= 3 ? NativeTutorialJunctionPlan.Create(tutorialId, definition, party) : NativeTutorialOtherPlan.Create(tutorialId, definition, party);
        ValidatePlan();
        Menu = new MenuController(BuildData, ApplyMenuAction) { IsJunctionTutorial = tutorialId <= 3 };
        RegisterTutorialScreens();
        Menu.Open(definition.Descriptor.CallbackMode switch { 18 => "junction", 24 => "gf", 28 => "status", 29 => "switch", _ => throw new InvalidDataException("Unsupported native tutorial callback.") });
        Menu.Execute(new MenuAction("select-character", $"character-{definition.Descriptor.CharacterId}"));
        InitializeSettings();
        if (saved is JsonElement snapshot)
        {
            Restore(snapshot);
        }
    }

    public MenuController Menu { get; }

    private static NativeCharacterVitals MapTutorialCommandAbilities(NativeCharacterVitals original)
    {
        NativeCharacterVitals result = original.DeepCopy();
        for (int row = 0; row < result.Records.Length; row++)
        {
            NativeCharacterVital character = result.Records[row];
            uint commands = character.Junction.Commands;
            for (int slot = 0; slot < 3; slot++)
            {
                int command = (byte)(commands >> (8 * slot));
                if (command is > 0 and < 20)
                {
                    int[] matches = Enumerable.Range(0, original.MenuDefinitions.CommandAbilityCommands.Length).Where(index => original.MenuDefinitions.CommandAbilityCommands[index] == command).ToArray();
                    if (matches.Length != 1)
                    {
                        throw new NotSupportedException("The tutorial's battle command requires one unambiguous authenticated command ability mapping.");
                    }
                    commands = (commands & ~(255u << (8 * slot))) | ((uint)(matches[0] + 20) << (8 * slot));
                }
            }
            result.Records[row] = character with { Junction = character.Junction with { Commands = commands } };
        }
        return result;
    }
    public bool IsComplete => complete;
    public bool WaitingForInput => !complete && messageVisible && delay == 0 && definition.Inputs[pc] >> 12 == 8;
    public object Status => new { TutorialId = definition.Id, Frame = frame, ProgramCounter = pc, Delay = delay, ActionDelay = actionDelay, TextId = textId, MessageVisible = messageVisible, WaitingForInput, IsComplete, Menu.ScreenId, Menu.SelectedIndex, SelectedEntry = Menu.SelectedEntry?.Id };
    internal NativeCharacterVitals Party => party.DeepCopy();
    internal void RefreshPresentation() => UpdateMessage();
    internal event Action<int, ushort>? OperationExecuted;

    public bool Apply(PlayerAction action)
    {
        if (complete || action.Kind is not (PlayerActionKind.Wait or PlayerActionKind.Confirm or PlayerActionKind.Cancel))
        {
            return false;
        }
        if (action.Frames < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }
        for (int count = 0; count < action.Frames && !complete; count++)
        {
            Tick(count == 0 ? action.Kind : PlayerActionKind.Wait);
        }
        return true;
    }

    private void Tick(PlayerActionKind input)
    {
        if (!WaitingForInput)
        {
            input = PlayerActionKind.Wait;
        }
        frame++;
        if (delay > 0)
        {
            delay--;
            if (delay != 0)
            {
                return;
            }
            pc++;
        }
        for (int budget = 0; budget < definition.Inputs.Length; budget++)
        {
            ushort word = definition.Inputs[pc];
            int operand = word & 4095;
            int opcode = word >> 12;
            switch (opcode)
            {
                case 1:
                    NativeTutorialMenuStep step = steps[pc];
                    if (!inputPrepared || Menu.ScreenId != step.ScreenId || step.EntryId is string target && Menu.SelectedEntry?.Id != target)
                    {
                        PrepareMenuInput(step);
                        inputPrepared = true;
                        actionDelay = MenuActionCallbacks - 1;
                        return;
                    }
                    if (actionDelay > 0 && --actionDelay > 0)
                    {
                        return;
                    }
                    PressMenuInput(step);
                    inputPrepared = false;
                    OperationExecuted?.Invoke(pc, word);
                    pc++;
                    return;
                case 2:
                    break;
                case 3:
                    messageVisible = definition.Id > 3 || DashboardMessage() is not null;
                    if (messageVisible)
                    {
                        messagePresentationId++;
                        UpdateMessage();
                    }
                    break;
                case 4:
                    messageVisible = false;
                    UpdateMessage();
                    break;
                case 5:
                    textId = operand;
                    break;
                case 6:
                    centerX = operand;
                    break;
                case 7:
                    centerY = operand;
                    break;
                case 8:
                    if (!messageVisible)
                    {
                        break;
                    }
                    if (input == PlayerActionKind.Cancel)
                    {
                        complete = true;
                        messageVisible = false;
                        UpdateMessage();
                        return;
                    }
                    if (input != PlayerActionKind.Confirm)
                    {
                        return;
                    }
                    OperationExecuted?.Invoke(pc, word);
                    pc++;
                    return;
                case 9:
                    if (operand == 0)
                    {
                        throw new InvalidDataException("A native tutorial delay must make progress.");
                    }
                    // Every scripted menu action already has its own pointer dwell.
                    // Dashboard guidance also omits padding between the original text pages.
                    if (steps.ContainsKey(pc - 1) || steps.ContainsKey(pc + 1) || definition.Id <= 3 && pc > 3)
                    {
                        break;
                    }
                    delay = operand;
                    OperationExecuted?.Invoke(pc, word);
                    return;
                case 10:
                    OperationExecuted?.Invoke(pc, word);
                    pc++;
                    return;
                case 15:
                    complete = true;
                    messageVisible = false;
                    UpdateMessage();
                    OperationExecuted?.Invoke(pc, word);
                    return;
                default:
                    throw new InvalidDataException($"Unsupported native tutorial word0x{word:X4} at{pc}.");
            }
            OperationExecuted?.Invoke(pc, word);
            pc++;
        }
        throw new InvalidDataException("Native tutorial did not reach an input, delay or completion boundary.");
    }

    private void PrepareMenuInput(NativeTutorialMenuStep step)
    {
        if (Menu.ScreenId != step.ScreenId || !Menu.IsOpen)
        {
            Menu.Open(step.ScreenId == "junction-dashboard" ? "junction" : step.ScreenId);
        }
        if (step.CharacterId is string character)
        {
            Menu.Execute(new MenuAction("select-character", character));
        }
        if (step.ScreenId == "junction-dashboard")
        {
            Menu.PrepareTutorialJunctionStep(step);
            UpdateMessage();
            return;
        }
        if (step.Context is MenuAction context)
        {
            Menu.Execute(context);
        }
        if (step.ScreenId == "junction-stock" && step.EntryId is string magic)
        {
            Menu.Execute(new MenuAction("select-magic", magic));
        }
        Menu.InvalidateLiveData();
        if (step.EntryId is string entry)
        {
            Menu.SelectEntry(step.ScreenId, entry, allowDisabled: step.Press == NativeTutorialPress.Select);
        }
        UpdateMessage();
    }

    private void PressMenuInput(NativeTutorialMenuStep step)
    {
        switch (step.Press)
        {
            case NativeTutorialPress.Confirm:
            case NativeTutorialPress.ApplyDashboard:
                Menu.Navigate(MenuInput.Confirm);
                break;
            case NativeTutorialPress.Cancel:
                Menu.Navigate(MenuInput.Cancel);
                break;
            case NativeTutorialPress.Inspect:
                Menu.Push("gf-abilities");
                break;
        }
        if (step.Destination is string destination)
        {
            Menu.Open(destination);
        }
        Menu.ClearTutorialActionReceipt();
        UpdateMessage();
    }

    private void UpdateMessage()
    {
        if (!messageVisible)
        {
            Menu.ShowTutorialMessage(null, centerX, centerY);
            observedTextId = null;
            return;
        }
        if (definition.Id <= 3)
        {
            Menu.ShowTutorialMessage(DashboardMessage(), centerX, centerY);
            return;
        }
        if (definition.Id == 4 && GuardianForceLessonMessage() is string guardianForceMessage)
        {
            Menu.ShowTutorialMessage(guardianForceMessage, centerX, centerY);
            return;
        }
        string file = $"mngrp_slot{definition.Descriptor.TextResource:000}_table";
        string source = definition.Texts[textId].Value;
        var bound = Menu.BindNativeTutorial(file, textId, source);
        string rendered = Menu.FormatNativeTutorialText(bound?.Text ?? Menu.LocalizeNativeTutorial(file, textId, source), names);
        if (bound is not null)
        {
            var binding = bound.Binding with { Text = rendered, Arguments = [names.ToDictionary(pair => pair.Key, pair => pair.Value)] };
            rendered = Memoria.FF8.Resources.Localization.LocalizationTextTrace.Attach(rendered, binding);
            if (observedTextId != textId || observedPresentationId != messagePresentationId)
            {
                observedTextId = textId;
                observedPresentationId = messagePresentationId;
                Menu.ObserveTutorialDialogue(binding, messageOccurrenceSession + ":" + (++messageOccurrenceSequence).ToString(CultureInfo.InvariantCulture), rendered);
            }
        }
        Menu.ShowTutorialMessage(rendered, centerX, centerY);
    }

    private string? DashboardMessage()
    {
        (string Key, string Text)? message = (definition.Id, textId) switch
        {
            (0, 0) => ("junction-intro", "Junction lets a character use a GF's learned abilities. This dashboard shows the character's assignments in one place."),
            (0, 13) => ("gfs", "Highlight an acquired GF portrait to see the abilities it has learned and preview which Junction slots it can unlock."),
            (0, 15) => ("attach-gf", "Confirm a GF portrait to junction that GF to the character."),
            (0, 18) => ("unlocked-slots", "The GF's learned HP-J and Vit-J abilities unlock the HP and Vit magic slots."),
            (0, 22) => ("commands", "The basic action has its own row. The three positions below it hold configurable battle commands."),
            (0, 23) => ("passives", "Passive abilities occupy separate positions. Some GFs can unlock additional positions."),
            (0, 35) => ("command-preview", "Open an empty command position and highlight a choice to preview it. Confirm equips the choice."),
            (0, 37) => ("draw-command", "Equip Draw so the character can acquire magic during battle."),
            (0, 39) => ("remaining-commands", "Equip the GF and Item commands in the two remaining command positions."),
            (0, 44) => ("passive-preview", "Open the first passive position and equip Mag+20% to raise the character's Magic."),
            (0, 49) => ("junction-end", "The character now has a GF, three battle commands and a passive ability."),
            (1, 0) => ("magic-intro", "Junctioning stocked magic can raise a character's stats."),
            (1, 9) => ("stats", "Highlight a stat that its attached GF has unlocked. Confirm to open the magic picker for that stat."),
            (1, 12) => ("stat-preview", "Highlight a spell to preview how much it changes the stat. The current value stays visible beside the proposed value."),
            (1, 14) => ("stat-assigned", "Confirm to assign the spell. The dashboard now shows the spell beside the updated stat."),
            (1, 18) => ("optimize-entry", "Open Actions and choose Optimize stats to propose several stat junctions at once."),
            (1, 20) => ("optimize", "Optimize stats offers Attack, Magic and Defense priorities. It adjusts unpinned stat junctions."),
            (1, 25) => ("optimize-review", "Choose Attack to favor Strength. Review the proposed values on the dashboard before applying them."),
            (1, 27) => ("magic-end", "The dashboard now shows the new stat junctions together."),
            (2, 0) => ("element-intro", "An attached GF with learned elemental Junction abilities unlocks elemental attack or defense slots."),
            (2, 8) => ("element-picker", "Confirm an unlocked elemental slot to open its magic picker. Highlight a spell to preview its effect."),
            (2, 11) => ("element-ineligible", "Spells without an elemental effect, such as Cure, cannot be assigned to Elem-Atk. Cancel closes the picker and keeps the current assignment."),
            (2, 12) => ("element-attack", "Elem-Atk gives the character's attacks an element. The attack percentage shows how much of the attack uses that element. An enemy may absorb it."),
            (2, 17) => ("element-defense", "Elem-Def reduces damage from its element. At 100%, that element deals no damage."),
            (2, 21) => ("element-absorption", "Elemental defense above 100% absorbs damage from that element and restores HP."),
            (2, 25) => ("element-combined", "Elemental defense adds the contributions from all equipped defense spells. The table shows the resulting values for all eight elements."),
            (2, 28) => ("element-end", "Use the table's Attack and Defense controls to inspect either set of elemental values."),
            (3, 0) => ("status-intro", "An attached GF with learned status Junction abilities unlocks status attack or defense slots."),
            (3, 7) => ("status-picker", "Confirm an unlocked status slot and highlight a spell to preview its effect in the status table."),
            (3, 10) => ("status-ineligible", "Spells that cause no status change, such as Fire, cannot be assigned to ST-Atk. Cancel closes the picker and keeps the current assignment."),
            (3, 12) => ("status-attack", "ST-Atk gives normal attacks a chance to inflict a status. The target's resistance also affects the outcome."),
            (3, 17) => ("status-defense", "ST-Def reduces the chance of receiving a status. The defense column combines contributions across equipped spells."),
            (3, 23) => ("status-combined", "Some spells defend against several statuses at once. Highlight Esuna to see which rows change before confirming."),
            (3, 25) => ("status-end", "Use the table's Attack and Defense controls to inspect either set of status values."),
            _ => null
        };
        return message is { } dialog ? Menu.SystemText("tutorial.junction-dashboard." + dialog.Key, dialog.Text) : null;
    }

    private MenuData BuildData()
    {
        NativeCharacterVitals displayedParty = party;
        MenuData result = LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = displayedParty }, names, menuCatalog, visibleCharacterRows: visibleRows);
        result.SourceDescription = Menu.SystemText("tutorial.source.initialization", "Original tutorial initialization");
        foreach (MenuCharacter character in result.Characters)
        {
            BattlePlayerStats stats = catalog.ReadStatistics(displayedParty, int.Parse(character.Id.AsSpan("character-".Length), CultureInfo.InvariantCulture));
            character.Stats["HP"] = character.MaximumHp ?? 0;
            character.Stats["Str"] = stats.Strength;
            character.Stats["Vit"] = stats.Vitality;
            character.Stats["Mag"] = stats.Magic;
            character.Stats["Spr"] = stats.Spirit;
            character.Stats["Spd"] = stats.Speed;
            character.Stats["Luck"] = stats.Luck;
            character.Stats["Hit"] = stats.Hit;
            character.Stats["Eva"] = stats.Evasion;
            character.LimitBreak = character.NativeId switch { 0 => "Renzokuken", 1 => "Duel", 4 => "Combine", _ => character.LimitBreak };
            foreach ((string key, string value) in settings)
            {
                character.LimitSettings[key] = value;
                if (key is "Auto" or "Indicator" or "Duel-Auto")
                {
                    character.LimitToggles[key] = value == "On";
                }
            }
        }
        result.SupportedActions.UnionWith(["junction-gf", "junction-command", "junction-ability", "junction-magic", "junction-off", "junction-edit", "gf-learn", "tutorial-setting", "tutorial-party", "tutorial-exchange", "tutorial-exchange-preview"]);
        return result;
    }

    private void ApplyMenuAction(MenuAction action)
    {
        int Number(string value, string prefix = "") => int.Parse(value.AsSpan(prefix.Length), CultureInfo.InvariantCulture);
        int row = action.TargetId.StartsWith("character-", StringComparison.Ordinal) ? Number(action.TargetId, "character-") : definition.Descriptor.CharacterId;
        party = action.Kind switch
        {
            "junction-gf" => party.ToggleGuardianForceJunction(row, Number(action.SubjectId, "gf-")),
            "junction-command" => party.SetJunctionAbility(row, Number(action.Value), Number(action.SubjectId), command: true),
            "junction-ability" => party.SetJunctionAbility(row, Number(action.Value), Number(action.SubjectId), command: false),
            "junction-magic" => party.SetJunctionMagic(row, Enumerable.Range(0, 19).Single(slot => LiveMenuData.JunctionSlotName(slot) == action.Value), action.SubjectId == "0" ? 0 : Number(action.SubjectId, $"magic-{row}-")),
            "junction-off" => party.RemoveJunctions(row, action.Value == "All"),
            "gf-learn" => party.SelectGuardianForceLearning(Number(action.TargetId, "gf-"), Number(action.SubjectId)),
            "tutorial-setting" => ApplySetting(action),
            "tutorial-party" => SwitchParty(action),
            "tutorial-exchange" => ExchangeJunctions(action),
            "tutorial-exchange-preview" => PreviewExchange(action),
            "junction-edit" => NativeJunctionMenuInput.Apply(party, NativeJunctionMenuProjection.PlayerAction(action)),
            _ => throw new InvalidOperationException($"Tutorial cannot dispatch '{action.Kind}' to a live owner.")
        };
        party.Validate();
    }

    public JsonElement Capture() => JsonSerializer.SerializeToElement(new NativeTutorialSnapshot(2, catalog.ResourceHash, definition.Id, pc, frame, delay, textId, centerX, centerY, messageVisible, inputPrepared, complete, party.DeepCopy(), Menu.CaptureNavigation(), new(settings, StringComparer.Ordinal), new(names)) { ActionDelay = actionDelay });

    private void Restore(JsonElement element)
    {
        NativeTutorialSnapshot saved = element.Deserialize<NativeTutorialSnapshot>() ?? throw new InvalidDataException("Missing native tutorial playback.");
        int terminal = definition.Inputs.IndexOf((ushort)0xF000);
        if (saved.Version is not (1 or 2) || saved.ResourceHash != catalog.ResourceHash || saved.TutorialId != definition.Id || saved.ProgramCounter < 0 || saved.ProgramCounter > terminal || saved.Frame < 0 || saved.Delay < 0 || saved.Delay > 4095 || saved.TextId < 0 || saved.TextId >= definition.Texts.Length || saved.CenterX is < 0 or > 4095 || saved.CenterY is < 0 or > 4095 || saved.Delay > 0 && (definition.Inputs[saved.ProgramCounter] >> 12 != 9 || saved.Delay > (definition.Inputs[saved.ProgramCounter] & 4095)) || saved.InputPrepared && definition.Inputs[saved.ProgramCounter] >> 12 != 1 || saved.Complete && definition.Inputs[saved.ProgramCounter] >> 12 is not (8 or 15) || saved.Party is null || saved.Navigation is null || saved.Settings is null || saved.Party.InitOutSha256 != party.InitOutSha256 || saved.Party.KernelSha256 != party.KernelSha256)
        {
            throw new InvalidDataException("Native tutorial playback changed its resource, program boundary or isolated initialization provenance.");
        }
        if (saved.ActionDelay is < 0 or >= MenuActionCallbacks || saved.ActionDelay > 0 && (!saved.InputPrepared || saved.Complete))
        {
            throw new InvalidDataException("Saved tutorial pointer timing is outside its prepared menu action.");
        }
        if (saved.Names is null || saved.Names.Values.Any(value => value is null))
        {
            throw new InvalidDataException("Saved tutorial names are incomplete.");
        }
        string[] requiredSettings = ["Auto", "Indicator", "Duel-Auto", "Angelo learning"];
        if (requiredSettings.Any(key => !saved.Settings.ContainsKey(key)) || saved.Settings.Values.Any(value => value is null) || saved.Settings.Any(pair => pair.Key is not ("Auto" or "Indicator" or "Duel-Auto" or "Angelo learning" or "Exchange source" or "Exchange target")) || requiredSettings.Take(3).Any(key => saved.Settings[key] is not ("On" or "Off")) || !int.TryParse(saved.Settings["Angelo learning"], out int training) || training is < 0 or > 7 || saved.Settings.Where(pair => pair.Key is "Exchange source" or "Exchange target").Any(pair => !pair.Value.StartsWith("character-", StringComparison.Ordinal) || !int.TryParse(pair.Value.AsSpan("character-".Length), out int row) || !visibleRows.Contains(row)))
        {
            throw new InvalidDataException("Saved tutorial settings changed their native option or character domains.");
        }
        saved.Party.Validate();
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(saved.Party.Definitions), JsonSerializer.SerializeToElement(party.Definitions)) || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(saved.Party.MenuDefinitions), JsonSerializer.SerializeToElement(party.MenuDefinitions)))
        {
            throw new InvalidDataException("Saved tutorial changed its immutable original kernel definitions.");
        }
        party = MapTutorialCommandAbilities(saved.Party);
        pc = saved.ProgramCounter;
        frame = saved.Frame;
        delay = saved.Delay;
        actionDelay = saved.ActionDelay;
        textId = saved.TextId;
        centerX = saved.CenterX;
        centerY = saved.CenterY;
        messageVisible = saved.MessageVisible && (definition.Id > 3 || DashboardMessage() is not null);
        inputPrepared = saved.InputPrepared;
        complete = saved.Complete;
        settings.Clear();
        foreach ((string key, string value) in saved.Settings)
        {
            settings.Add(key, value);
        }
        names.Clear();
        foreach ((int key, string value) in saved.Names)
        {
            names.Add(key, value);
        }
        Menu.InvalidateLiveData();
        if (saved.Version == 1 && definition.Id <= 3 && saved.Navigation.JunctionDashboard is null)
        {
            RestoreLegacyJunctionNavigation(saved.Navigation);
        }
        else
        {
            Menu.RestoreNavigation(AdaptGuardianPortraitNavigation(saved.Navigation));
        }
        if (!Menu.IsJunctionDashboard && Menu.Entries.Count > 0 && Menu.SelectedIndex >= Menu.Entries.Count || Menu.IsSelectingCharacter && Menu.CharacterSelectionIndex >= Menu.CharacterChoices.Count)
        {
            throw new InvalidDataException("Saved tutorial cursor is outside its current command or character choices.");
        }
        UpdateMessage();
    }

    private MenuNavigationState AdaptGuardianPortraitNavigation(MenuNavigationState saved)
    {
        if (saved.JunctionDashboard is not { } dashboard)
        {
            return saved;
        }
        int firstGuardianForce = Array.FindIndex(party.GuardianForces, force => (force.Flags & 1) != 0);
        if (firstGuardianForce < 0)
        {
            return saved;
        }
        string portrait = dashboard.Candidate.StartsWith("gf-", StringComparison.Ordinal) ? "gf-portrait-" + dashboard.Candidate : $"gf-portrait-gf-{firstGuardianForce}";
        string origin = dashboard.Origin == "manage-gfs" ? portrait : dashboard.Origin;
        string focus = dashboard.Focus == "manage-gfs" || dashboard.Editor == "gf" && dashboard.Focus.StartsWith("candidate-gf-", StringComparison.Ordinal) ? portrait : dashboard.Focus;
        return saved with { JunctionDashboard = dashboard with { Origin = origin, Focus = focus } };
    }

    private void RestoreLegacyJunctionNavigation(MenuNavigationState saved)
    {
        string[] screens = ["junction", "junction-options", "junction-gf", "gf-abilities", "junction-command", "junction-command-choice", "junction-ability", "junction-ability-choice", "junction-magic", "junction-element", "junction-status", "junction-stock", "junction-off", "junction-auto"];
        string expectedCharacter = $"character-{definition.Descriptor.CharacterId}";
        if (!screens.Contains(saved.ScreenId, StringComparer.Ordinal) || saved.History is null || saved.History.Any(screen => !screens.Contains(screen, StringComparer.Ordinal)) || saved.CharacterId != expectedCharacter || saved.CharacterSelectionDestination is null || saved.CharacterSelectionDestination.Length != 0)
        {
            throw new InvalidDataException("This earlier Junction tutorial snapshot has no supported dashboard navigation migration.");
        }
        NativeTutorialMenuStep? step = inputPrepared ? steps.GetValueOrDefault(pc) : steps.Where(pair => pair.Key < pc).OrderBy(pair => pair.Key).LastOrDefault().Value;
        if (saved.ScreenId == "junction-stock" && (step is null || step.EntryId is not string candidate || !candidate.StartsWith("candidate-magic-", StringComparison.Ordinal) || saved.MagicId != candidate[10..] || step.DashboardPreparation?.Contains("slot-" + saved.JunctionSlot, StringComparer.Ordinal) != true))
        {
            throw new InvalidDataException("The earlier tutorial magic picker cannot be mapped to one explicit dashboard candidate and destination.");
        }
        if (step is null)
        {
            Menu.Open("junction");
            Menu.Execute(new MenuAction("select-character", expectedCharacter));
            return;
        }
        if (!inputPrepared && step.Press == NativeTutorialPress.ApplyDashboard)
        {
            Menu.Open("junction");
            Menu.Execute(new MenuAction("select-character", expectedCharacter));
            string origin = step.DashboardPreparation?.LastOrDefault(control => control.StartsWith("slot-", StringComparison.Ordinal) || control.StartsWith("command-", StringComparison.Ordinal) || control.StartsWith("passive-", StringComparison.Ordinal) || control.StartsWith("gf-portrait-", StringComparison.Ordinal)) ?? "slot-HP";
            Menu.SelectEntry("junction-dashboard", origin, allowDisabled: true);
            return;
        }
        PrepareMenuInput(step);
        if (!inputPrepared && step.Press == NativeTutorialPress.Confirm)
        {
            PressMenuInput(step);
        }
    }

    private void ValidatePlan()
    {
        for (int position = 0; position < definition.Inputs.Length; position++)
        {
            ushort word = definition.Inputs[position];
            if (word >> 12 == 15)
            {
                return;
            }
            if (word >> 12 == 1 && !steps.ContainsKey(position) || word >> 12 == 5 && (word & 4095) >= definition.Texts.Length)
            {
                throw new InvalidDataException($"Native tutorial{definition.Id} has no resolved menu target/text at{position}.");
            }
        }
        throw new InvalidDataException("Native tutorial has no original termination word.");
    }
}

internal sealed record NativeTutorialSnapshot(int Version, string ResourceHash, int TutorialId, int ProgramCounter, long Frame, int Delay, int TextId, int CenterX, int CenterY, bool MessageVisible, bool InputPrepared, bool Complete, NativeCharacterVitals Party, MenuNavigationState Navigation, Dictionary<string, string> Settings, Dictionary<int, string> Names)
{
    public int ActionDelay { get; init; }
}
