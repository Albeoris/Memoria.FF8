using System.Globalization;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>4BE100's original WORD program, presented through semantic menu targets and an exclusively owned tutorial party.</summary>
internal sealed partial class NativeTutorialPlayer
{
    // Tutorial WORD delays count menu callbacks, independently of the field simulation.
    internal const int CallbacksPerSecond = 60;
    private readonly NativeTutorialCatalog catalog;
    private readonly LiveMenuCatalog menuCatalog;
    private readonly NativeTutorialDefinition definition;
    private readonly IReadOnlyDictionary<int, NativeTutorialMenuStep> steps;
    private readonly IReadOnlySet<int> visibleRows;
    private NativeCharacterVitals party;
    private int pc;
    private long frame;
    private int delay;
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
        party = catalog.CreateIsolatedParty(tutorialId);
        visibleRows = party.CombatParty.Where(row => row != 255).Select(row => (int)row).Concat(Enumerable.Range(0, party.Records.Length).Where(row => (party.Records[row].MemberFlags & 1) != 0)).ToHashSet();
        steps = tutorialId <= 3 ? NativeTutorialJunctionPlan.Create(tutorialId, definition, party) : NativeTutorialOtherPlan.Create(tutorialId, definition, party);
        ValidatePlan();
        Menu = new MenuController(BuildData, ApplyMenuAction);
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
    public bool IsComplete => complete;
    public bool WaitingForInput => !complete && delay == 0 && definition.Inputs[pc] >> 12 == 8;
    public object Status => new { TutorialId = definition.Id, Frame = frame, ProgramCounter = pc, Delay = delay, TextId = textId, MessageVisible = messageVisible, WaitingForInput, IsComplete, Menu.ScreenId, Menu.SelectedIndex, SelectedEntry = Menu.SelectedEntry?.Id };
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
                    if (inputPrepared && (Menu.ScreenId != step.ScreenId || step.EntryId is string target && Menu.SelectedEntry?.Id != target))
                    {
                        PrepareMenuInput(step);
                        return;
                    }
                    if (!inputPrepared)
                    {
                        PrepareMenuInput(step);
                        inputPrepared = true;
                        // Selection is visible for a full presentation callback before the emulated press.
                        if (step.Press != NativeTutorialPress.Select)
                        {
                            return;
                        }
                    }
                    PressMenuInput(step);
                    inputPrepared = false;
                    OperationExecuted?.Invoke(pc, word);
                    pc++;
                    continue;
                case 2:
                    OperationExecuted?.Invoke(pc, word);
                    pc++;
                    return;
                case 3:
                    messageVisible = true;
                    messagePresentationId++;
                    UpdateMessage();
                    OperationExecuted?.Invoke(pc, word);
                    pc++;
                    return;
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
            Menu.Open(step.ScreenId);
        }
        if (step.CharacterId is string character)
        {
            Menu.Execute(new MenuAction("select-character", character));
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

    private MenuData BuildData()
    {
        NativeCharacterVitals displayedParty = PreviewParty();
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
        result.SupportedActions.UnionWith(["junction-gf", "junction-command", "junction-ability", "junction-magic", "junction-off", "gf-learn", "tutorial-setting", "tutorial-party", "tutorial-exchange", "tutorial-exchange-preview", "tutorial-auto"]);
        return result;
    }

    private NativeCharacterVitals PreviewParty()
    {
        if (Menu is null)
        {
            return party;
        }
        MenuNavigationState context = Menu.CaptureNavigation();
        if (context.ScreenId != "junction-stock" || !context.CharacterId.StartsWith("character-", StringComparison.Ordinal) || !int.TryParse(context.CharacterId.AsSpan("character-".Length), out int row) || row is < 0 or >= 8)
        {
            return party;
        }
        string prefix = $"magic-{row}-";
        if (!context.MagicId.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(context.MagicId.AsSpan(prefix.Length), out int spell) || spell is < 1 or >= 57)
        {
            return party;
        }
        int slot = Enumerable.Range(0, 19).Single(index => LiveMenuData.JunctionSlotName(index) == context.JunctionSlot);
        if (!party.JunctionMagicSlotAvailable(row, slot))
        {
            return party;
        }
        var magic = catalog.ReadStatRules(party, row).MagicRecords[spell];
        // Original elemental/status choices retain a noneligible spell's cursor without previewing a junction.
        bool eligible = slot switch
        {
            < 9 => true,
            9 => magic[0x20] != 0,
            10 => (magic[0x26] | magic[0x27]) != 0,
            < 15 => magic[0x22] != 0,
            _ => (magic[0x28] | magic[0x29]) != 0
        };
        return eligible ? party.SetJunctionMagic(row, slot, spell) : party;
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
            "tutorial-auto" => AutoJunction(action),
            _ => throw new InvalidOperationException($"Tutorial cannot dispatch '{action.Kind}' to a live owner.")
        };
        party.Validate();
    }

    public JsonElement Capture() => JsonSerializer.SerializeToElement(new NativeTutorialSnapshot(1, catalog.ResourceHash, definition.Id, pc, frame, delay, textId, centerX, centerY, messageVisible, inputPrepared, complete, party.DeepCopy(), Menu.CaptureNavigation(), new(settings, StringComparer.Ordinal), new(names)));

    private void Restore(JsonElement element)
    {
        NativeTutorialSnapshot saved = element.Deserialize<NativeTutorialSnapshot>() ?? throw new InvalidDataException("Missing native tutorial playback.");
        int terminal = definition.Inputs.IndexOf((ushort)0xF000);
        if (saved.Version != 1 || saved.ResourceHash != catalog.ResourceHash || saved.TutorialId != definition.Id || saved.ProgramCounter < 0 || saved.ProgramCounter > terminal || saved.Frame < 0 || saved.Delay < 0 || saved.Delay > 4095 || saved.TextId < 0 || saved.TextId >= definition.Texts.Length || saved.CenterX is < 0 or > 4095 || saved.CenterY is < 0 or > 4095 || saved.Delay > 0 && (definition.Inputs[saved.ProgramCounter] >> 12 != 9 || saved.Delay > (definition.Inputs[saved.ProgramCounter] & 4095)) || saved.InputPrepared && definition.Inputs[saved.ProgramCounter] >> 12 != 1 || saved.Complete && definition.Inputs[saved.ProgramCounter] >> 12 is not (8 or 15) || saved.Party is null || saved.Navigation is null || saved.Settings is null || saved.Party.InitOutSha256 != party.InitOutSha256 || saved.Party.KernelSha256 != party.KernelSha256)
        {
            throw new InvalidDataException("Native tutorial playback changed its resource, program boundary or isolated initialization provenance.");
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
        party = saved.Party.DeepCopy();
        pc = saved.ProgramCounter;
        frame = saved.Frame;
        delay = saved.Delay;
        textId = saved.TextId;
        centerX = saved.CenterX;
        centerY = saved.CenterY;
        messageVisible = saved.MessageVisible;
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
        Menu.RestoreNavigation(saved.Navigation);
        if (Menu.Entries.Count > 0 && Menu.SelectedIndex >= Menu.Entries.Count || Menu.IsSelectingCharacter && Menu.CharacterSelectionIndex >= Menu.CharacterChoices.Count)
        {
            throw new InvalidDataException("Saved tutorial cursor is outside its current command or character choices.");
        }
        UpdateMessage();
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

internal sealed record NativeTutorialSnapshot(int Version, string ResourceHash, int TutorialId, int ProgramCounter, long Frame, int Delay, int TextId, int CenterX, int CenterY, bool MessageVisible, bool InputPrepared, bool Complete, NativeCharacterVitals Party, MenuNavigationState Navigation, Dictionary<string, string> Settings, Dictionary<int, string> Names);
