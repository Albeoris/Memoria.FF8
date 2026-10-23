namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal void RestoreEditingContext(bool demo, bool fixture)
    {
        IsDemo = demo;
        IsFixtureContext = fixture;
        _view = null;
    }

    private static string ResolveSettingsAlias(string id) => id switch
    {
        "config-display" => "config-frame-limits",
        "debug-ui" => "debug-gallery",
        "debug-audio" => "config-audio",
        "debug-controls" => "debug-shortcuts",
        "config-mods" => "mods",
        _ => id
    };

    private bool ExecuteFixtureAction(MenuAction action)
    {
        if (action.Kind == "demo-reset" && (IsFixtureContext || ScreenId == "debug-gallery"))
        {
            _demo = MenuDemoData.Create();
            demoFontPreferences = new UiPreferences();
            Message = "Preview / Fixture data reset.";
            return true;
        }
        if (!IsFixtureContext)
        {
            return false;
        }
        if (action.Kind == "config")
        {
            if (action.SubjectId is "UI presentation" or "UI font" or "Dialogue font" or "UI font size" or "Dialogue font size" or "Text scale")
            {
                demoFontPreferences = (demoFontPreferences with { MenuPresentation = Presentation }).WithSetting(action.SubjectId, action.Value);
                Presentation = demoFontPreferences.MenuPresentation;
            }
            Data.Settings[action.SubjectId] = action.Value;
            Message = "Preview / Fixture: " + action.SubjectId + " = " + action.Value;
            return true;
        }
        if (action.Kind is "menu-back" or "demo-reset" or "select-character" or "select-gf" or "select-item" or "select-magic" or "select-card" or "select-recipe" or "select-refinement-ability" or "select-slot" or "select-command-slot" or "select-ability-slot" or "select-exchange" or "select-save-slot" or "test-answer" or "test-reset" || action.Kind.StartsWith("controls-", StringComparison.Ordinal) || action.Kind.StartsWith("font-", StringComparison.Ordinal))
        {
            return false;
        }
        ApplyDemo(action);
        Message = "Preview / Fixture: " + Message;
        return true;
    }
}
