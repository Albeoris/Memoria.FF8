using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal sealed record BattleLoadoutEditorCharacter(int CharacterId, string Name, string Summary);
internal sealed record BattleLoadoutEditorWeapon(int WeaponId, string Name, string Owner, string Description, string Family = "");
internal sealed record BattleLoadoutEditorContext(BattleLoadoutDraft Draft, BattleLoadoutEditorCharacter[] Characters, BattleLoadoutEditorWeapon[] Weapons, Func<int, int, string?>? WeaponReason = null);

public sealed partial class MenuController
{
    private Func<BattleLoadoutEditorContext>? readBattleLoadoutEditor;
    private Action<BattleLoadoutDraft>? applyBattleLoadoutEditor;
    private Func<string?>? battleLoadoutEditorBoundary;
    private Func<bool>? canOpenBattleLoadoutEditor;
    private Func<BattleLoadoutDraft, int, string[]>? describeBattleLoadout;
    private Func<BattleLoadoutDraft, int, BattleLimitFamily, string[]>? describeBattleLoadoutFamily;
    private BattleLoadoutEditorContext? battleLoadoutEditor;
    private int battleLoadoutCharacter;
    private BattleLimitFamily battleLoadoutFamily;
    private string battleLoadoutExchange = "";

    internal bool BattleLoadoutEditorAvailable => readBattleLoadoutEditor is not null && !IsFixtureContext && (canOpenBattleLoadoutEditor?.Invoke() ?? true);

    internal string BattleLoadoutEditorEntryHelp => BattleLoadoutEditorAvailable ? LoadoutText("open-help", "Edit current FFVIII weapons and ordered Limit families with Apply and Cancel.") : battleLoadoutEditorBoundary?.Invoke() ?? LoadoutText("unavailable", "A current FFVIII game is required; the laboratory stays isolated.");

    internal void ConnectBattleLoadoutEditor(Func<BattleLoadoutEditorContext> read, Action<BattleLoadoutDraft> apply, Func<string?> boundary, Func<BattleLoadoutDraft, int, string[]> describe, Func<bool>? available = null, Func<BattleLoadoutDraft, int, BattleLimitFamily, string[]>? familyStatus = null)
    {
        readBattleLoadoutEditor = read;
        canOpenBattleLoadoutEditor = available;
        applyBattleLoadoutEditor = apply;
        battleLoadoutEditorBoundary = boundary;
        describeBattleLoadout = describe;
        describeBattleLoadoutFamily = familyStatus;
    }

    private string LoadoutText(string key, string original)
    {
        return SystemText("debug.loadout." + key, original);
    }

    private void RegisterBattleLoadoutScreens()
    {
        Register(new()
        {
            Id = "debug-loadouts",
            Title = "Weapons & Limit Breaks",
            Help = "Current FFVIII game · Stage changes, then Apply or Cancel.",
            Entries = c => c.BattleLoadoutRosterEntries(),
            Details = c => c.BattleLoadoutReviewDetails()
        });
        Register(new()
        {
            Id = "debug-loadout-character",
            Layout = MenuLayout.Character,
            Title = "Weapons & Limit Breaks · Character",
            Help = "Changes remain in the draft until Apply.",
            Entries = c => c.BattleLoadoutCharacterEntries(),
            Details = c => c.BattleLoadoutCharacterDetails()
        });
        Register(new()
        {
            Id = "debug-loadout-weapons",
            Title = "Weapons & Limit Breaks · Weapon catalog",
            Help = "Choose a source weapon and its combat style.",
            Entries = c => c.battleLoadoutEditor?.Weapons.OrderBy(weapon => weapon.Family).ThenBy(weapon => weapon.Owner).ThenBy(weapon => weapon.WeaponId).Select(weapon => new MenuEntry("weapon-" + weapon.WeaponId, (weapon.Family.Length == 0 ? "" : weapon.Family + " · ") + weapon.Owner + " · " + weapon.Name, weapon.Description + " " + (c.battleLoadoutEditor!.WeaponReason?.Invoke(c.battleLoadoutCharacter, weapon.WeaponId) ?? ""), Action: new("loadout-weapon", Value: weapon.WeaponId.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ToArray() ?? [],
            Details = c => c.BattleLoadoutCharacterDetails()
        });
        Register(new()
        {
            Id = "debug-loadout-families",
            Title = "Weapons & Limit Breaks · Families",
            Help = "Choose a family to add, remove, move or reorder.",
            Entries = c => c.BattleLoadoutFamilyCatalog(),
            Details = c => c.BattleLoadoutFamilyDetails()
        });
        Register(new()
        {
            Id = "debug-loadout-family",
            Title = "Weapons & Limit Breaks · Family actions",
            Help = "Assignments reference shared learning; removing a family keeps its progress.",
            Entries = c => c.BattleLoadoutFamilyEntries(),
            Details = c => c.BattleLoadoutFamilyDetails()
        });
        Register(new()
        {
            Id = "debug-loadout-exchange",
            Title = "Weapons & Limit Breaks · Exchange",
            Help = "Choose a different character. Both changes are staged together.",
            Entries = c => c.battleLoadoutEditor?.Characters.Select(character => new MenuEntry("exchange-" + character.CharacterId, character.Name, character.CharacterId == c.battleLoadoutCharacter ? c.LoadoutText("distinct", "Choose another character.") : c.LoadoutText("exchange-help", "Stage both participants; review and Apply from the main editor."), Action: new("loadout-exchange-target", Value: character.CharacterId.ToString(System.Globalization.CultureInfo.InvariantCulture)), Enabled: character.CharacterId != c.battleLoadoutCharacter)).ToArray() ?? [],
            Details = c => c.BattleLoadoutReviewDetails()
        });
    }

    private string FamilyLabel(BattleLimitFamily family)
    {
        string original = family switch
        {
            BattleLimitFamily.FireCross => "Fire Cross / No Mercy",
            BattleLimitFamily.BloodPain => "Blood Pain",
            BattleLimitFamily.MassiveAnchor => "Massive Anchor",
            BattleLimitFamily.AngelWing => "Angel Wing",
            BattleLimitFamily.IceStrike => "Ice Strike",
            BattleLimitFamily.BlueMagic => "Blue Magic",
            _ => family.ToString()
        };
        return LoadoutText("family." + family, original);
    }

    private string FamilyRequirement(BattleLimitFamily family)
    {
        BattleWeaponCapabilities requirement = BattleLimitFamilyRules.RequiredWeapon(family);
        return requirement == BattleWeaponCapabilities.None ? LoadoutText("weapon-independent", "No weapon requirement. Crisis and action rules apply in battle.") : FormatSystemText("debug.loadout.requirement", "Required weapon: {0}. Crisis and action rules apply in battle.", BattleLoadoutLabels.Capabilities(requirement, localizeInterface));
    }

    private IReadOnlyList<MenuEntry> BattleLoadoutFamilyCatalog()
    {
        BattleLoadoutEditorContext? editor = battleLoadoutEditor;
        if (editor is null)
        {
            return [];
        }
        BattleLimitFamily[] assigned = editor.Draft.ResolveFamilies(battleLoadoutCharacter);
        return assigned.Concat(Enum.GetValues<BattleLimitFamily>().Except(assigned)).Select(family =>
        {
            int order = Array.IndexOf(assigned, family);
            string label = FamilyLabel(family) + (order >= 0 ? FormatSystemText("debug.loadout.order", " · #{0}", order + 1) : "");
            string help = FamilyRequirement(family) + " " + string.Join(" ", (describeBattleLoadoutFamily?.Invoke(editor.Draft, battleLoadoutCharacter, family) ?? []).Take(2));
            return new MenuEntry(family.ToString(), label, help, Action: new("loadout-family", Value: family.ToString()));
        }).ToArray();
    }

    private string[] BattleLoadoutFamilyDetails()
    {
        if (battleLoadoutEditor is null)
        {
            return [];
        }
        BattleLimitFamily selected = ScreenId == "debug-loadout-families" && SelectedEntry?.Action is { } action && Enum.TryParse(action.Value, out BattleLimitFamily family) ? family : battleLoadoutFamily;
        return [FamilyLabel(selected), FamilyRequirement(selected), .. describeBattleLoadoutFamily?.Invoke(battleLoadoutEditor.Draft, battleLoadoutCharacter, selected) ?? []];
    }

    private IReadOnlyList<MenuEntry> BattleLoadoutRosterEntries()
    {
        List<MenuEntry> entries = battleLoadoutEditor?.Characters.Select(character => new MenuEntry("character-" + character.CharacterId, character.Name + (battleLoadoutEditor.Draft.ChangedCharacters.Contains(character.CharacterId) ? " *" : ""), character.Summary, Action: new("loadout-character", Value: character.CharacterId.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ToList() ?? [];
        string? reason = battleLoadoutEditorBoundary?.Invoke();
        entries.Add(new("apply", LoadoutText("apply", "Apply"), reason ?? LoadoutText("retain", "Apply the complete draft. Save normally to retain changes."), Action: new("loadout-apply"), Enabled: battleLoadoutEditor is not null && reason is null));
        entries.Add(new("cancel", LoadoutText("cancel", "Cancel"), LoadoutText("discard", "Discard the complete draft."), Action: new("loadout-cancel")));
        return entries;
    }

    private IReadOnlyList<MenuEntry> BattleLoadoutCharacterEntries()
    {
        return
        [
            new("weapon", LoadoutText("weapon", "Assign Weapon"), LoadoutText("weapon-help", "Choose installed equipment without changing Limit assignments."), "debug-loadout-weapons"),
            new("families", LoadoutText("families", "Limit families"), LoadoutText("families-help", "Add, remove, move and reorder assigned families."), "debug-loadout-families"),
            new("remove-all", LoadoutText("remove-all", "Remove All Limits"), LoadoutText("empty-help", "Stage an explicit empty family set. Learned progress is preserved."), Action: new("loadout-remove-all")),
            new("swap-weapons", LoadoutText("swap-weapons", "Swap Weapons"), LoadoutText("swap-help", "Exchange the effective assignments of two characters."), Action: new("loadout-exchange", Value: "weapons")),
            new("swap-families", LoadoutText("swap-families", "Swap Limit Sets"), LoadoutText("swap-help", "Exchange the effective assignments of two characters."), Action: new("loadout-exchange", Value: "families")),
            new("reset", LoadoutText("reset", "Reset Character"), LoadoutText("reset-help", "Stage removal of overrides. Native defaults and learned progress are preserved."), Action: new("loadout-reset")),
            new("review", LoadoutText("review", "Review draft"), LoadoutText("review-help", "Review all participants before Apply."), Action: new("menu-back"))
        ];
    }

    private IReadOnlyList<MenuEntry> BattleLoadoutFamilyEntries()
    {
        bool assigned = battleLoadoutEditor?.Draft.ResolveFamilies(battleLoadoutCharacter).Contains(battleLoadoutFamily) == true;
        return
        [
            new("toggle", assigned ? LoadoutText("remove", "Remove Limit") : LoadoutText("add", "Add Limit"), FamilyRequirement(battleLoadoutFamily), Action: new("loadout-toggle-family")),
            new("up", LoadoutText("up", "Move up"), LoadoutText("order-help", "Change selector order without changing learned progress."), Action: new("loadout-order", Value: "-1"), Enabled: assigned),
            new("down", LoadoutText("down", "Move down"), LoadoutText("order-help", "Change selector order without changing learned progress."), Action: new("loadout-order", Value: "1"), Enabled: assigned),
            new("move", LoadoutText("move", "Move Limit"), LoadoutText("move-help", "Remove this family here and add it to another character; existing destination copies are kept once."), Action: new("loadout-exchange", Value: "move"), Enabled: assigned)
        ];
    }

    private string[] BattleLoadoutCharacterDetails()
    {
        if (battleLoadoutEditor is null)
        {
            return [];
        }
        BattleLoadoutEditorCharacter? character = battleLoadoutEditor.Characters.FirstOrDefault(entry => entry.CharacterId == battleLoadoutCharacter);
        return [LoadoutText("current", "Current FFVIII game · Draft"), character?.Name ?? "", .. describeBattleLoadout?.Invoke(battleLoadoutEditor.Draft, battleLoadoutCharacter) ?? []];
    }

    private string[] BattleLoadoutReviewDetails()
    {
        if (battleLoadoutEditor is null)
        {
            return [];
        }
        string changed = string.Join(", ", battleLoadoutEditor.Characters.Where(character => battleLoadoutEditor.Draft.ChangedCharacters.Contains(character.CharacterId)).Select(character => character.Name));
        List<string> details = [LoadoutText("current", "Current FFVIII game · Draft"), changed.Length == 0 ? LoadoutText("unchanged", "No staged changes.") : FormatSystemText("debug.loadout.changed", "Changed: {0}", changed)];
        int selectedCharacter = SelectedEntry?.Action is { Kind: "loadout-character" } action && int.TryParse(action.Value, out int identity) ? identity : battleLoadoutCharacter;
        BattleLoadoutEditorCharacter? selected = battleLoadoutEditor.Characters.FirstOrDefault(character => character.CharacterId == selectedCharacter);
        details.Add(selected?.Name ?? "");
        details.AddRange(describeBattleLoadout?.Invoke(battleLoadoutEditor.Draft, selectedCharacter) ?? []);
        return details.ToArray();
    }

    private void PreserveBattleLoadoutFamilyFocus()
    {
        int index = BattleLoadoutFamilyCatalog().ToList().FindIndex(entry => entry.Id == battleLoadoutFamily.ToString());
        if (index >= 0)
        {
            _selections["debug-loadout-families"] = index;
        }
    }

    private bool ExecuteBattleLoadoutAction(MenuAction action)
    {
        if (!action.Kind.StartsWith("loadout-", StringComparison.Ordinal))
        {
            return false;
        }
        if (action.Kind == "loadout-cancel")
        {
            battleLoadoutEditor = null;
            if (ScreenId.StartsWith("debug-loadout", StringComparison.Ordinal))
            {
                Back();
            }
            return true;
        }
        if (!BattleLoadoutEditorAvailable)
        {
            Message = BattleLoadoutEditorEntryHelp;
            return true;
        }
        try
        {
            if (action.Kind == "loadout-open")
            {
                battleLoadoutEditor = readBattleLoadoutEditor!();
                battleLoadoutCharacter = battleLoadoutEditor.Characters.FirstOrDefault()?.CharacterId ?? 0;
                Push("debug-loadouts");
                return true;
            }
            if (battleLoadoutEditor is null || !ScreenId.StartsWith("debug-loadout", StringComparison.Ordinal))
            {
                return true;
            }
            BattleLoadoutDraft draft = battleLoadoutEditor.Draft;
            switch (action.Kind)
            {
                case "loadout-character":
                    battleLoadoutCharacter = int.Parse(action.Value, System.Globalization.CultureInfo.InvariantCulture);
                    _characterId = Data.Characters.FirstOrDefault(character => character.NativeId == battleLoadoutCharacter)?.Id ?? _characterId;
                    Push("debug-loadout-character");
                    break;
                case "loadout-weapon":
                    draft.AssignWeapon(battleLoadoutCharacter, int.Parse(action.Value, System.Globalization.CultureInfo.InvariantCulture));
                    Back();
                    break;
                case "loadout-family":
                    if (!Enum.TryParse(action.Value, out BattleLimitFamily selectedFamily) || !Enum.IsDefined(selectedFamily))
                    {
                        throw new InvalidDataException("Unknown Limit family.");
                    }
                    battleLoadoutFamily = selectedFamily;
                    Push("debug-loadout-family");
                    break;
                case "loadout-toggle-family":
                    if (draft.ResolveFamilies(battleLoadoutCharacter).Contains(battleLoadoutFamily))
                    {
                        draft.RemoveLimit(battleLoadoutCharacter, battleLoadoutFamily);
                    }
                    else
                    {
                        draft.AddLimit(battleLoadoutCharacter, battleLoadoutFamily);
                    }
                    break;
                case "loadout-order":
                    draft.ReorderLimit(battleLoadoutCharacter, battleLoadoutFamily, int.Parse(action.Value, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "loadout-remove-all":
                    draft.RemoveAllLimits(battleLoadoutCharacter);
                    break;
                case "loadout-reset":
                    draft.ResetCharacter(battleLoadoutCharacter);
                    break;
                case "loadout-exchange":
                    battleLoadoutExchange = action.Value;
                    Push("debug-loadout-exchange");
                    break;
                case "loadout-exchange-target":
                    int destination = int.Parse(action.Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (battleLoadoutExchange == "weapons")
                    {
                        draft.SwapWeapons(battleLoadoutCharacter, destination);
                    }
                    else if (battleLoadoutExchange == "families")
                    {
                        draft.SwapLimitSets(battleLoadoutCharacter, destination);
                    }
                    else
                    {
                        draft.MoveLimit(battleLoadoutCharacter, destination, battleLoadoutFamily);
                    }
                    Back();
                    break;
                case "loadout-apply":
                    applyBattleLoadoutEditor!(draft);
                    battleLoadoutEditor = null;
                    Back();
                    InvalidateLiveData();
                    Message = LoadoutText("applied", "Loadouts applied. Save the game to retain them.");
                    break;
                case "loadout-cancel":
                    battleLoadoutEditor = null;
                    Back();
                    break;
            }
            if (action.Kind is "loadout-toggle-family" or "loadout-order" or "loadout-exchange-target" or "loadout-remove-all" or "loadout-reset")
            {
                PreserveBattleLoadoutFamilyFocus();
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            Message = exception.Message;
        }
        return true;
    }
}
