using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private MenuController? namingMenus;
    private long namingRequest = -1;
    private readonly Dictionary<(SessionGame Game, int Character), int> namingPortraits = [];
    private NativeMenuArtwork? namingArtwork;
    private bool namingDraftEdited;
    private static int nextNamingPortrait;

    public bool EnterText(char character)
    {
        if (focusPaused || debugMenu || namingMenus is null)
        {
            return false;
        }
        string before = namingMenus.NameDraft;
        bool accepted = namingMenus.EnterNameCharacter(character);
        namingDraftEdited |= before != namingMenus.NameDraft;
        return accepted;
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
            string nativeName = Session.Progress.Characters.Single(character => character.Id == menu.Parameter).Name;
            Ff7CharacterDisplayIdentity identity = characterIdentity.Resolve(menu.Parameter, nativeName);
            var portraitKey = (identity.PortraitGame, identity.PortraitCharacter);
            if (!namingPortraits.TryGetValue(portraitKey, out int portraitId))
            {
                portraitId = Interlocked.Increment(ref nextNamingPortrait);
                if (identity.PortraitGame == SessionGame.Ff8)
                {
                    namingArtwork ??= NativeMenuArtwork.Load(identityFf8Directory ?? throw new InvalidOperationException("Mapped character portraits require the selected FFVIII installation."));
                    var portrait = namingArtwork.GetCharacterPortrait(identity.PortraitCharacter);
                    Color[] colors = portrait.Pixels.Select(pixel => Color.FromNonPremultiplied(pixel.R, pixel.G, pixel.B, pixel.A)).ToArray();
                    dialogueCanvas.RegisterHostImage(portraitId, UploadTexture(portrait.Width, portrait.Height, colors));
                }
                else
                {
                    Ff7MenuResources resources = Ff7MenuResources.LoadPortraits(installation);
                    Ff7MenuPortrait portrait = Ff7MenuPortrait.Decode(resources.GetPortrait(identity.PortraitCharacter));
                    Color[] colors = portrait.Pixels.ToArray().Select(value => Color.FromNonPremultiplied((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24))).ToArray();
                    dialogueCanvas.RegisterHostImage(portraitId, UploadTexture(portrait.Width, portrait.Height, colors));
                }
                namingPortraits.Add(portraitKey, portraitId);
            }
            namingDraftEdited = false;
            bool presentationOwned = identity.PresentationOwned;
            namingMenus.OpenNamePrompt(menu.Parameter, identity.Name, name => CompleteIdentityNaming(menu.RequestId, menu.Parameter, name, presentationOwned), new NamePromptOptions(presentationOwned ? GameNames.MaximumLength : 9, presentationOwned ? identity.DefaultName : identity.Name, new UiImageSource(UiImageKind.HostArtwork, portraitId), value => presentationOwned ? GameNames.IsValidDraft(value) : value.All(character => character is >= ' ' and <= '~')));
            namingRequest = menu.RequestId;
        }
        if (semanticInput.MenuVertical != 0)
        {
            NavigateNaming(semanticInput.MenuVertical < 0 ? MenuInput.Up : MenuInput.Down);
        }
        if (semanticInput.MenuHorizontal != 0)
        {
            NavigateNaming(semanticInput.MenuHorizontal < 0 ? MenuInput.Left : MenuInput.Right);
        }
        if (sharedInput.UiTabRequested)
        {
            NavigateNaming(MenuInput.Tab);
        }
        if (semanticInput.Confirm)
        {
            NavigateNaming(MenuInput.Confirm);
        }
        if (semanticInput.Cancel)
        {
            NavigateNaming(MenuInput.Cancel);
        }
        return true;
    }

    private void NavigateNaming(MenuInput input)
    {
        string before = namingMenus!.NameDraft;
        namingMenus.Navigate(input);
        namingDraftEdited |= before != namingMenus.NameDraft;
    }

    private void CompleteIdentityNaming(long request, int character, string name, bool presentationOwned)
    {
        if (!presentationOwned)
        {
            Session!.CompleteNamingMenu(request, name);
            return;
        }
        Ff7Menu menu = Session!.Host.Process.Menu ?? throw new InvalidOperationException("No FFVII naming menu is active.");
        if (menu.Mode != 6 || menu.Parameter != character || menu.RequestId != request || menu.Completed)
        {
            throw new InvalidOperationException("Only the current FFVII naming menu can accept a presentation name.");
        }
        CombinedSessionSave prepared = Ff7CharacterPresentationIdentity.AcceptPresentationName(envelope, character, name, namingDraftEdited);
        Session.Host.CompleteMenu(request);
        envelope = prepared;
        characterIdentity = Ff7CharacterPresentationIdentity.FromSave(envelope);
        commonDialoguePages.Clear();
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
        namingArtwork = null;
        namingDraftEdited = false;
        namingMenus?.Close();
        namingMenus = null;
        namingRequest = -1;
    }
}
