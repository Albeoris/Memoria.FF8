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
        Assert.Equal("Vitality reduction", slider.Text);
        UiNumericSlider spirit = Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-1"));
        Assert.Equal("Spirit reduction", spirit.Text);
        Assert.Equal(25, spirit.Value);
        Assert.Equal(5, spirit.Step);
        Assert.Equal(10, spirit.Min);
        Assert.Equal(90, spirit.Max);
        string description = Assert.IsType<UiLabel>(Element(menu, "mod-setting-description-1")).Text;
        Assert.Contains("Percentage by which Meltdown reduces Spirit.", description);
        Assert.Contains("10%", description);
        Assert.Contains("90%", description);
        Assert.Contains("5%", description);
        menu.Navigate(MenuInput.Right);
        Assert.Equal(51, Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-0")).Value);
        Assert.Equal(50, runtime.ReadBehaviorSettings(PackageId)[0].Value);
        menu.Navigate(MenuInput.Down);
        menu.Navigate(MenuInput.Right);
        Assert.Equal(30, Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-1")).Value);
        menu.Navigate(MenuInput.Cancel);
        menu.Execute(new("mod-settings-open"));
        Assert.Equal(25, Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-1")).Value);
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
        Assert.Equal(50, runtime.ReadBehaviorSettings(PackageId)[0].Value);
        menu.Navigate(MenuInput.Down);
        Assert.Equal("mod-setting-1", menu.SelectedEntry?.Id);
        menu.Navigate(MenuInput.Right);
        Assert.Equal(30, Assert.IsType<UiNumericSlider>(Element(menu, "mod-setting-1")).Value);
        menu.Navigate(MenuInput.Down);
        Assert.Equal("save", menu.SelectedEntry?.Id);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("mods", menu.ScreenId);
        Assert.Equal(draft, runtime.ReadBehaviorSettings(PackageId)[0].Value);
        Assert.Equal(30, runtime.ReadBehaviorSettings(PackageId)[1].Value);
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

    [Fact]
    public void LockedOptionsExplainHardcoreAndRejectPointerKeyboardAndSaveWrites()
    {
        using ModRuntime runtime = Runtime();
        LockedMods service = new(runtime);
        MenuController menu = Menu(service);
        Assert.Contains(LockedMods.Reason, menu.CurrentScreen.Details(menu));
        menu.Execute(new("mod-selected-toggle"));
        Assert.Equal(LockedMods.Reason, menu.Message);
        Assert.Equal(0, service.Writes);
        menu.Execute(new("mod-settings-open"));
        foreach (string id in new[] { "mod-setting-0", "mod-setting-1" })
        {
            UiNumericSlider slider = Assert.IsType<UiNumericSlider>(Element(menu, id));
            Assert.False(slider.Enabled);
            Assert.True(slider.ReadOnly);
            float before = slider.Value;
            slider.Pointer(slider.Bounds.X + slider.Bounds.Width * 0.72f, slider.Bounds.Y + slider.Bounds.Height / 2);
            slider.Key(UiKey.Right);
            Assert.Equal(before, slider.Value);
        }
        Assert.Equal(LockedMods.Reason, Assert.IsType<UiLabel>(Element(menu, "mod-settings-help")).Text);
        Assert.False(Element(menu, "save").Enabled);
        Assert.True(Element(menu, "cancel").Enabled);
        menu.Navigate(MenuInput.Right);
        menu.Execute(new("mod-settings-save"));
        Assert.Equal(0, service.Writes);
        Assert.False(runtime.IsReloadPending);
        Assert.False(File.Exists(Path.Combine(root, "Mods/Memoria/Balance/config.json")));
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("mods", menu.ScreenId);
    }

    [Fact]
    public void MeltdownExpansionFollowsToggleAndCardButtonPreservesDraft()
    {
        WriteBundled("Meltdown/mod.ini", "enabled=0");
        WriteBundled("Meltdown/gameplay/behaviors.json", """{"schemaVersion":1,"meltdown":{"defenseReductionPercent":50}}""");
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        MenuController menu = Menu(runtime, ModRuntime.MemoriaPackageId);
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Confirm);
        string toggleId = Assert.Single(menu.Entries, entry => entry.Label == "Meltdown").Id;
        menu.Navigate(MenuInput.Down);
        Assert.Equal(toggleId, menu.SelectedEntry?.Id);
        Assert.False(Assert.IsType<UiToggle>(Element(menu, toggleId)).Value);
        Assert.DoesNotContain(menu.Entries, entry => entry.Label == "Vitality reduction");

        menu.Navigate(MenuInput.Alternate);
        Assert.False(Assert.IsType<UiToggle>(Element(menu, toggleId)).Value);
        Assert.Equal(toggleId, menu.SelectedEntry?.Id);
        Assert.Contains(menu.Entries, entry => entry.Label == "Spirit reduction");
        menu.Navigate(MenuInput.Alternate);
        Assert.DoesNotContain(menu.Entries, entry => entry.Label == "Spirit reduction");

        menu.Navigate(MenuInput.Confirm);
        UiToggle toggle = Assert.IsType<UiToggle>(Element(menu, toggleId));
        Assert.True(toggle.Value);
        string vitalityId = Assert.Single(menu.Entries, entry => entry.Label == "Vitality reduction").Id;
        UiNumericSlider vitality = Assert.IsType<UiNumericSlider>(Element(menu, vitalityId));
        Assert.True(vitality.Bounds.X > toggle.Bounds.X);
        menu.Navigate(MenuInput.Down);
        menu.Navigate(MenuInput.Right);
        menu.Navigate(MenuInput.Up);
        menu.Navigate(MenuInput.Alternate);
        Assert.True(Assert.IsType<UiToggle>(Element(menu, toggleId)).Value);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id == vitalityId);
        menu.Navigate(MenuInput.Alternate);
        Assert.Equal(51, Assert.IsType<UiNumericSlider>(Element(menu, vitalityId)).Value);

        UiView view = menu.BuildView();
        view.Resize(1280, 768);
        toggle = Assert.IsType<UiToggle>(view.FocusedElement);
        view.Pointer(toggle.Bounds.X + 10, toggle.Bounds.Y + 10, pressed: true);
        Assert.False(Assert.IsType<UiToggle>(Element(menu, toggleId)).Value);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id == vitalityId);
        menu.Navigate(MenuInput.Right);
        Assert.True(Assert.IsType<UiToggle>(Element(menu, toggleId)).Value);
        Assert.Equal(51, Assert.IsType<UiNumericSlider>(Element(menu, vitalityId)).Value);
        menu.Navigate(MenuInput.Alternate);
        menu.Execute(new("mod-settings-save"));
        Assert.Equal(1, Assert.Single(runtime.ReadBehaviorSettings(ModRuntime.MemoriaPackageId), setting => setting.Parameter == "Meltdown").Value);
        Assert.Equal(51, Assert.Single(runtime.ReadBehaviorSettings(ModRuntime.MemoriaPackageId), setting => setting.Parameter == BehaviorMods.VitalityReductionParameter).Value);
    }

    [Fact]
    public void MemoriaCategoriesCollapseAndScrollWhileUnlockedChangesSave()
    {
        WriteBundled("Meltdown/gameplay/behaviors.json", """{"schemaVersion":1,"meltdown":{"defenseReductionPercent":50}}""");
        WriteBundled("Girl Next Door/mod.ini", "enabled=0");
        foreach (string component in new[] { "Depixelation", "Reorientation", "EdeaMirrors", "Extended FFVIII Roster", "Level-Based Stat Bonuses", "Girl Next Door", "FF7EnemyIntegration" })
        {
            WriteBundled(component + "/README.md", component);
        }
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        runtime.SetHardcoreMode(true);
        runtime.SetFf7BridgeEnemyIntegration(true);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        MenuController menu = Menu(runtime, ModRuntime.MemoriaPackageId);
        Assert.False(Element(menu, "mod-selected-toggle").Visible);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("mod-settings", menu.ScreenId);
        Assert.Equal(new[] { "Field", "Battle", "Party", "Items", "FFVII", "Save", "Cancel" }, menu.Entries.Select(entry => entry.Label));

        menu.Navigate(MenuInput.Down);
        menu.Navigate(MenuInput.Confirm);
        string lockedSliderId = Assert.Single(menu.Entries, entry => entry.Label == "Vitality reduction").Id;
        menu.Navigate(MenuInput.Down);
        string meltdownId = Assert.Single(menu.Entries, entry => entry.Label == "Meltdown").Id;
        Assert.Equal(meltdownId, menu.SelectedEntry?.Id);
        menu.Navigate(MenuInput.Confirm);
        Assert.True(Assert.IsType<UiToggle>(Element(menu, meltdownId)).Value);
        menu.Navigate(MenuInput.Alternate);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id == lockedSliderId);
        Assert.True(Assert.IsType<UiToggle>(Element(menu, meltdownId)).Value);
        menu.Navigate(MenuInput.Alternate);
        Assert.False(Element(menu, lockedSliderId).Enabled);
        Assert.Contains("Hardcore", Assert.IsType<UiLabel>(Element(menu, "mod-setting-description-" + lockedSliderId["mod-setting-".Length..])).Text);
        Assert.True(Element(menu, "save").Enabled);
        menu.Navigate(MenuInput.Up);
        menu.Navigate(MenuInput.Left);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id == lockedSliderId);

        UiView pointerView = menu.BuildView();
        pointerView.Resize(1280, 768);
        UiElement itemsCategory = Descendants(pointerView.Root).Single(element => element.Id == "mod-settings-category-3");
        pointerView.Pointer(itemsCategory.Bounds.X + 10, itemsCategory.Bounds.Y + 10, pressed: true);
        string bookId = Assert.Single(menu.Entries, entry => entry.Label == "Girl Next Door").Id;
        menu.Navigate(MenuInput.Down);
        Assert.Equal(bookId, menu.SelectedEntry?.Id);
        menu.Navigate(MenuInput.Confirm);
        Assert.True(Assert.IsType<UiToggle>(Element(menu, bookId)).Value);
        Assert.Equal(0, Assert.Single(runtime.ReadBehaviorSettings(ModRuntime.MemoriaPackageId), setting => setting.Parameter == "Girl Next Door").Value);

        foreach (string category in new[] { "Field", "Battle", "Party", "FFVII" })
        {
            menu.Execute(new("mod-settings-category", category));
        }
        UiView view = menu.BuildView();
        view.Resize(1280, 768);
        Assert.IsType<UiToggle>(view.FocusedElement);
        UiScrollPanel scroll = Assert.IsType<UiScrollPanel>(Descendants(view.Root).Single(element => element.Id == "mod-settings-options"));
        Assert.True(scroll.MaximumOffset > 0);
        UiRect saveBounds = Descendants(view.Root).Single(element => element.Id == "save").Bounds;
        Assert.True(saveBounds.Y >= scroll.Bounds.Bottom && saveBounds.Bottom <= 768);
        int wheelX = (int)(scroll.Bounds.X + 5);
        int wheelY = (int)(scroll.Bounds.Y + 5);
        menu.HandleModsWheel(wheelX, wheelY, -12000);
        Assert.Equal(scroll.MaximumOffset, scroll.Offset);
        menu.HandleModsWheel(wheelX, wheelY, 12000);
        Assert.Equal(0, scroll.Offset);
        menu.Navigate(MenuInput.PageDown);
        Assert.True(scroll.Offset > 0);
        Assert.False(scroll.Bounds.Intersect(view.FocusedElement!.Bounds).IsEmpty);
        Assert.Equal(saveBounds, Descendants(view.Root).Single(element => element.Id == "save").Bounds);

        menu.Execute(new("mod-settings-save"));
        Assert.Equal("mods", menu.ScreenId);
        Assert.Equal(1, Assert.Single(runtime.ReadBehaviorSettings(ModRuntime.MemoriaPackageId), setting => setting.Parameter == "Girl Next Door").Value);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(2048, 1080)]
    public void DescriptionsAndLockExplanationFitTheirRenderedBounds(int width, int height)
    {
        using ModRuntime runtime = Runtime();
        foreach (IModService service in new IModService[] { runtime, new LockedMods(runtime) })
        {
            MenuController menu = Menu(service);
            menu.Execute(new("mod-settings-open"));
            UiView view = menu.BuildView();
            view.Resize(width, height);
            UiRecordingCanvas canvas = new();
            view.Draw(canvas, new UiTheme());
            foreach (UiLabel label in Descendants(view.Root).OfType<UiLabel>().Where(label => label.Id?.StartsWith("mod-setting-description-", StringComparison.Ordinal) == true || label.Id == "mod-settings-help"))
            {
                var text = canvas.Texts.Last(text => text.Text == label.Text);
                UiSize measured = canvas.MeasureText(text.Text, text.FontSize, text.Bounds.Width);
                Assert.True(measured.Height <= text.Bounds.Height + 0.01f, $"{label.Id}: text height {measured.Height} exceeds {text.Bounds.Height}.");
                Assert.True(text.Bounds.X >= 0 && text.Bounds.Y >= 0);
                Assert.True(text.Bounds.Right <= width && text.Bounds.Bottom <= height);
            }
        }
    }

    private sealed class UiRecordingCanvas : IUiCanvas
    {
        public List<(string Text, float FontSize, UiRect Bounds)> Texts { get; } = [];
        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            float lines = text.Split('\n').Sum(line => Math.Max(1, MathF.Ceiling(line.Length * fontSize * 0.6f / maximumWidth)));
            return new UiSize(maximumWidth, lines * fontSize * 1.5f);
        }
        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, fontSize, bounds));
        }
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }
        public void Fill(UiRect bounds, UiColor color)
        {
        }
        public void MenuPointer(UiRect bounds)
        {
        }
        public void Image(UiImageSource source, UiRect bounds)
        {
        }
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
        public void PushClip(UiRect bounds)
        {
        }
        public void PopClip()
        {
        }
    }

    private sealed class LockedMods(ModRuntime runtime) : IModService
    {
        public const string Reason = "Hardcore requires Meltdown: Vitality reduction 50%, Spirit reduction 25%. These settings cannot be changed.";
        public int Writes { get; private set; }
        public ModCatalogSnapshot Catalog => runtime.Catalog;
        public bool AutoEnableNewMods => runtime.AutoEnableNewMods;
        public bool IsReloadPending => runtime.IsReloadPending;
        public IReadOnlyList<ModDiagnostic> Diagnostics => runtime.Diagnostics;
        public string? GetPackageLockReason(string packageId) => Reason;
        public string? GetBehaviorSettingLockReason(string packageId, string behavior, string parameter) => Reason;
        public IReadOnlyList<BehaviorModSetting> ReadBehaviorSettings(string packageId) => runtime.ReadBehaviorSettings(packageId);
        public ModOperationResult SetEnabled(string packageId, bool enabled)
        {
            Writes++;
            return runtime.SetEnabled(packageId, enabled);
        }
        public ModOperationResult SetAutoEnableNewMods(bool enabled) => runtime.SetAutoEnableNewMods(enabled);
        public void Refresh() => runtime.Refresh();
        public IDisposable Subscribe(Action<ModCatalogSnapshot> changed) => runtime.Subscribe(changed);
        public ModOperationResult SetBehaviorSetting(string packageId, string behavior, string parameter, int value)
        {
            Writes++;
            return runtime.SetBehaviorSetting(packageId, behavior, parameter, value);
        }
    }

    private ModRuntime Runtime()
    {
        string payload = Path.Combine(root, "BundledMods/Memoria/Balance/gameplay/behaviors.json");
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        File.WriteAllText(payload, """
            {
              "schemaVersion": 2,
              "meltdown": {
                "VitalityReductionPercent": { "label": "Vitality reduction", "description": "Percentage by which Meltdown reduces Vitality.", "control": "slider", "unit": "%", "min": 0, "max": 100, "step": 1, "default": 50 },
                "SpiritReductionPercent": { "label": "Spirit reduction", "description": "Percentage by which Meltdown reduces Spirit.", "control": "slider", "unit": "%", "min": 10, "max": 90, "step": 5, "default": 25 }
              }
            }
            """);
        ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        return runtime;
    }

    private void WriteBundled(string relativePath, string content)
    {
        string path = Path.Combine(root, "BundledMods", "Memoria", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static MenuController Menu(IModService service, string packageId = PackageId)
    {
        MenuController menu = new();
        menu.ConnectMods(service);
        menu.Open("mods");
        menu.SelectEntry("mods", packageId);
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
