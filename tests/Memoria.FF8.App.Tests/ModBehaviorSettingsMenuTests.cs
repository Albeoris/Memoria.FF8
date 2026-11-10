using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ModBehaviorSettingsMenuTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-mod-settings-menu-" + Guid.NewGuid().ToString("N"));
    private const string PackageId = "Memoria/Balance";

    [Fact]
    public void SliderStagesKeyboardChangesAndCancelRestoresSavedValue()
    {
        using ModRuntime runtime = Runtime();
        MenuController menu = Menu(runtime);
        Assert.True(Element(menu, "mod-settings").Visible);
        menu.Execute(new("mod-settings-open"));
        Assert.Equal("Mods · Balance · Settings", menu.CurrentScreen.Title);
        UiNumericSlider slider = Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-0"));
        Assert.Equal(0, slider.Min);
        Assert.Equal(100, slider.Max);
        Assert.Equal(1, slider.Step);
        Assert.Equal("0'%'", slider.ValueFormat);
        menu.Navigate(MenuInput.Right);
        Assert.Equal(51, Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-0")).Value);
        Assert.Equal(50, Assert.Single(runtime.ReadBehaviorSettings(PackageId)).Value);
        menu.Navigate(MenuInput.Cancel);
        menu.Execute(new("mod-settings-open"));
        Assert.Equal(50, Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-0")).Value);
        Assert.False(runtime.IsReloadPending);
    }

    [Fact]
    public void PointerDraftAndControllerSavePersistOnlyOnSave()
    {
        using ModRuntime runtime = Runtime();
        MenuController menu = Menu(runtime);
        menu.Execute(new("mod-settings-open"));
        UiNumericSlider slider = Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-0"));
        slider.Pointer(slider.Bounds.X + slider.Bounds.Width * 0.72f, slider.Bounds.Y + slider.Bounds.Height / 2);
        int draft = (int)Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-0")).Value;
        Assert.NotEqual(50, draft);
        Assert.Equal(50, Assert.Single(runtime.ReadBehaviorSettings(PackageId)).Value);
        menu.Navigate(MenuInput.Down);
        Assert.Equal("save", menu.SelectedEntry?.Id);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("mods", menu.ScreenId);
        Assert.Equal(draft, Assert.Single(runtime.ReadBehaviorSettings(PackageId)).Value);
        Assert.True(runtime.IsReloadPending);
        Assert.True(File.Exists(Path.Combine(root, "Mods/Memoria/Balance/config.json")));
        Assert.False(File.Exists(Path.Combine(root, "BundledMods/Memoria/Balance/config.json")));
    }

    [Fact]
    public void FailedSaveKeepsDraftAndShowsError()
    {
        using ModRuntime runtime = Runtime();
        MenuController menu = Menu(runtime);
        menu.Execute(new("mod-settings-open"));
        menu.Navigate(MenuInput.Right);
        string config = Path.Combine(root, "Mods/Memoria/Balance/config.json");
        Directory.CreateDirectory(config);
        menu.Execute(new("mod-settings-save"));
        Assert.Equal("mod-settings", menu.ScreenId);
        Assert.NotEmpty(menu.Message);
        Assert.Equal(51, Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-0")).Value);
        Assert.False(runtime.IsReloadPending);
    }

    private ModRuntime Runtime()
    {
        string payload = Path.Combine(root, "BundledMods/Memoria/Balance/gameplay/behaviors.json");
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        File.WriteAllText(payload, "{\"schemaVersion\":1,\"meltdown\":{\"defenseReductionPercent\":50}}");
        ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        return runtime;
    }

    private static MenuController Menu(IModService service)
    {
        MenuController menu = new();
        menu.ConnectMods(service);
        menu.Open("mods");
        menu.SelectEntry("mods", PackageId);
        return menu;
    }

    private static UiElement Element(MenuController menu, string id)
    {
        UiView view = menu.BuildView();
        view.Resize(2048, 1080);
        return Assert.Single(Descendants(view.Root), element => element.Id == id);
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
