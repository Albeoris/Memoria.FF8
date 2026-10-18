using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Loads original artwork on demand; menu panels remain procedural.</summary>
internal sealed class MenuArtworkProvider(string gameDirectory)
{
    private NativeMenuArtwork? portraits;
    private CardArtwork? cards;
    private NativeTutorialArtwork? tutorials;
    private NativeMagazineArtwork? magazines;
    private NativeTripleTriadArtwork? tripleTriad;
    private TripleTriadCatalog? cardDefinitions;
    private CardFaceComposer? cardComposer;
    private string? cardFontFamily;
    private readonly Dictionary<(int Id, int Owner), PixelImage> cardFaces = [];

    public PixelImage Read(UiImageSource source, string fontFamily = SystemFontCatalog.DefaultFamily)
    {
        return source.Kind switch
        {
            UiImageKind.CharacterPortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory)).GetCharacterPortrait(source.Id),
            UiImageKind.GuardianForcePortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory)).GetGuardianForcePortrait(source.Id),
            UiImageKind.AngeloPortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory)).AngeloPortrait,
            UiImageKind.Card => (cards ??= CardArtwork.Load(gameDirectory)).GetFront(source.Id),
            UiImageKind.CardElement => (tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory)).GetElement((TripleTriadElement)source.Id),
            UiImageKind.CardFace => ReadCardFace(source.Id, source.Palette, fontFamily),
            UiImageKind.CardBack => (tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory)).Back,
            UiImageKind.TripleTriadBackground => (tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory)).Background,
            UiImageKind.TripleTriadBoardBackground => (tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory)).BoardBackground,
            UiImageKind.Icon => (portraits ??= NativeMenuArtwork.Load(gameDirectory)).GetIcon(source.Id, source.Palette),
            UiImageKind.NativeTutorialPage => (tutorials ??= NativeTutorialArtwork.Load(gameDirectory)).Read(source.Id),
            UiImageKind.Magazine => (magazines ??= NativeMagazineArtwork.Load(gameDirectory)).GetImage(source.Id),
            UiImageKind.MagazineButton => (magazines ??= NativeMagazineArtwork.Load(gameDirectory)).GetButtonImage(source.Id),
            _ => throw new NotSupportedException($"Unsupported menu artwork kind {source.Kind}.")
        };
    }

    private PixelImage ReadCardFace(int cardId, int owner, string fontFamily)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cardId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cardId, CardArtwork.CardCount);
        if (cardFontFamily != fontFamily)
        {
            cardComposer = new CardFaceComposer(fontFamily);
            cardFontFamily = fontFamily;
            cardFaces.Clear();
        }
        if (!cardFaces.TryGetValue((cardId, owner), out PixelImage? image))
        {
            tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory);
            cardDefinitions ??= TripleTriadCatalog.Load(gameDirectory);
            var card = cardDefinitions.Cards[cardId];
            PixelImage? element = card.Element == TripleTriadElement.None ? null : tripleTriad.GetElement(card.Element);
            image = cardComposer!.Compose(tripleTriad.GetFront(cardId), card, owner, element);
            cardFaces.Add((cardId, owner), image);
        }
        return image;
    }
}
