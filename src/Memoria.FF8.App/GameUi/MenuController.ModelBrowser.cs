namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    public event Action? ModelBrowserRequested;

    private bool ExecuteModelBrowserAction(MenuAction action)
    {
        if (action.Kind != "open-model-browser")
        {
            return false;
        }
        if (ModelBrowserRequested is null)
        {
            SetSystemMessage(() => SystemText("ui.executemodelbrowseraction.message.the.model.browser.is.not.connected.to.this", "The model browser is not connected to this menu host."));
        }
        else
        {
            ModelBrowserRequested.Invoke();
        }
        return true;
    }
}
