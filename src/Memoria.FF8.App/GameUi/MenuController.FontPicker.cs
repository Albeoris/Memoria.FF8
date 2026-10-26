using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string fontPickerKey = "UI font";
    private string fontSearch = "";
    private string? selectedFontId;

    private UiPreferences FontPreferences => IsFixtureContext ? demoFontPreferences : _readPreferences?.Invoke() ?? demoFontPreferences;

    private string FontDisplayValue(string key)
    {
        UiPreferences preferences = FontPreferences;
        if (key == "Dialogue font")
        {
            return ResolvedFontDisplay(IsFixtureContext || _readPreferences is not null ? preferences.DialogueFontFamily : ConfigValue(key));
        }
        bool themeDefault = (Presentation == MenuPresentation.Vector ? preferences.VectorUiFontFamily : preferences.ClassicUiFontFamily) is null && preferences.LegacyUiFontFamily == SystemFontCatalog.DefaultFamily;
        return FormatSystemText("ui.fontdisplayvalue.details.theme.default.template", "{0}{1}", (themeDefault ? SystemText("ui.fontdisplayvalue.details.theme.default", "Theme default — ") : ""), ResolvedFontDisplay(preferences.GetUiFontFamily(Presentation)));
    }

    private string ResolvedFontDisplay(string requestedFamily)
    {
        string resolved = SystemFontCatalog.ResolveFamily(requestedFamily);
        return string.Equals(requestedFamily, resolved, StringComparison.OrdinalIgnoreCase) ? requestedFamily : FormatSystemText("font.missing-fallback", "{0} — unavailable; using {1}", requestedFamily, resolved);
    }

    private void RegisterFontPickerScreen()
    {
        Register(new MenuScreenDefinition
        {
            Id = "font-picker",
            Title = "Config / Text and fonts / Font family",
            Help = "Search installed families with the keyboard or the Search characters action. Confirm applies; Back returns without changing the family.",
            Entries = c => c.FontPickerEntries(),
            Details = c => [FormatSystemText("ui.registerfontpickerscreen.details.search.template", "Search: {0}", c.fontSearch), FormatSystemText("ui.registerfontpickerscreen.details.current.template", "Current: {0}", c.FontDisplayValue(c.fontPickerKey)), SystemText("ui.registerfontpickerscreen.details.latin.0123456789", "Latin / Кириллица · 0123456789 !?"), SystemText("ui.registerfontpickerscreen.details.families.use.os.shaping.and.fallback", "Families use OS shaping and fallback.")]
        });
        Register(new MenuScreenDefinition
        {
            Id = "font-search",
            Title = "Search font families",
            Help = "Choose characters to filter installed font families.",
            Columns = 8,
            Entries = _ => [.. "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -".Select((letter, index) => new MenuEntry("letter-" + index, letter.ToString(), SystemText("ui.registerfontpickerscreen.entry.help.add.search.character", "Add search character."), Action: new("font-search-letter", Value: letter.ToString()))), new("delete", SystemText("ui.registerfontpickerscreen.entry.label.delete", "Delete"), SystemText("ui.registerfontpickerscreen.entry.help.remove.search.character", "Remove search character."), Action: new("font-search-delete")), new("clear", SystemText("ui.registerfontpickerscreen.entry.label.clear", "Clear"), SystemText("ui.registerfontpickerscreen.entry.help.clear.search", "Clear search."), Action: new("font-search-clear")), new("done", SystemText("ui.registerfontpickerscreen.entry.label.done", "Done"), SystemText("ui.registerfontpickerscreen.entry.help.return.to.matching.families", "Return to matching families."), Action: new("menu-back"))],
            Details = c => [FormatSystemText("ui.registerfontpickerscreen.details.search.template", "Search: {0}", c.fontSearch)]
        });
    }

    private IReadOnlyList<MenuEntry> FontPickerEntries()
    {
        List<MenuEntry> entries = [new("font-search", SystemText("ui.fontpickerentries.entry.label.search.characters", "Search characters…"), SystemText("ui.fontpickerentries.entry.help.type.to.filter.or.choose.characters.with.a", "Type to filter, or choose characters with a controller."), "font-search"), new("font-clear", SystemText("ui.fontpickerentries.entry.label.clear.search", "Clear search"), SystemText("ui.fontpickerentries.entry.help.show.all.installed.families", "Show all installed families."), Action: new("font-search-clear"))];
        foreach (string family in SystemFontCatalog.Families.Where(family => family.Contains(fontSearch, StringComparison.OrdinalIgnoreCase)))
        {
            entries.Add(new("family:" + family, family, FormatSystemText("ui.fontpickerentries.entry.help.apply.this.family.menu.and.dialogue.sizes.remain.template", "{0}. Apply this family. Menu and dialogue sizes remain independent.", family), Action: new("font-picker-select", fontPickerKey, Value: family)));
        }
        return entries;
    }

    public bool EnterFontSearchCharacter(char character)
    {
        if (!IsOpen || ScreenId != "font-search")
        {
            return false;
        }
        SetFontSearch(character == '\b' ? fontSearch[..Math.Max(0, fontSearch.Length - 1)] : char.IsControl(character) ? fontSearch : fontSearch + character);
        return true;
    }

    private void SetFontSearch(string value)
    {
        selectedFontId = SelectedEntry?.Id?.StartsWith("family:", StringComparison.Ordinal) == true ? SelectedEntry.Id : selectedFontId;
        fontSearch = value.Length <= 100 ? value : value[..100];
        int index = FontPickerEntries().ToList().FindIndex(entry => entry.Id == selectedFontId);
        _selections["font-picker"] = Math.Max(0, index);
        _view = null;
    }

    private bool ExecuteFontPickerAction(MenuAction action)
    {
        switch (action.Kind)
        {
            case "font-picker-open":
                fontPickerKey = action.SubjectId;
                fontSearch = "";
                selectedFontId = "family:" + ConfigValue(fontPickerKey);
                Push("font-picker");
                _selections[ScreenId] = Math.Max(0, Entries.ToList().FindIndex(entry => entry.Id == selectedFontId));
                return true;
            case "font-picker-select":
                if (ScreenId != "font-picker" || action.SubjectId != fontPickerKey || !SystemFontCatalog.Families.Contains(action.Value))
                {
                    return true;
                }
                Execute(new("config", fontPickerKey, Value: action.Value));
                Back();
                return true;
            case "font-search-letter":
                SetFontSearch(fontSearch + action.Value);
                return true;
            case "font-search-delete":
                SetFontSearch(fontSearch[..Math.Max(0, fontSearch.Length - 1)]);
                return true;
            case "font-search-clear":
                SetFontSearch("");
                return true;
            default:
                return false;
        }
    }
}
