using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Loads original foreground artwork on demand; panel backgrounds remain procedural.</summary>
internal sealed class MenuArtworkProvider(string gameDirectory)
{
    private NativeMenuArtwork? portraits;
    private CardArtwork? cards;

    public PixelImage Read(UiImageSource source)
    {
        return source.Kind switch
        {
            UiImageKind.CharacterPortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory)).GetCharacterPortrait(source.Id),
            UiImageKind.GuardianForcePortrait => (portraits ??= NativeMenuArtwork.Load(gameDirectory)).GetGuardianForcePortrait(source.Id),
            UiImageKind.Card => (cards ??= CardArtwork.Load(gameDirectory)).GetFront(source.Id),
            UiImageKind.CardBack => (cards ??= CardArtwork.Load(gameDirectory)).Back,
            UiImageKind.Icon => (portraits ??= NativeMenuArtwork.Load(gameDirectory)).GetIcon(source.Id, source.Palette),
            _ => throw new NotSupportedException($"Unsupported menu artwork kind {source.Kind}.")
        };
    }
}
