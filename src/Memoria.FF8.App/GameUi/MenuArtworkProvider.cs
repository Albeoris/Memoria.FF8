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
    private PixelImage? silverCrest;
    private readonly Dictionary<(int Id, int Owner, bool Grayscale), PixelImage> cardFaces = [];

    public PixelImage Read(UiImageSource source, string fontFamily = SystemFontCatalog.DefaultFamily)
    {
        return source.Kind switch
        {
            UiImageKind.SilverCrest => silverCrest ??= ReadSilverCrest(),
            UiImageKind.CharacterPortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory)).GetCharacterPortrait(source.Id),
            UiImageKind.GuardianForcePortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory)).GetGuardianForcePortrait(source.Id),
            UiImageKind.AngeloPortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory)).AngeloPortrait,
            UiImageKind.Card => (cards ??= CardArtwork.Load(gameDirectory)).GetFront(source.Id),
            UiImageKind.CardElement => (tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory)).GetElement((TripleTriadElement)source.Id),
            UiImageKind.CardFace => ReadCardFace(source.Id, source.Palette, fontFamily),
            UiImageKind.CardFaceGrayscale => ReadCardFace(source.Id, source.Palette, fontFamily, grayscale: true),
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

    private static PixelImage ReadSilverCrest()
    {
        var source = TitleScene.TitleArtworkRenderer.CreateSilverCrestImage();
        byte[] pixels = new byte[checked(source.PixelWidth * source.PixelHeight * 4)];
        source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        PixelImage image = new(source.PixelWidth, source.PixelHeight);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int index = (y * image.Width + x) * 4;
                image[x, y] = new Rgba32(pixels[index + 2], pixels[index + 1], pixels[index], pixels[index + 3]);
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
            tripleTriad ??= NativeTripleTriadArtwork.Load(gameDirectory);
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
