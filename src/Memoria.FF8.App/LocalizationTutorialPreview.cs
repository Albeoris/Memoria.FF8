using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using SkiaSharp;

namespace Memoria.FF8;

internal sealed class LocalizationTutorialPreview : IDisposable
{
    private const int Width = 640;
    private const int Height = 480;
    private readonly GameWorld world;
    private readonly SKBitmap bitmap;
    private readonly BattlePreviewCanvas canvas;
    private long callbacks;

    public LocalizationTutorialPreview(GameWorld world, NativeTutorialPlayer player, string? contentLanguage)
    {
        this.world = world;
        Player = player;
        var assets = world.ValidationAssets;
        var resources = assets.Resources;
        LocalizationBattlePresentation localization = new(resources);
        Player.Menu.ConfigureNativeTutorialLocalization(localization.Table, localization.BoundTable);
        Player.Menu.ConnectLocalizationText(resources.Localize, () => contentLanguage ?? resources.ResourceLanguage);
        bitmap = new(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        try
        {
            canvas = new(bitmap, assets.GameDirectory, resources);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    internal NativeTutorialPlayer Player { get; }

    internal bool Advance(PlayerAction input)
    {
        PlayerAction? action = input.Kind switch
        {
            PlayerActionKind.Wait => input with { Frames = checked(input.Frames * NativeTutorialPlayer.CallbacksPerSecond / EventEngine.SimulationFps) },
            PlayerActionKind.Confirm or PlayerActionKind.Cancel when Player.WaitingForInput => input with { Frames = 1 },
            _ => null
        };
        if (action is null || !world.Apply(action).Accepted)
        {
            return false;
        }
        callbacks += action.Frames;
        return true;
    }

    internal bool IsSynchronizedWith(LocalizationTutorialPreview other) => callbacks == other.callbacks && Player.IsComplete == other.Player.IsComplete && Player.WaitingForInput == other.Player.WaitingForInput && Player.Menu.ScreenId == other.Player.Menu.ScreenId && Player.Menu.SelectedEntry?.Id == other.Player.Menu.SelectedEntry?.Id;

    internal PixelImage Render()
    {
        Player.RefreshPresentation();
        Player.Menu.PrepareViewport(Width, Height);
        UiView view = Player.Menu.BuildView();
        view.Resize(Width, Height);
        bitmap.Erase(SKColors.Black);
        canvas.BeginFrame(TimeSpan.FromSeconds((double)callbacks / NativeTutorialPlayer.CallbacksPerSecond));
        view.Draw(canvas, Player.Menu.CreateTheme());
        return new(Width, Height, MemoryMarshal.Cast<byte, Rgba32>(PortableRaster.Rgba(bitmap)).ToArray());
    }

    public void Dispose()
    {
        canvas.Dispose();
        bitmap.Dispose();
    }
}
