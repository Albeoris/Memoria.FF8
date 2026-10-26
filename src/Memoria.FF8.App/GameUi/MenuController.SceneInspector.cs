namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<bool>? sceneInspectorAvailable;
    private Action? openSceneInspector;

    public bool SceneInspectorAvailable => sceneInspectorAvailable?.Invoke() == true;

    public void ConnectSceneInspector(Func<bool> available, Action open)
    {
        sceneInspectorAvailable = available;
        openSceneInspector = open;
        _view = null;
    }

    private bool ExecuteSceneInspectorAction(MenuAction action)
    {
        if (action.Kind != "open-scene-inspector")
        {
            return false;
        }
        if (SceneInspectorAvailable)
        {
            openSceneInspector?.Invoke();
        }
        else
        {
            Message = SystemText("ui.executesceneinspectoraction.message.scene.inspector.requires.a.loaded.field.scene", "Scene inspector requires a loaded field scene.");
        }
        return true;
    }
}
