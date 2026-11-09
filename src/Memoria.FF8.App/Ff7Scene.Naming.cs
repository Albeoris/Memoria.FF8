using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private MenuController? namingMenus;
    private long namingRequest = -1;
    private readonly Dictionary<int, int> namingPortraits = [];
    private static int nextNamingPortrait;

    public bool EnterText(char character)
    {
        return !focusPaused && !debugMenu && namingMenus?.EnterNameCharacter(character) == true;
    }

    private bool UpdateNamingMenu(KeyboardState keys)
    {
        if (Session!.Host.Process.Menu is not { Completed: false } menu)
        {
            namingMenus?.Close();
            namingRequest = -1;
            return false;
        }
        if (menu.Mode != 6)
        {
            throw new NotSupportedException($"FFVII menu mode {menu.Mode} requires its native shared-menu adapter.");
        }
        if (dialogueCanvas is null || dialoguePreferences is null)
        {
            throw new InvalidOperationException("FFVII naming requires the shared host presentation context.");
        }
        namingMenus ??= new MenuController();
        namingMenus.Presentation = dialoguePreferences().MenuPresentation;
        if (namingRequest != menu.RequestId)
        {
            if (!namingPortraits.TryGetValue(menu.Parameter, out int portraitId))
            {
                portraitId = Interlocked.Increment(ref nextNamingPortrait);
                Ff7MenuResources resources = Ff7MenuResources.LoadPortraits(installation);
                Ff7MenuPortrait portrait = Ff7MenuPortrait.Decode(resources.GetPortrait(menu.Parameter));
                Color[] colors = portrait.Pixels.ToArray().Select(value => Color.FromNonPremultiplied((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24))).ToArray();
                dialogueCanvas.RegisterHostImage(portraitId, UploadTexture(portrait.Width, portrait.Height, colors));
                namingPortraits.Add(menu.Parameter, portraitId);
            }
            string current = Session.Progress.Characters.Single(character => character.Id == menu.Parameter).Name;
            namingMenus.OpenNamePrompt(menu.Parameter, current, name => Session.CompleteNamingMenu(menu.RequestId, name), new NamePromptOptions(9, current, new UiImageSource(UiImageKind.HostArtwork, portraitId), value => value.All(character => character is >= ' ' and <= '~')));
            namingRequest = menu.RequestId;
        }
        foreach ((Keys key, MenuInput input) in new[] { (Keys.Up, MenuInput.Up), (Keys.Down, MenuInput.Down), (Keys.Left, MenuInput.Left), (Keys.Right, MenuInput.Right), (Keys.Tab, MenuInput.Tab), (Keys.PageUp, MenuInput.PageUp), (Keys.PageDown, MenuInput.PageDown), (Keys.Enter, MenuInput.Confirm), (Keys.Escape, MenuInput.Cancel) })
        {
            if (Pressed(keys, key))
            {
                namingMenus.Navigate(input);
            }
        }
        if (!namingMenus.IsTypingName && Pressed(keys, Keys.Z))
        {
            namingMenus.Navigate(MenuInput.Confirm);
        }
        return true;
    }

    private void DrawNamingMenu(Rectangle bounds)
    {
        if (namingMenus?.IsOpen != true)
        {
            return;
        }
        UiPreferences preferences = dialoguePreferences!();
        UiTheme theme = dialogueTheme?.Invoke() ?? (preferences.MenuPresentation == MenuPresentation.Vector ? new VectorUiTheme { FontFamily = preferences.UiFontFamily } : new UiTheme { FontFamily = preferences.UiFontFamily });
        UiView view = namingMenus.BuildView();
        view.Resize(bounds.Width, bounds.Height);
        view.Draw(dialogueCanvas!, theme);
    }

    private void DisposeNamingPresentation()
    {
        foreach (int portrait in namingPortraits.Values)
        {
            dialogueCanvas?.UnregisterHostImage(portrait);
        }
        namingPortraits.Clear();
        namingMenus?.Close();
        namingMenus = null;
        namingRequest = -1;
    }
}
