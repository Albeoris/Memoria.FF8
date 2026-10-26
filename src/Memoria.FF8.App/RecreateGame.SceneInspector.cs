using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private SceneInspectorController? sceneInspector;
    private bool previousSceneInspectorMouse;
    private int previousSceneInspectorWheel;

    private bool SceneInspectorAvailable => world is not null && session.State.Scene == SceneKind.Field && this.field is not null && this.field.Id == session.State.FieldId && world.CanPresentCurrentField && ActiveBattle is null && world.ActiveTripleTriad is null && !fieldFaulted && !fieldRenderFaulted;

    private SceneInspectorSnapshot? ReadSceneInspectorSnapshot()
    {
        return SceneInspectorAvailable ? world!.ReadSceneInspection() : null;
    }

    private void OpenSceneInspector()
    {
        if (!SceneInspectorAvailable)
        {
            return;
        }
        gameMenus?.Close();
        sceneInspector = new SceneInspectorController(ReadSceneInspectorSnapshot, command => world!.ApplySceneInspection(command), CloseSceneInspector);
        sceneInspector.LocalizeText = LocalizeUiText;
        sceneInspector.SoundRequested += sound => fieldAudio?.PlayUiSound(sound);
        MouseState mouse = Mouse.GetState();
        previousSceneInspectorMouse = mouse.LeftButton == ButtonState.Pressed;
        previousSceneInspectorWheel = mouse.ScrollWheelValue;
        SynchronizeUiMoviePause();
    }

    private void CloseSceneInspector()
    {
        sceneInspector = null;
        gameMenus?.Open("debug-room", true);
        drainUiButtons = true;
        SynchronizeUiMoviePause();
    }

    private bool UpdateSceneInspectorUi(InputFrame input)
    {
        if (sceneInspector is null)
        {
            return false;
        }
        sceneInspector.Refresh();
        if (sceneInspector.Snapshot is null || keyboard.DebugRoomRequested || keyboard.GameMenuRequested)
        {
            CloseSceneInspector();
            return true;
        }
        if (keyboard.UiTabRequested)
        {
            sceneInspector.Navigate(MenuInput.Tab);
        }
        if (keyboard.UiDetailRequested)
        {
            sceneInspector.Navigate(MenuInput.Alternate);
        }
        NavigateBattleView(sceneInspector.Navigate, input);
        if (sceneInspector is not null)
        {
            MouseState mouse = Mouse.GetState();
            int wheel = mouse.ScrollWheelValue - previousSceneInspectorWheel;
            previousSceneInspectorWheel = mouse.ScrollWheelValue;
            if (IsActive && wheel != 0)
            {
                int distance = Math.Clamp(Math.Abs(wheel / 120), 1, 3);
                sceneInspector.Select(sceneInspector.SelectedIndex - Math.Sign(wheel) * distance);
            }
            bool pressed = IsActive && mouse.LeftButton == ButtonState.Pressed;
            UiView view = sceneInspector.BuildView();
            view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            view.Pointer(mouse.X, mouse.Y, pressed && !previousSceneInspectorMouse);
            previousSceneInspectorMouse = pressed;
        }
        return true;
    }

    private void DrawSceneInspectorUi(Rectangle bounds)
    {
        if (sceneInspector is not null && uiCanvas is not null)
        {
            UiView view = sceneInspector.BuildView();
            view.Resize(bounds.Width, bounds.Height);
            view.Draw(uiCanvas, uiTheme);
        }
    }

    internal static Rectangle SceneInspectorPreviewBounds(Rectangle viewport)
    {
        float scale = Math.Min(viewport.Width / 960f, viewport.Height / 672f);
        float offsetX = (viewport.Width - 960 * scale) / 2;
        float offsetY = (viewport.Height - 672 * scale) / 2;
        UiRect preview = SceneInspectorElement.PreviewBounds;
        return new Rectangle(viewport.X + (int)Math.Round(offsetX + preview.X * scale), viewport.Y + (int)Math.Round(offsetY + preview.Y * scale), (int)Math.Round(preview.Width * scale), (int)Math.Round(preview.Height * scale));
    }

    private bool TryDispatchSceneInspectorControl(ControlRequest request, UiControlCommand command)
    {
        if (sceneInspector is null)
        {
            return false;
        }
        sceneInspector.Refresh();
        if (request.Command.Equals("ui-close", StringComparison.OrdinalIgnoreCase))
        {
            CloseSceneInspector();
        }
        else if (request.Command.Equals("ui-open", StringComparison.OrdinalIgnoreCase) || request.Command.Equals("debug-room", StringComparison.OrdinalIgnoreCase))
        {
            sceneInspector = null;
            return false;
        }
        else if (request.Command.Equals("ui-input", StringComparison.OrdinalIgnoreCase))
        {
            if (command.Input is MenuInput input)
            {
                sceneInspector.Navigate(input);
            }
            if (sceneInspector is not null && command.X is float x && command.Y is float y)
            {
                UiView view = sceneInspector.BuildView();
                view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                view.Pointer(x, y, command.Pressed && !uiControlPointerPressed);
                uiControlPointerPressed = command.Pressed;
            }
        }
        else if (!request.Command.Equals("ui-status", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }

    private object? SceneInspectorUiStatus()
    {
        return sceneInspector is null ? null : new { sceneInspector.Kind, sceneInspector.Filter, sceneInspector.Focus, sceneInspector.SelectedIndex, SelectedId = sceneInspector.Selected?.Id, sceneInspector.Page, sceneInspector.PageCount, sceneInspector.DetailPage, sceneInspector.DetailPageCount, sceneInspector.Message, FieldId = sceneInspector.Snapshot?.FieldId, FieldName = sceneInspector.Snapshot?.FieldName, Frame = sceneInspector.Snapshot?.Frame, Entries = sceneInspector.Entries.Select(entry => new { entry.Id, entry.Name, entry.Enabled }).ToArray() };
    }
}
