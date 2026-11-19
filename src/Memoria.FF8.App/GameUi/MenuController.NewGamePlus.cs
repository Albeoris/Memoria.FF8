using System.Globalization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private NewGamePlusDraft? newGamePlusDraft;
    private Action<NewGamePlusDraft>? beginNewGamePlus;
    private Action? backNewGamePlus;
    private string newGamePlusCategory = "characters";
    private string newGamePlusResetCategory = "";
    private NewGamePlusPreset newGamePlusResetPreset;
    private int newGamePlusGuardian;
    private int newGamePlusAbility;
    private int newGamePlusMagicRow;
    private int newGamePlusSpell;
    private string newGamePlusMagicReview = "";
    public bool IsNewGamePlusSetup { get; private set; }
    public Action? CancelNewGamePlusSetup { get; set; }

    public void OpenNewGamePlusInitialSettings()
    {
        IsNewGamePlusSetup = true;
        Open("new-game-options");
        SelectEntry("new-game-options", "begin");
    }

    public void EndNewGamePlusSetup()
    {
        IsNewGamePlusSetup = false;
        newGamePlusDraft = null;
        beginNewGamePlus = null;
        backNewGamePlus = null;
        CancelNewGamePlusSetup = null;
    }

    internal void OpenNewGamePlus(NewGamePlusDraft draft, Action<NewGamePlusDraft> begin, Action back)
    {
        newGamePlusDraft = draft;
        beginNewGamePlus = begin;
        backNewGamePlus = back;
        IsNewGamePlusSetup = true;
        RegisterNewGamePlusScreens();
        Open("new-game-plus");
        string review = NgMagicConversionSummary();
        if ((draft.MagicConversionMoves.Length != 0 || draft.MagicOverflow.Length != 0) && newGamePlusMagicReview != review)
        {
            newGamePlusMagicReview = review;
            Push("new-game-plus-magic-review");
        }
    }

    private string NgText(string key, string fallback) => SystemText("new-game-plus." + key, fallback);
    private string NgPreset(NewGamePlusPreset value) => NgText("preset." + value.ToString().ToLowerInvariant(), value.ToString());
    private string NgCharacter(byte identity) => NewGamePlusCharacterName?.Invoke(identity) ?? Data.Characters.FirstOrDefault(character => character.NativeId == identity)?.Name ?? NgText("character." + identity, new[] { "Squall", "Zell", "Irvine", "Quistis", "Rinoa", "Selphie", "Seifer", "Edea", "Laguna", "Kiros", "Ward" }[identity]);
    private string NgGuardian(int identity) => guardianEditorName?.Invoke(identity) ?? Data.GuardianForces.FirstOrDefault(gf => gf.NativeId == identity)?.Name ?? NgText("gf." + identity, new[] { "Quezacotl", "Shiva", "Ifrit", "Siren", "Brothers", "Diablos", "Carbuncle", "Leviathan", "Pandemona", "Cerberus", "Alexander", "Doomtrain", "Bahamut", "Cactuar", "Tonberry", "Eden" }[identity]);
    private string NgAbility(int identity) => guardianAbilityName?.Invoke(identity) ?? Data.GuardianForces.SelectMany(gf => gf.LearningChoices).FirstOrDefault(ability => ability.Id == identity.ToString(CultureInfo.InvariantCulture))?.Name ?? NgText("ability", "Ability") + " " + identity;
    private string NgItem(int identity) => NewGamePlusItemName?.Invoke(identity) ?? Data.Items.FirstOrDefault(item => item.Id == identity.ToString(CultureInfo.InvariantCulture))?.Name ?? NgText("item", "Item") + " " + identity;
    private string NgCard(int identity) => NewGamePlusCardName?.Invoke(identity) ?? Data.Cards.FirstOrDefault(card => card.NativeId == identity)?.Name ?? NgText("card", "Card") + " " + identity;
    private string NgSpell(int identity) => NewGamePlusMagicName?.Invoke(identity) ?? Data.Magic.FirstOrDefault(spell => spell.SpellId == identity)?.Name ?? NgText("spell", "Spell") + " " + identity;
    internal Func<int, string>? NewGamePlusCharacterName { get; set; }
    internal Func<int, string>? NewGamePlusItemName { get; set; }
    internal Func<int, UiImageSource?>? NewGamePlusItemIcon { get; set; }
    internal Func<int, string>? NewGamePlusCardName { get; set; }
    internal Func<int, string>? NewGamePlusMagicName { get; set; }
    private string NgHint => CurrentBindingText(ControlAction.Card) + " " + NgText("config", "Config");
    private bool IsNewGamePlusScreen => ScreenId.StartsWith("new-game-plus", StringComparison.Ordinal);

    private void RegisterNewGamePlusScreens()
    {
        foreach (string id in new[] { "new-game-plus", "new-game-plus-config", "new-game-plus-abilities", "new-game-plus-add-ability", "new-game-plus-ability-actions", "new-game-plus-reset", "new-game-plus-review", "new-game-plus-magic-actions", "new-game-plus-magic-review" })
        {
            Register(new()
            {
                Id = id,
                Title = "New Game+",
                Help = "Configure carryover for a fresh playthrough.",
                TitleText = c => c.NgText("title", "New Game+"),
                HelpText = c => c.NgHint,
                Entries = c => c.NewGamePlusEntries(),
                BuildContent = (c, root) => root.Children.Add(c.BuildNewGamePlus())
            });
        }
    }

    private MenuEntry NgAction(string id, string label, string help, string action, string value = "", bool enabled = true) => new(id, label, help, Action: new(action, id, Value: value), Enabled: enabled);
    private string NgRewardHelp(bool experience) => experience ? NgText("exp.help", "Applies to character and GF EXP. The whole battle subtotal is scaled once, with halves rounded up. Zero disables EXP; other rewards continue.") : NgText("ap.help", "Applies to GF AP. The whole battle subtotal is scaled once, with halves rounded up. Zero disables AP; other rewards continue.");
    private string NgCategoryLabel(string category) => NgText("category." + category, category switch { "characters" => "Character Levels", "exp" => "EXP Gain", "gf" => "Guardian Forces", "ap" => "AP Gain", "magic" => "Magic", "items" => "Items", "cards" => "Cards", _ => category });
    private NewGamePlusPreset NgCategoryPreset(string category) => category switch { "characters" => newGamePlusDraft!.CharacterPreset, "gf" => newGamePlusDraft!.GuardianForcePreset, "magic" => newGamePlusDraft!.MagicPreset, "items" => newGamePlusDraft!.ItemPreset, "cards" => newGamePlusDraft!.CardPreset, _ => NewGamePlusPreset.Custom };

    private IReadOnlyList<MenuEntry> NewGamePlusEntries()
    {
        NewGamePlusDraft draft = newGamePlusDraft!;
        List<MenuEntry> entries = [];
        if (ScreenId == "new-game-plus")
        {
            foreach (string category in new[] { "characters", "exp", "gf", "ap", "magic", "items", "cards" })
            {
                entries.Add(NgAction(category, NgCategoryLabel(category), category is "exp" or "ap" ? NgRewardHelp(category == "exp") : NgText("category.help", "Choose a preset or open Config to edit starting values."), "ng-config"));
            }
            entries.Add(NgAction("begin", NgText("begin", "Begin New Game+"), NgText("begin.help", "Review carryover and selected rules before starting."), "ng-review"));
        }
        else if (ScreenId == "new-game-plus-config")
        {
            switch (newGamePlusCategory)
            {
                case "exp":
                case "ap":
                    entries.Add(NgAction(newGamePlusCategory, NgCategoryLabel(newGamePlusCategory), NgRewardHelp(newGamePlusCategory == "exp"), "ng-number"));
                    break;
                case "characters":
                    entries.Add(NgAction("world", NgText("world", "Allow Party Changes on the World Map") + "   " + NgText(draft.AllowWorldMapPartyChanges ? "on" : "off", draft.AllowWorldMapPartyChanges ? "On" : "Off"), NgText("world.help", "Allow party exchange while controlling the world map before the normal story unlock."), "ng-world"));
                    entries.Add(NgAction("all", NgText("global-level", "Level for All Characters"), NgText("global-level.help", "0: Individual / No Override. Values 1–100 override levels while retaining individual choices. Availability remains independent."), "ng-number"));
                    foreach (NewGamePlusCharacter character in draft.Characters)
                    {
                        string id = character.CharacterId.ToString(CultureInfo.InvariantCulture);
                        entries.Add(NgAction("level-" + id, NgCharacter(character.CharacterId), character.NativeFallback ? NgText("native-fallback", "Absent from source: using the native starting level.") : NgText("individual.help", "Choose the starting level. This transfers levels without weapons, junctions or permanent stat bonuses."), "ng-number", enabled: draft.GlobalLevel == 0));
                        entries.Add(NgAction("available-" + id, (character.AvailableFromStart ? "☑ " : "☐ ") + NgText("available", "Available from Start"), NgText("available.help", "Available in the reserve roster. Extra characters remain available; regular characters still follow story departures."), "ng-availability"));
                    }
                    break;
                case "gf":
                    for (int id = 0; id < 16; id++)
                    {
                        bool available = (draft.GuardianForces.GuardianForces[id].Flags & 1) != 0;
                        entries.Add(NgAction(id.ToString(CultureInfo.InvariantCulture), (available ? "☑ " : "☐ ") + NgGuardian(id), NgText("gf.help", "Confirm toggles initial ownership. Config opens this GF's abilities without granting it."), "ng-gf-availability"));
                    }
                    break;
                case "items":
                    for (int id = 1; id < 198; id++)
                    {
                        entries.Add(NgAction(id.ToString(CultureInfo.InvariantCulture), NgItem(id), NgText("quantity.help", "Left / Right or the slider changes starting quantity. Zero removes the stock."), "ng-number"));
                    }
                    break;
                case "cards":
                    for (int id = 0; id < NativeCardInventory.CardCount; id++)
                    {
                        entries.Add(NgAction(id.ToString(CultureInfo.InvariantCulture), NgCard(id), id < 77 ? NgText("cards.common.help", "Common cards stack from 0 to 100.") : NgText("cards.rare.help", "Rare cards are unique: 0 or 1, with matching ownership."), "ng-number"));
                    }
                    break;
                case "magic":
                    if (!draft.Magic.MagicSystem.SharedMagicStock)
                    {
                        entries.Add(NgAction("owner", NgCharacter(draft.Magic.Records[newGamePlusMagicRow].CharacterId), NgText("magic.owner.help", "Left / Right changes the character whose personal stock is shown."), "ng-owner"));
                    }
                    for (int id = 1; id <= 56; id++)
                    {
                        entries.Add(NgAction(id.ToString(CultureInfo.InvariantCulture), NgSpell(id), NgText("magic.quantity.help", "Set stock from 0 to 100. Existing bank capacity and retained overflow are respected."), "ng-number"));
                    }
                    break;
            }
            entries.Add(NgAction("done", NgText("done", "Done"), NgText("done.help", "Keep edits and return."), "menu-back"));
        }
        else if (ScreenId == "new-game-plus-abilities")
        {
            foreach (GuardianForceAbilityTemplate ability in draft.GuardianForces.ResolveGuardianForceTemplates()[newGamePlusGuardian])
            {
                GuardianForceAbilityProgress? progress = draft.GuardianForces.GuardianForces[newGamePlusGuardian].Progression?.Abilities.FirstOrDefault(entry => entry.Ability == ability.Ability);
                bool learned = progress?.Learned == true;
                entries.Add(NgAction(ability.Ability.ToString(CultureInfo.InvariantCulture), (learned ? "☑ " : "☐ ") + NgAbility(ability.Ability) + (progress?.EarnedPoints > 0 ? "   " + progress.EarnedPoints + " / " + ability.RequiredPoints : ""), NgText("ability.help", "Confirm toggles starting learned state and resets AP. Untouched partial AP is retained. Config opens Add / Remove Ability."), "ng-ability-toggle"));
            }
            entries.Add(NgAction("add", NgText("add-ability", "Add Ability"), NgText("add-ability.help", "Choose a supported ability not already present."), "ng-ability-add-open"));
            entries.Add(NgAction("done", NgText("done", "Done"), NgText("done.help", "Keep edits and return."), "menu-back"));
        }
        else if (ScreenId == "new-game-plus-add-ability")
        {
            for (int id = 1; id < draft.GuardianForces.MenuDefinitions.AbilityPoints.Length; id++)
            {
                if (!draft.GuardianForces.ResolveGuardianForceTemplates()[newGamePlusGuardian].Any(ability => ability.Ability == id))
                {
                    entries.Add(NgAction(id.ToString(CultureInfo.InvariantCulture), NgAbility(id), NgText("add-ability.help", "Choose a supported ability not already present."), "ng-ability-add"));
                }
            }
            entries.Add(NgAction("done", NgText("done", "Done"), "", "menu-back"));
        }
        else if (ScreenId == "new-game-plus-ability-actions")
        {
            entries.Add(NgAction("add", NgText("add-ability", "Add Ability"), "", "ng-ability-add-open"));
            entries.Add(NgAction("remove", NgText("remove-ability", "Remove Ability"), NgText("remove-ability.help", "Remove the definition from this GF. This differs from clearing its learned state."), "ng-ability-remove"));
            entries.Add(NgAction("done", NgText("done", "Done"), "", "menu-back"));
        }
        else if (ScreenId == "new-game-plus-reset")
        {
            entries.Add(NgAction("cancel", NgText("cancel", "Cancel"), NgText("reset.cancel", "Keep the current edits."), "menu-back"));
            entries.Add(NgAction("reset", NgText("reset", "Reset to") + " " + NgPreset(newGamePlusResetPreset), NgText("reset.help", "Replace this category's edits with the reviewed preset. Other categories are retained."), "ng-reset-accept"));
        }
        else if (ScreenId == "new-game-plus-review")
        {
            entries.Add(NgAction("cancel", NgText("back", "Back"), NgText("review.back", "Return to customize the draft."), "menu-back"));
            entries.Add(NgAction("begin", NgText("begin", "Begin New Game+"), NgText("review.begin", "Start this fresh playthrough with the selected carryover and rules."), "ng-begin"));
        }
        else if (ScreenId == "new-game-plus-magic-review")
        {
            entries.Add(NgAction("done", NgText("done", "Done"), NgText("magic.review.keep", "Keep this conversion and continue configuring the draft."), "menu-back"));
            entries.Add(NgAction("config", NgText("config", "Config"), NgText("magic.review.config", "Inspect the shared pool and retained personal stock."), "ng-magic-review-config"));
        }
        else if (ScreenId == "new-game-plus-magic-actions")
        {
            entries.Add(NgAction("done", NgText("done", "Done"), "", "menu-back"));
            entries.Add(NgAction("everywhere", NgText("magic.remove-everywhere", "Remove from Entire Collection"), NgText("magic.remove-everywhere.help", "Remove this spell from the shared pool, every personal bank and all retained stock. Changing modes will not restore it."), "ng-magic-remove-everywhere"));
            foreach (MagicStockMove retained in draft.MagicOverflow.Where(move => move.SpellId == newGamePlusSpell))
            {
                byte identity = draft.Magic.Records[retained.RecordId].CharacterId;
                entries.Add(NgAction(identity.ToString(CultureInfo.InvariantCulture), NgText("magic.remove-retained", "Remove retained stock") + " · " + NgCharacter(identity) + " ×" + retained.Quantity, NgText("magic.remove-retained.help", "Remove only this character's retained copy. The shared pool is unchanged."), "ng-magic-remove-retained"));
            }
        }
        return entries;
    }

    private UiElement BuildNewGamePlus()
    {
        UiGrid page = Grid([1.3f, 6.6f, 0.75f], [1], 7);
        Place(page, Panel(null, Label(Message.Length > 0 ? Message : SelectedEntry?.Help ?? NgText("title", "New Game+"), 19, UiTextFlow.Wrap)), 0);
        if (ScreenId == "new-game-plus")
        {
            UiGrid rows = Grid(Enumerable.Repeat(1f, 8).ToArray(), [1], 4);
            for (int index = 0; index < Entries.Count; index++)
            {
                MenuEntry entry = Entries[index];
                int selected = index;
                UiGrid row = Grid([1], [5, 1], 6);
                UiElement control;
                if (entry.Id is "exp" or "ap")
                {
                    control = NgSlider(entry.Id, entry.Label, entry.Id == "exp" ? newGamePlusDraft!.ExperienceMultiplierTenths / 10f : newGamePlusDraft!.ApMultiplierTenths / 10f, 0, 10, 0.1f, value => SetNgReward(entry.Id, value));
                }
                else if (entry.Id == "begin")
                {
                    control = new UiButton { Text = entry.Label, Click = () => Activate(selected), FontSize = 23 * TextScale, DrawBackground = false };
                }
                else
                {
                    NewGamePlusPreset[] choices = entry.Id is "characters" or "gf" ? [NewGamePlusPreset.Stored, NewGamePlusPreset.Native] : [NewGamePlusPreset.Stored, NewGamePlusPreset.Empty];
                    NewGamePlusPreset current = NgCategoryPreset(entry.Id);
                    if (current == NewGamePlusPreset.Custom)
                    {
                        choices = [.. choices, NewGamePlusPreset.Custom];
                    }
                    control = new UiChoiceRow { Text = entry.Label, Items = choices.Select(NgPreset).ToArray(), SelectedIndex = Array.IndexOf(choices, current), SelectionChanged = value => RequestNgReset(entry.Id, choices[value]), FontSize = 22 * TextScale, LabelWidthRatio = 0.5f };
                }
                control.Id = entry.Id;
                control.FocusChanged = focused => SelectConfigRow(focused, selected);
                Place(row, control, 0);
                if (entry.Id != "begin")
                {
                    Place(row, new UiButton { Id = "config-" + entry.Id, Text = NgText("config", "Config"), FontSize = 19 * TextScale, Click = () => OpenNgConfig(entry.Id) }, 0, 1);
                }
                Place(rows, row, index);
            }
            Place(page, Panel(null, rows), 1);
        }
        else if (ScreenId == "new-game-plus-config" && newGamePlusCategory == "characters")
        {
            Place(page, BuildNewGamePlusCharacters(), 1);
        }
        else
        {
            UiGrid body = Grid([1], ScreenId == "new-game-plus-review" ? [2, 3] : [3, 2], 8);
            UiList commands = SecondaryCommands(38, 21);
            if (ScreenId == "new-game-plus-config" && newGamePlusCategory == "gf")
            {
                commands.Images = Entries.Select(entry => int.TryParse(entry.Id, out int id) ? (UiImageSource?)new UiImageSource((newGamePlusDraft!.GuardianForces.GuardianForces[id].Flags & 1) != 0 ? UiImageKind.GuardianForcePortrait : UiImageKind.GuardianForcePortraitGrayscale, id) : null).ToArray();
            }
            else if (ScreenId == "new-game-plus-config" && newGamePlusCategory is "items" or "cards" or "magic")
            {
                commands.SecondaryItems = Entries.Select(entry => int.TryParse(entry.Id, out int id) ? (newGamePlusCategory == "items" ? newGamePlusDraft!.Items.Items.FirstOrDefault(item => item.ItemId == id)?.Count ?? 0 : newGamePlusCategory == "cards" ? Math.Max(0, newGamePlusDraft!.Cards.AvailableCount(id)) : newGamePlusDraft!.Magic.GetMagicCount(newGamePlusMagicRow, id)).ToString(CultureInfo.CurrentCulture) : "").ToArray();
                commands.AutoSecondaryWidth = true;
                if (newGamePlusCategory == "cards")
                {
                    commands.Images = Entries.Select(entry => int.TryParse(entry.Id, out int id) ? (UiImageSource?)new UiImageSource(UiImageKind.Card, id) : null).ToArray();
                }
                else if (newGamePlusCategory == "items")
                {
                    commands.Images = Entries.Select(entry => int.TryParse(entry.Id, out int id) ? NewGamePlusItemIcon?.Invoke(id) : null).ToArray();
                }
            }
            Place(body, Panel(null, commands), 0);
            UiGrid detail = Grid([1, 3, 1], [1], 8);
            string title = ScreenId == "new-game-plus-review" ? NgText("review.title", "Review New Game+") : ScreenId.Contains("abilit", StringComparison.Ordinal) ? NgGuardian(newGamePlusGuardian) : NgCategoryLabel(newGamePlusCategory);
            Place(detail, Panel(null, Label(title, 24, UiTextFlow.Wrap)), 0);
            string explanation = ScreenId == "new-game-plus-review" ? NgSummary() : ScreenId == "new-game-plus-magic-review" ? NgMagicConversionSummary() : SelectedEntry?.Help ?? "";
            if (ScreenId == "new-game-plus-config" && newGamePlusCategory == "magic" && int.TryParse(SelectedEntry?.Id, out int selectedSpell))
            {
                explanation += "\n" + NgRetainedMagic(selectedSpell);
            }
            UiScrollPanel explanationScroll = new() { ContentHeight = ScreenId == "new-game-plus-review" ? 440 * TextScale : 260 * TextScale };
            explanationScroll.Children.Add(Label(explanation, 19, UiTextFlow.Wrap));
            Place(detail, Panel(null, explanationScroll), 1);
            if (TryNgNumber(out float value, out float max))
            {
                Place(detail, Panel(null, NgSlider("ng-selected-value", NgText("value", "Value"), value, 0, max, newGamePlusCategory is "exp" or "ap" ? 0.1f : 1, SetNgNumber)), 2);
            }
            else if (ScreenId == "new-game-plus-config" && newGamePlusCategory == "gf")
            {
                Place(detail, new UiButton { Text = NgText("config", "Config"), Click = OpenSelectedNgConfig, FontSize = 22 * TextScale }, 2);
            }
            if (ScreenId == "new-game-plus-config" && newGamePlusCategory == "magic" && newGamePlusDraft!.Magic.MagicSystem.SharedMagicStock && int.TryParse(SelectedEntry?.Id, out _))
            {
                UiGrid actions = Grid([1], [3, 1], 6);
                if (TryNgNumber(out float magicValue, out float magicMax))
                {
                    Place(actions, NgSlider("ng-selected-value", NgText("value", "Value"), magicValue, 0, magicMax, 1, SetNgNumber), 0);
                }
                Place(actions, new UiButton { Text = NgText("config", "Config"), FontSize = 19 * TextScale, Click = OpenSelectedNgConfig }, 0, 1);
                Place(detail, Panel(null, actions), 2);
            }
            Place(body, detail, 0, 1);
            Place(page, body, 1);
        }
        Place(page, Panel(null, Label(NgHint + "   ·   " + CurrentBindingText(ControlAction.Cancel) + " " + NgText("back", "Back"), 18, UiTextFlow.Wrap)), 2);
        return page;
    }

    private UiNumericSlider NgSlider(string id, string label, float value, float min, float max, float step, Action<float> changed) => new() { Id = id, Text = label, Value = value, Min = min, Max = max, Step = step, ValueFormat = step < 1 ? "0.0'×'" : "0", ValueWidth = 55 * TextScale, FontSize = 21 * TextScale, LabelWidthRatio = 0.48f, ValueChanged = changed };

    private UiElement BuildNewGamePlusCharacters()
    {
        NewGamePlusDraft draft = newGamePlusDraft!;
        float rowHeight = 62 * TextScale;
        UiGrid rows = new() { Gap = 5 };
        for (int index = 0; index < 14; index++)
        {
            rows.RowDefinitions.Add(UiGridLength.Pixel(rowHeight));
        }
        rows.ColumnDefinitions.Add(UiGridLength.Star());
        UiButton world = new() { Id = "world", Text = (draft.AllowWorldMapPartyChanges ? "☑ " : "☐ ") + NgText("world", "Allow Party Changes on the World Map"), FontSize = 22 * TextScale, Click = () => Execute(new("ng-world")), FocusChanged = focused => SelectConfigRow(focused, 0) };
        Place(rows, world, 0);
        UiGrid global = Grid([1, 1], [1], 0);
        Place(global, Label(NgText("global-level", "Level for All Characters") + (draft.GlobalLevel == 0 ? " · " + NgText("individual", "Individual / No Override") : ""), 18, UiTextFlow.Wrap), 0);
        UiNumericSlider all = NgSlider("all", "", draft.GlobalLevel, 0, 100, 1, value =>
        {
            draft.SetGlobalLevel((int)value);
            _view = null;
        });
        all.LabelWidthRatio = 0;
        all.FocusChanged = focused => SelectConfigRow(focused, 1);
        Place(global, all, 1);
        Place(rows, global, 1);
        for (int index = 0; index < draft.Characters.Count; index++)
        {
            NewGamePlusCharacter character = draft.Characters[index];
            int levelSelection = 2 + index * 2;
            int availabilitySelection = levelSelection + 1;
            UiGrid row = Grid([1], [0.5f, 3.1f, 2.1f], 8);
            UiImage portrait = Artwork(draft.GlobalLevel == 0 ? UiImageKind.CharacterPortrait : UiImageKind.CharacterPortraitGrayscale, character.CharacterId);
            Place(row, portrait, 0);
            UiNumericSlider level = NgSlider("level-" + character.CharacterId, NgCharacter(character.CharacterId), draft.GlobalLevel == 0 ? character.IndividualLevel : draft.GlobalLevel, 1, 100, 1, value =>
            {
                draft.SetLevel(character.CharacterId, (int)value);
                _view = null;
            });
            level.ReadOnly = draft.GlobalLevel != 0;
            level.FocusChanged = focused => SelectConfigRow(focused, levelSelection);
            Place(row, level, 0, 1);
            UiButton available = new()
            {
                Id = "available-" + character.CharacterId,
                FontSize = 18 * TextScale,
                Click = () =>
                {
                    draft.ToggleAvailability(character.CharacterId);
                    _view = null;
                },
                FocusChanged = focused => SelectConfigRow(focused, availabilitySelection)
            };
            available.Children.Add(Label((character.AvailableFromStart ? "☑ " : "☐ ") + NgText("available", "Available from Start"), 18, UiTextFlow.Wrap));
            Place(row, available, 0, 2);
            Place(rows, row, index + 2);
        }
        UiButton done = new() { Id = "done", Text = NgText("done", "Done"), FontSize = 22 * TextScale, Click = Back, FocusChanged = focused => SelectConfigRow(focused, Entries.Count - 1) };
        Place(rows, done, 13);
        UiScrollPanel scroll = new() { ContentHeight = 14 * (rowHeight + 5) };
        scroll.Children.Add(rows);
        return Panel(null, scroll);
    }
    private void SetNgReward(string category, float value)
    {
        int tenths = Math.Clamp((int)Math.Round(value * 10), 0, 100);
        if (category == "exp")
        {
            newGamePlusDraft!.ExperienceMultiplierTenths = tenths;
        }
        else
        {
            newGamePlusDraft!.ApMultiplierTenths = tenths;
        }
        _view = null;
    }

    private bool TryNgNumber(out float value, out float max)
    {
        value = 0;
        max = 100;
        if (SelectedEntry?.Action?.Kind != "ng-number" || ScreenId != "new-game-plus-config")
        {
            return false;
        }
        string id = SelectedEntry.Id;
        NewGamePlusDraft draft = newGamePlusDraft!;
        if (newGamePlusCategory is "exp" or "ap")
        {
            value = (newGamePlusCategory == "exp" ? draft.ExperienceMultiplierTenths : draft.ApMultiplierTenths) / 10f;
            max = 10;
        }
        else if (id == "all")
        {
            value = draft.GlobalLevel;
        }
        else if (id.StartsWith("level-", StringComparison.Ordinal))
        {
            value = draft.GlobalLevel != 0 ? draft.GlobalLevel : draft.Characters.Single(character => character.CharacterId == byte.Parse(id[6..], CultureInfo.InvariantCulture)).IndividualLevel;
        }
        else if (newGamePlusCategory == "items")
        {
            value = draft.Items.Items.FirstOrDefault(item => item.ItemId == int.Parse(id, CultureInfo.InvariantCulture))?.Count ?? 0;
        }
        else if (newGamePlusCategory == "cards")
        {
            int card = int.Parse(id, CultureInfo.InvariantCulture);
            value = Math.Max(0, draft.Cards.AvailableCount(card));
            max = card < 77 ? 100 : 1;
        }
        else if (newGamePlusCategory == "magic")
        {
            value = draft.Magic.GetMagicCount(newGamePlusMagicRow, int.Parse(id, CultureInfo.InvariantCulture));
        }
        return true;
    }

    private void SetNgNumber(float value)
    {
        if (SelectedEntry?.Enabled != true)
        {
            return;
        }
        string id = SelectedEntry.Id;
        NewGamePlusDraft draft = newGamePlusDraft!;
        try
        {
            if (newGamePlusCategory is "exp" or "ap")
            {
                SetNgReward(newGamePlusCategory, value);
            }
            else if (id == "all")
            {
                draft.SetGlobalLevel((int)value);
            }
            else if (id.StartsWith("level-", StringComparison.Ordinal))
            {
                draft.SetLevel(byte.Parse(id[6..], CultureInfo.InvariantCulture), (int)value);
            }
            else if (newGamePlusCategory == "items")
            {
                draft.SetItem(int.Parse(id, CultureInfo.InvariantCulture), (int)value);
            }
            else if (newGamePlusCategory == "cards")
            {
                draft.SetCard(int.Parse(id, CultureInfo.InvariantCulture), (int)value);
            }
            else if (newGamePlusCategory == "magic")
            {
                draft.SetMagic(newGamePlusMagicRow, int.Parse(id, CultureInfo.InvariantCulture), (int)value);
            }
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        _view = null;
    }

    private string NgSummary()
    {
        NewGamePlusDraft draft = newGamePlusDraft!;
        return string.Join("\n", new[] { "characters", "gf", "magic", "items", "cards" }.Select(category => NgCategoryLabel(category) + ": " + NgPreset(NgCategoryPreset(category)))) + "\nEXP " + (draft.ExperienceMultiplierTenths / 10m).ToString("0.0", CultureInfo.CurrentCulture) + "× · AP " + (draft.ApMultiplierTenths / 10m).ToString("0.0", CultureInfo.CurrentCulture) + "×\n" + NgText("difficulty", "Difficulty") + ": " + draft.Settings.Difficulty + "\n" + NgText("global-level", "Level for All Characters") + ": " + (draft.GlobalLevel == 0 ? NgText("individual", "Individual / No Override") : draft.GlobalLevel.ToString(CultureInfo.CurrentCulture)) + (draft.MagicOverflow.Length == 0 ? "" : "\n" + NgText("overflow", "Magic overflow is retained in the original personal banks.") + " " + draft.MagicOverflow.Sum(move => move.Quantity));
    }

    private string NgRetainedMagic(int spell)
    {
        MagicStockMove[] retained = newGamePlusDraft!.MagicOverflow.Where(move => move.SpellId == spell).ToArray();
        return retained.Length == 0 ? NgText("magic.retained.none", "No retained personal copies.") : NgText("magic.retained", "Retained personal stock:") + "\n" + string.Join("\n", retained.Select(move => NgCharacter(newGamePlusDraft.Magic.Records[move.RecordId].CharacterId) + " ×" + move.Quantity));
    }

    private string NgMagicConversionSummary()
    {
        NewGamePlusDraft draft = newGamePlusDraft!;
        return NgText("magic.review.title", "Magic conversion") + "\n" + NgText("magic.review.explanation", "Stock is moved once into the shared pool up to its capacity. Overflow remains with its original owner. Config can remove a retained copy or a spell from the entire collection.") + "\n\n" + string.Join("\n", draft.MagicOverflow.Select(move => NgCharacter(draft.Magic.Records[move.RecordId].CharacterId) + " · " + NgSpell(move.SpellId) + " ×" + move.Quantity));
    }

    private void OpenNgConfig(string category)
    {
        newGamePlusCategory = category;
        if (category == "magic" && newGamePlusDraft!.Magic.MagicSystem.SharedMagicStock)
        {
            newGamePlusMagicRow = 0;
        }
        Push("new-game-plus-config");
    }

    private void OpenSelectedNgConfig()
    {
        if (ScreenId == "new-game-plus" && SelectedEntry?.Id is string category && category != "begin")
        {
            OpenNgConfig(category);
        }
        else if (ScreenId == "new-game-plus-config" && newGamePlusCategory == "gf" && int.TryParse(SelectedEntry?.Id, out int guardian))
        {
            newGamePlusGuardian = guardian;
            Push("new-game-plus-abilities");
        }
        else if (ScreenId == "new-game-plus-abilities")
        {
            if (int.TryParse(SelectedEntry?.Id, out int ability))
            {
                newGamePlusAbility = ability;
                Push("new-game-plus-ability-actions");
            }
            else
            {
                Push("new-game-plus-add-ability");
            }
        }
        else if (ScreenId == "new-game-plus-config" && newGamePlusCategory == "magic" && newGamePlusDraft!.Magic.MagicSystem.SharedMagicStock && int.TryParse(SelectedEntry?.Id, out int spell))
        {
            newGamePlusSpell = spell;
            Push("new-game-plus-magic-actions");
        }
    }

    private void RequestNgReset(string category, NewGamePlusPreset preset)
    {
        if (preset == NgCategoryPreset(category) || preset == NewGamePlusPreset.Custom)
        {
            return;
        }
        newGamePlusResetCategory = category;
        newGamePlusResetPreset = preset;
        Push("new-game-plus-reset");
        SelectEntry("new-game-plus-reset", "cancel");
    }

    private bool NavigateNewGamePlus(MenuInput input)
    {
        if (!IsNewGamePlusScreen)
        {
            return false;
        }
        if (input == MenuInput.Alternate)
        {
            OpenSelectedNgConfig();
            return true;
        }
        if (input is MenuInput.Left or MenuInput.Right)
        {
            int direction = input == MenuInput.Left ? -1 : 1;
            if (ScreenId == "new-game-plus" && SelectedEntry?.Id is "exp" or "ap")
            {
                string category = SelectedEntry.Id;
                SetNgReward(category, ((category == "exp" ? newGamePlusDraft!.ExperienceMultiplierTenths : newGamePlusDraft!.ApMultiplierTenths) + direction) / 10f);
            }
            else if (ScreenId == "new-game-plus" && SelectedEntry?.Id is string category && category != "begin")
            {
                NewGamePlusPreset alternate = category is "characters" or "gf" ? NewGamePlusPreset.Native : NewGamePlusPreset.Empty;
                RequestNgReset(category, NgCategoryPreset(category) == NewGamePlusPreset.Stored ? alternate : NewGamePlusPreset.Stored);
            }
            else if (TryNgNumber(out float value, out _))
            {
                SetNgNumber(value + direction * (newGamePlusCategory is "exp" or "ap" ? 0.1f : 1));
            }
            else if (SelectedEntry?.Id == "owner")
            {
                newGamePlusMagicRow = (newGamePlusMagicRow + direction + newGamePlusDraft!.Magic.Records.Length) % newGamePlusDraft.Magic.Records.Length;
                _view = null;
            }
            return true;
        }
        if (input is MenuInput.Up or MenuInput.Down)
        {
            Select(SelectedIndex + (input == MenuInput.Up ? -1 : 1), Entries.Count);
            return true;
        }
        return false;
    }

    private bool ExecuteNewGamePlus(MenuAction action)
    {
        if (!action.Kind.StartsWith("ng-", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            switch (action.Kind)
            {
                case "ng-config":
                    OpenNgConfig(action.SubjectId);
                    break;
                case "ng-review":
                    newGamePlusDraft!.ResolveDefinition();
                    Push("new-game-plus-review");
                    SelectEntry("new-game-plus-review", "cancel");
                    break;
                case "ng-begin":
                    newGamePlusDraft!.ResolveDefinition();
                    beginNewGamePlus!(newGamePlusDraft);
                    break;
                case "ng-world":
                    newGamePlusDraft!.ToggleWorldPartyChanges();
                    break;
                case "ng-availability":
                    newGamePlusDraft!.ToggleAvailability(byte.Parse(action.SubjectId[10..], CultureInfo.InvariantCulture));
                    break;
                case "ng-gf-availability":
                    newGamePlusDraft!.ToggleGuardianForce(int.Parse(action.SubjectId, CultureInfo.InvariantCulture));
                    break;
                case "ng-ability-toggle":
                    newGamePlusDraft!.ToggleAbility(newGamePlusGuardian, int.Parse(action.SubjectId, CultureInfo.InvariantCulture));
                    break;
                case "ng-ability-add-open":
                    Push("new-game-plus-add-ability");
                    break;
                case "ng-ability-add":
                    newGamePlusDraft!.AddAbility(newGamePlusGuardian, int.Parse(action.SubjectId, CultureInfo.InvariantCulture));
                    Back();
                    break;
                case "ng-ability-remove":
                    newGamePlusDraft!.RemoveAbility(newGamePlusGuardian, newGamePlusAbility);
                    Back();
                    break;
                case "ng-reset-accept":
                    newGamePlusDraft!.Reset(newGamePlusResetCategory, newGamePlusResetPreset);
                    Back();
                    break;
                case "ng-magic-review-config":
                    Back();
                    OpenNgConfig("magic");
                    break;
                case "ng-magic-remove-everywhere":
                    newGamePlusDraft!.RemoveMagicEverywhere(newGamePlusSpell);
                    Back();
                    break;
                case "ng-magic-remove-retained":
                    newGamePlusDraft!.SetRetainedMagic(byte.Parse(action.SubjectId, CultureInfo.InvariantCulture), newGamePlusSpell, 0);
                    Back();
                    break;
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or NotSupportedException)
        {
            Message = exception.Message;
        }
        _view = null;
        return true;
    }

    private bool BackNewGamePlus()
    {
        if (ScreenId == "new-game-plus")
        {
            backNewGamePlus?.Invoke();
            return true;
        }
        if (IsNewGamePlusSetup && ScreenId == "new-game-options")
        {
            CancelNewGamePlusSetup?.Invoke();
            return true;
        }
        return false;
    }
}
