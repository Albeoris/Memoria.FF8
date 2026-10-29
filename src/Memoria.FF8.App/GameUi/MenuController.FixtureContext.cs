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
            SetSystemMessage(() => SystemText("ui.executefixtureaction.message.preview.fixture.data.reset", "Preview / Fixture data reset."));
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
            SetSystemMessage(() => FormatSystemText("ui.executefixtureaction.message.preview.fixture.template", "Preview / Fixture: {0} = {1}", action.SubjectId, action.Value));
            return true;
        }
        if (action.Kind is "menu-back" or "demo-reset" or "select-character" or "select-gf" or "select-item" or "select-magic" or "select-card" or "select-recipe" or "select-refinement-ability" or "select-slot" or "select-command-slot" or "select-ability-slot" or "select-exchange" or "select-save-slot" or "test-answer" or "test-reset" || action.Kind.StartsWith("controls-", StringComparison.Ordinal) || action.Kind.StartsWith("font-", StringComparison.Ordinal))
        {
            return false;
        }
        if (!ApplyDemo(action))
        {
            return false;
        }
        string message = Message;
        SetSystemMessage(() => FormatSystemText("ui.executefixtureaction.message.preview.fixture.template.preview.fixture.0", "Preview / Fixture: {0}", message));
        return true;
    }
}
