using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class SettingsLayoutTests
{
    [Fact]
    public void OptInInstalledCanvasCapturesLargeTextSettingsForBothPresentations()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SETTINGS_UI_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidOperationException("Configure MEMORIA_FF8_GAME_DIR for installed-canvas captures.");
        Directory.CreateDirectory(output);
        string family = SystemFontCatalog.Families.OrderByDescending(family => family.Length).First();
        string[]? requestedScreens = Environment.GetEnvironmentVariable("MEMORIA_FF8_SETTINGS_UI_SCREENS")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string[]? requestedScales = Environment.GetEnvironmentVariable("MEMORIA_FF8_SETTINGS_UI_SCALES")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        List<object> manifest = [];
        foreach ((int percent, int size) in new[] { (100, 24), (125, 24), (150, 32) })
        {
            if (requestedScales is not null && !requestedScales.Contains(percent.ToString()))
            {
                continue;
            }
            foreach (MenuPresentation presentation in Enum.GetValues<MenuPresentation>())
            {
                UiPreferences preferences = new() { MenuPresentation = presentation, TextScale = percent, UiFontSize = size, ClassicUiFontFamily = family, VectorUiFontFamily = family, DialogueFontFamily = family, DialogueFontSize = 16 };
                int writes = 0;
                MenuData data = MenuDemoData.Create();
                data.SupportedActions.Add("config");
                data.Settings["Text scale"] = percent + "%";
                data.Settings["UI font size"] = size.ToString();
                data.Settings["Dialogue font size"] = preferences.DialogueFontSize.ToString();
                data.Settings["Dialogue font"] = family;
                data.Settings["UI presentation"] = presentation.ToString();
                MenuController menus = new(() => data, _ => writes++);
                menus.ConnectPreferences(() => preferences, _ => writes++);
                menus.ConnectMods(new PreviewMods());
                menus.ConnectBattleSettings(() => false, _ => writes++);
                menus.Presentation = presentation;
                foreach ((int width, int height) in new[] { (640, 448), (1280, 720), (1920, 1080), (2560, 1080) })
                {
                    foreach ((string screen, bool last) in new[] { ("config", false), ("config-graphics", false), ("config-audio", false), ("config-text", false), ("config-text", true), ("config-exploration", true), ("debug-battle", false), ("debug-shortcuts", false), ("font-picker", false), ("mods", false), ("mods", true) })
                    {
                        if (requestedScreens is not null && !requestedScreens.Contains(screen))
                        {
                            continue;
                        }
                        if (percent != 150 && screen is not ("config" or "config-text" or "mods"))
                        {
                            continue;
                        }
                        menus.Open(screen);
                        if (last && menus.Entries.Count > 0)
                        {
                            menus.SelectEntry(screen, menus.Entries[^1].Id, allowDisabled: true);
                        }
                        using Bitmap bitmap = new(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
                        UiView view = menus.BuildView();
                        view.Resize(width, height);
                        using (BattlePreviewCanvas canvas = new(bitmap, gameDirectory))
                        {
                            view.Draw(canvas, menus.CreateTheme());
                        }
                        string name = $"scale{percent}-font{size}-{presentation.ToString().ToLowerInvariant()}-{screen}-{(last ? "last" : "first")}-{width}x{height}.png";
                        bitmap.SavePng(Path.Combine(output, name));
                        manifest.Add(new { name, screen, width, height, family, preferences.UiFontSize, preferences.TextScale, menus.SelectedIndex });
                    }
                }
                Assert.Equal(0, writes);
            }
        }
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class PreviewMods : IModService, IDisposable
    {
        public ModCatalogSnapshot Catalog { get; } = new(1, Enumerable.Range(0, 35).Select(index => new ModPackage("package-" + index, $"Package {index:00} / \u041f\u0440\u0438\u043c\u0435\u0440 \u0434\u043b\u0438\u043d\u043d\u043e\u0433\u043e \u043d\u0430\u0437\u0432\u0430\u043d\u0438\u044f", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, "")), []);
        public bool AutoEnableNewMods => true;
        public bool IsReloadPending => false;
        public IReadOnlyList<ModDiagnostic> Diagnostics => [];
        public ModOperationResult SetEnabled(string packageId, bool enabled) => throw new InvalidOperationException("Capture must not write mod configuration.");
        public ModOperationResult SetAutoEnableNewMods(bool enabled) => throw new InvalidOperationException("Capture must not write mod configuration.");
        public void Refresh() => throw new InvalidOperationException("Capture must not refresh live resources.");
        public IDisposable Subscribe(Action<ModCatalogSnapshot> changed) => this;
        public void Dispose()
        {
        }
    }
}
