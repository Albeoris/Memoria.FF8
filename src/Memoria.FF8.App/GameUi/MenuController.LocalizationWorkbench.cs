namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    public Func<IReadOnlyList<string>>? ReadNativeFontProfiles { get; set; }
    private Action<string>? localizationWorkbenchAction;
    public void ConnectLocalizationWorkbench(Func<IReadOnlyList<MenuEntry>> entries, Action<string> action)
    {
        localizationWorkbenchAction = action;
        Register(new MenuScreenDefinition
        {
            Id = "localization-workbench",
            Title = "Localization",
            Help = "Choose the editing project. Authoring does not change package priority or enable state.",
            Entries = _ => entries(),
            GalleryEligible = false
        });
    }

    private bool ExecuteLocalizationWorkbenchAction(MenuAction action)
    {
        if (action.Kind != "localization-workbench")
        {
            return false;
        }
        localizationWorkbenchAction?.Invoke(action.SubjectId);
        return true;
    }
}
