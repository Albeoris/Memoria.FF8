using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

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

    private bool UpdateNamingMenu()
    {
        if (Session!.Host.Process.Menu is not { Mode: 6, Completed: false } menu)
        {
            namingMenus?.Close();
            namingRequest = -1;
            return false;
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
        if (semanticInput.MenuVertical != 0)
        {
            namingMenus.Navigate(semanticInput.MenuVertical < 0 ? MenuInput.Up : MenuInput.Down);
        }
        if (semanticInput.MenuHorizontal != 0)
        {
            namingMenus.Navigate(semanticInput.MenuHorizontal < 0 ? MenuInput.Left : MenuInput.Right);
        }
        if (sharedInput.UiTabRequested)
        {
            namingMenus.Navigate(MenuInput.Tab);
        }
        if (semanticInput.Confirm)
        {
            namingMenus.Navigate(MenuInput.Confirm);
        }
        if (semanticInput.Cancel)
        {
            namingMenus.Navigate(MenuInput.Cancel);
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
