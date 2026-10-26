using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Memoria.FF8.Resources;

namespace Memoria.FF8.GameUi;

/// <summary>Loads original artwork on demand; menu panels remain procedural.</summary>
internal sealed class MenuArtworkProvider(string gameDirectory, ResourceService? resources = null)
{
    private NativeMenuArtwork? portraits;
    private CardArtwork? cards;
    private NativeTutorialArtwork? tutorials;
    private NativeMagazineArtwork? magazines;
    private NativeTripleTriadArtwork? tripleTriad;
    private TripleTriadCatalog? cardDefinitions;
    private CardFaceComposer? cardComposer;
    private string? cardFontFamily;
    private PixelImage? silverCrest;
    private readonly Dictionary<(int Id, int Owner, bool Grayscale), PixelImage> cardFaces = [];

    private long artworkGeneration = -1;

    public long Generation => resources?.Generation ?? 0;

    internal static bool IsCardImage(UiImageSource source) => source.Kind is UiImageKind.TripleTriadOutcome or UiImageKind.Card or UiImageKind.CardElement or UiImageKind.CardFace or UiImageKind.CardFaceGrayscale or UiImageKind.CardBack or UiImageKind.TripleTriadBackground or UiImageKind.TripleTriadBoardBackground;

    internal static bool IsResourceImage(UiImageSource source) => IsCardImage(source) || source.Kind is UiImageKind.CharacterPortrait or UiImageKind.GuardianForcePortrait or UiImageKind.AngeloPortrait or UiImageKind.Icon or UiImageKind.NativeTutorialPage or UiImageKind.Magazine or UiImageKind.MagazineButton or UiImageKind.ControllerButton;

    public PixelImage? ReadControllerButton(int nativeIconId)
    {
        if (resources?.Source.Layouts.Contains("classic-ffnx", StringComparer.Ordinal) != true)
        {
            return null;
        }
        Refresh();
        portraits ??= NativeMenuArtwork.Load(gameDirectory, resources);
        return portraits.HasControllerArtwork ? portraits.GetControllerButtonIcon(nativeIconId) : null;
    }

    public PixelImage Read(UiImageSource source, string? fontFamily = null)
    {
        Refresh();
        return ReadCurrent(source, fontFamily ?? SystemFontCatalog.DefaultFamily);
    }

    private void Refresh()
    {
        if (artworkGeneration != Generation)
        {
            portraits = null;
            magazines = null;
            tutorials = null;
            cards = null;
            tripleTriad = null;
            cardFaces.Clear();
            artworkGeneration = Generation;
        }
    }

    private PixelImage ReadCurrent(UiImageSource source, string fontFamily)
    {
        return source.Kind switch
        {
            UiImageKind.ControllerButton => (portraits ??= NativeMenuArtwork.Load(gameDirectory, resources)).GetControllerButtonIcon(source.Id),
            UiImageKind.SilverCrest => silverCrest ??= ReadSilverCrest(),
            UiImageKind.CharacterPortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory, resources)).GetCharacterPortrait(source.Id),
            UiImageKind.GuardianForcePortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory, resources)).GetGuardianForcePortrait(source.Id),
            UiImageKind.AngeloPortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory, resources)).AngeloPortrait,
            UiImageKind.Card => (cards ??= CardArtwork.Load(gameDirectory, resources)).GetFront(source.Id),
            UiImageKind.CardElement => (tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory, resources)).GetElement((TripleTriadElement)source.Id),
            UiImageKind.CardFace => ReadCardFace(source.Id, source.Palette, fontFamily),
            UiImageKind.CardFaceGrayscale => ReadCardFace(source.Id, source.Palette, fontFamily, grayscale: true),
            UiImageKind.TripleTriadOutcome => (tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory, resources)).GetOutcomeBanner(source.Id),
            UiImageKind.CardBack => (tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory, resources)).Back,
            UiImageKind.TripleTriadBackground => (tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory, resources)).Background,
            UiImageKind.TripleTriadBoardBackground => (tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory, resources)).BoardBackground,
            UiImageKind.Icon => (portraits ??= NativeMenuArtwork.Load(gameDirectory, resources)).GetIcon(source.Id, source.Palette),
            UiImageKind.NativeTutorialPage => (tutorials ??= NativeTutorialArtwork.Load(gameDirectory, resources)).Read(source.Id),
            UiImageKind.Magazine => (magazines ??= NativeMagazineArtwork.Load(gameDirectory, resources)).GetImage(source.Id),
            UiImageKind.MagazineButton => (magazines ??= NativeMagazineArtwork.Load(gameDirectory, resources)).GetButtonImage(source.Id),
            _ => throw new NotSupportedException($"Unsupported menu artwork kind {source.Kind}.")
        };
    }

    private static PixelImage ReadSilverCrest()
    {
        using var source = TitleScene.TitleArtworkRenderer.CreateSilverCrestImage();
        byte[] pixels = PortableRaster.Rgba(source);
        PixelImage image = new(source.Width, source.Height);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int index = (y * image.Width + x) * 4;
                image[x, y] = new Rgba32(pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]);
            }
        }
        return image;
    }

    private PixelImage ReadCardFace(int cardId, int owner, string fontFamily, bool grayscale = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cardId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cardId, CardArtwork.CardCount);
        if (cardFontFamily != fontFamily)
        {
            cardComposer = new CardFaceComposer(fontFamily);
            cardFontFamily = fontFamily;
            cardFaces.Clear();
        }
        if (!cardFaces.TryGetValue((cardId, owner, grayscale), out PixelImage? image))
        {
            if (grayscale)
            {
                image = ToGrayscale(ReadCardFace(cardId, owner, fontFamily));
                cardFaces.Add((cardId, owner, true), image);
                return image;
            }
            tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory, resources);
            cardDefinitions ??= TripleTriadCatalog.Load(gameDirectory);
            var card = cardDefinitions.Cards[cardId];
            PixelImage? element = card.Element == TripleTriadElement.None ? null : tripleTriad.GetElement(card.Element);
            image = cardComposer!.Compose(tripleTriad.GetFront(cardId), card, owner, element);
            cardFaces.Add((cardId, owner, false), image);
        }
        return image;
    }

    internal static PixelImage ToGrayscale(PixelImage source)
    {
        PixelImage result = new(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Rgba32 pixel = source[x, y];
                byte luminance = (byte)((299 * pixel.R + 587 * pixel.G + 114 * pixel.B + 500) / 1000);
                result[x, y] = new Rgba32(luminance, luminance, luminance, pixel.A);
            }
        }
        return result;
    }
}
