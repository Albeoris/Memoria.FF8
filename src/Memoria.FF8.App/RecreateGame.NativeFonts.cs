using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Text;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private NativeFontPresentation? nativeTextFont;
    private GameTextFontMode visibleTextFontMode;
    private GameTextLayoutMode visibleTextLayoutMode;
    private string? visibleNativeProfile;
    private bool NativeGameFonts => uiPreferences.TextFontMode == GameTextFontMode.Native;
    private bool OriginalTextLayout => uiPreferences.TextLayoutMode == GameTextLayoutMode.OriginalCompatible;
    private NativeTextContext NativeDialogueContext => new(resourceHost!.Resources.Source.Edition.ToString(), session.State.Scene == Runtime.SceneKind.WorldMap ? "World" : "Fields", session.State.Scene == Runtime.SceneKind.WorldMap ? null : world?.Field?.Name, world?.TextTerms.Language ?? resourceHost.Resources.ResourceLanguage);
    private float NativeDialogueLineHeight => nativeTextFont?.LineHeightFor(NativeDialogueContext) ?? 16;

    private void UpdateNativeFontPresentation()
    {
        if (resourceHost is null || uiCanvas is null)
        {
            return;
        }
        Memoria.FF8.Resources.Localization.LocalizationTextTrace.Enabled = NativeGameFonts || localizationWorkbench.Enabled;
        if (NativeGameFonts || localizationWorkbench.Enabled)
        {
            nativeTextFont ??= new(GraphicsDevice);
            bool selectedPreview = localizationWorkbench.Enabled && localizationWorkbench.PreviewSelectedMod && authoringSelection is not null;
            var fontPackages = selectedPreview ? resourceHost.Mods.Catalog.Packages.Select(package => package.Id == authoringSelection!.Package.Id ? package with { Status = Mods.ModEffectiveStatus.Active } : package).ToArray() : resourceHost.Mods.Catalog.Packages.ToArray();
            nativeTextFont.Prepare(resourceHost.Resources, fontPackages, uiPreferences.NativeFontProfileId, selectedPreview ? authoringSelection!.Project.Manifest.TargetLanguage : resourceHost.Mods.LocalizationPrimaryTarget);
        }
        if (visibleTextFontMode != uiPreferences.TextFontMode || visibleTextLayoutMode != uiPreferences.TextLayoutMode || visibleNativeProfile != nativeTextFont?.Profile?.Id)
        {
            visibleTextFontMode = uiPreferences.TextFontMode;
            visibleTextLayoutMode = uiPreferences.TextLayoutMode;
            visibleNativeProfile = nativeTextFont?.Profile?.Id;
            // Font/profile identity participates in the page cache key; retain reveal/page position.
            uiCanvas.ClearTextMeasurements();
            gameMenus?.InvalidateLiveData();
        }
        uiCanvas.NativeFont = nativeTextFont;
        uiCanvas.UseNativeFonts = NativeGameFonts;
        uiCanvas.UseNativeLayout = OriginalTextLayout;
    }

    private bool DrawNativeDialogueText(string value, Vector2 position, float scale, Color color, Rectangle bounds, NativeTextContext? context = null)
    {
        if (!NativeGameFonts)
        {
            return false;
        }
        if (nativeTextFont?.Draw(batch, value, position, scale, color, context ?? NativeDialogueContext, bounds, maximumHeight: Math.Max(0, bounds.Bottom - position.Y) / scale) != true)
        {
            uiCanvas?.Fill(new(position.X, position.Y, 6, 6), new Ui.UiColor(255, 80, 100));
        }
        return true;
    }
}
