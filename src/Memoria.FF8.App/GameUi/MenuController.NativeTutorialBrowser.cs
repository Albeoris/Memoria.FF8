using System.Globalization;
using Memoria.FF8.Assets;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private NativeTutorialCatalog? _nativeTutorialCatalog;
    private NativeTutorialBrowserCatalog? _nativeTutorialBrowser;
    private Func<IReadOnlySet<int>>? _nativeTutorialUnlocks;
    private Action<int>? _startNativeTutorial;
    private readonly Dictionary<int, int> _nativeTutorialPageIndices = [];

    public NativeTutorialBrowserPage? CurrentNativeTutorialPage
    {
        get
        {
            NativeTutorialBrowserCategory? category = CurrentNativePageCategory();
            return category is null ? null : category.Pages[_nativeTutorialPageIndices.GetValueOrDefault(category.Id)];
        }
    }

    public void ConnectNativeTutorials(NativeTutorialCatalog tutorials, NativeTutorialBrowserCatalog browser, Func<IReadOnlySet<int>> unlocked, Action<int> startDemo)
    {
        ArgumentNullException.ThrowIfNull(tutorials);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(unlocked);
        ArgumentNullException.ThrowIfNull(startDemo);
        _nativeTutorialCatalog = tutorials;
        _nativeTutorialBrowser = browser;
        _nativeTutorialUnlocks = unlocked;
        _startNativeTutorial = startDemo;
        foreach (string id in _screens.Keys.Where(id => id.StartsWith("tutorial-", StringComparison.Ordinal) || id == "seed-test").ToArray())
        {
            _screens.Remove(id);
        }
        Register(new MenuScreenDefinition
        {
            Id = "tutorial",
            Title = "Tutorial",
            Help = NativeBrowserText(browser.TutorialLabels[32].Value),
            MainCommand = true,
            Entries = controller => browser.Categories.Select(category => controller.NativeTutorialCategoryEntry(category)).ToArray(),
            BuildContent = (controller, panel) => panel.Children.Add(controller.BuildNativeTutorialList())
        });
        Register(new MenuScreenDefinition
        {
            Id = "tutorial-native-online-help",
            Title = browser.Categories.Single(category => category.CallbackMode == 255).Title,
            Help = browser.Categories.Single(category => category.CallbackMode == 255).Help,
            Entries = controller => tutorials.Tutorials.Where(tutorial => unlocked().Contains(checked((int)tutorial.Descriptor.UnlockId))).Select(tutorial => new MenuEntry($"tutorial-{tutorial.Id}", NativeBrowserText(tutorial.Title), NativeBrowserText(tutorial.Help), Action: new MenuAction("native-tutorial-start", tutorial.Id.ToString(CultureInfo.InvariantCulture)))).ToArray(),
            BuildContent = (controller, panel) => panel.Children.Add(controller.BuildNativeTutorialList())
        });
        foreach (NativeTutorialBrowserCategory category in browser.Categories.Where(category => category.Pages.Length > 0))
        {
            Register(new MenuScreenDefinition
            {
                Id = NativePageScreen(category.Id),
                Title = category.Title,
                Help = category.Help,
                Details = controller => category.Pages[controller._nativeTutorialPageIndices.GetValueOrDefault(category.Id)].Placements.Select(placement => placement.Value).ToArray(),
                BuildContent = (controller, panel) => panel.Children.Add(controller.BuildNativeTutorialPage(category))
            });
        }
        _view = null;
    }

    private MenuEntry NativeTutorialCategoryEntry(NativeTutorialBrowserCategory category)
    {
        string? subject = category.CallbackMode == 21 ? "information" : category.Id == 3 ? "test" : category.Id == 4 ? "review" : null;
        string? destination = category.CallbackMode == 255 ? "tutorial-native-online-help" : category.Pages.Length > 0 ? NativePageScreen(category.Id) : subject is not null && _nativeTutorialExamConnected ? $"tutorial-native-{subject}" : null;
        MenuAction? action = category.Pages.Length > 0 ? new("native-tutorial-open-pages", category.Id.ToString(CultureInfo.InvariantCulture)) : subject is not null && _nativeTutorialExamConnected ? new("native-exam-open", subject) : null;
        bool enabled = destination is not null && (subject is null || CanOpenNativeTutorialExam(subject));
        return new MenuEntry($"category-{category.Id}", category.Title, category.Help, destination, action, enabled);
    }

    private bool TryExecuteNativeTutorialBrowser(MenuAction action)
    {
        if (!action.Kind.StartsWith("native-tutorial-", StringComparison.Ordinal))
        {
            return false;
        }
        if (_nativeTutorialBrowser is null || _nativeTutorialCatalog is null || _nativeTutorialUnlocks is null || _startNativeTutorial is null)
        {
            throw new NotSupportedException("Original tutorial resources are not connected to this menu.");
        }
        if (!int.TryParse(action.SubjectId, NumberStyles.None, CultureInfo.InvariantCulture, out int id))
        {
            throw new ArgumentException("Native tutorial selection requires its original numeric identity.", nameof(action));
        }
        if (action.Kind == "native-tutorial-open-pages")
        {
            NativeTutorialBrowserCategory category = _nativeTutorialBrowser.Categories.Single(category => category.Id == id);
            if (category.Pages.Length == 0)
            {
                throw new NotSupportedException("Selected native tutorial category has no static page range.");
            }
            _nativeTutorialPageIndices[id] = 0;
            return true;
        }
        if (action.Kind == "native-tutorial-start")
        {
            NativeTutorialDefinition tutorial = _nativeTutorialCatalog.Get(id);
            if (!_nativeTutorialUnlocks().Contains(checked((int)tutorial.Descriptor.UnlockId)))
            {
                throw new InvalidOperationException("The selected native tutorial is not unlocked in the current saved state.");
            }
            _startNativeTutorial(id);
            return true;
        }
        throw new NotSupportedException($"Unknown native tutorial browser action '{action.Kind}'.");
    }

    private bool TryNavigateNativeTutorialBrowser(MenuInput input)
    {
        NativeTutorialBrowserCategory? category = CurrentNativePageCategory();
        if (category is null)
        {
            return false;
        }
        int page = _nativeTutorialPageIndices.GetValueOrDefault(category.Id);
        if (input == MenuInput.Cancel || input == MenuInput.Confirm && page == category.Pages.Length - 1)
        {
            Back();
            return true;
        }
        int delta = input switch
        {
            MenuInput.Left or MenuInput.PageLeft or MenuInput.PageUp => -1,
            MenuInput.Right or MenuInput.PageRight or MenuInput.PageDown or MenuInput.Confirm => 1,
            _ => 0
        };
        if (delta != 0)
        {
            _nativeTutorialPageIndices[category.Id] = (page + delta + category.Pages.Length) % category.Pages.Length;
            SoundRequested?.Invoke(input == MenuInput.Confirm ? MenuSound.Confirm : MenuSound.Move);
            _view = null;
        }
        return true;
    }

    private NativeTutorialBrowserCategory? CurrentNativePageCategory()
    {
        return _nativeTutorialBrowser?.Categories.FirstOrDefault(category => category.Pages.Length > 0 && ScreenId == NativePageScreen(category.Id));
    }

    private UiElement BuildNativeTutorialList()
    {
        var page = Grid([1.8f, 6.2f], [1], 5);
        Place(page, SecondaryHeader("Tutorial", NativeBrowserText(CurrentScreen.Title), NativeBrowserText(SelectedEntry?.Help ?? CurrentScreen.Help)), 0);
        Place(page, Panel(null, SecondaryCommands()), 1);
        return page;
    }

    private UiElement BuildNativeTutorialPage(NativeTutorialBrowserCategory category)
    {
        NativeTutorialBrowserPage page = category.Pages[_nativeTutorialPageIndices.GetValueOrDefault(category.Id)];
        var canvas = new UiAnchorPanel { Id = $"native-tutorial-page-{page.Id}" };
        canvas.Children.Add(new UiImage { Source = new UiImageSource(UiImageKind.NativeTutorialPage, page.Id) });
        int originX = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(page.Metadata.AsSpan());
        int originY = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(page.Metadata.AsSpan().Slice(2, 2));
        foreach (NativeTutorialPageText placement in page.Placements)
        {
            canvas.Children.Add(new NativeTutorialTextElement(NativeBrowserText(placement.Value), 384 - originX - placement.X, _readPreferences?.Invoke().UiFontFamily)
            {
                Id = $"native-tutorial-text-{placement.TextIndex}",
                FontSize = 27 * TextScale,
                Anchors = new UiAnchors((originX + placement.X) / 384f, (originY + placement.Y) / 256f, 1, 1)
            });
        }
        var aspect = new NativeTutorialPageAspect();
        aspect.Children.Add(canvas);
        return aspect;
    }

    private string NativeBrowserText(string text)
    {
        return FormatNativeTutorialText(text);
    }

    internal string FormatNativeTutorialText(string text, IReadOnlyDictionary<int, string>? names = null)
    {
        return DialogueTextFormatter.Format(text, names, buttonBindingHint: buttons => CurrentBindingText(KeyboardInput.NativeButtonAction(buttons)));
    }

    private static string NativePageScreen(int category) => $"tutorial-native-pages-{category}";

    private sealed class NativeTutorialPageAspect : UiElement
    {
        public override void Arrange(UiRect bounds)
        {
            Bounds = bounds;
            UiRect page = UiImage.Fit(bounds, NativeTutorialArtwork.Width, NativeTutorialArtwork.Height);
            foreach (UiElement child in Children)
            {
                child.Arrange(page);
            }
        }
    }
}
