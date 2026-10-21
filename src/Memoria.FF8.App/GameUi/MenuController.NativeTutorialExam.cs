using System.Globalization;
using Memoria.FF8.Assets;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private bool _nativeTutorialExamConnected;
    private NativeTutorialExamCatalog? _nativeExamCatalog;
    private Func<NativeTutorialExamProgress>? _nativeExamContext;
    private NativeTutorialExamProgress? _nativeExamProgress;
    private (int Passed, int Rank, int Level)? _nativeExamBaseline;
    private int _nativeExamLevel;
    private bool _nativeExamReview;
    private readonly List<byte> _nativeExamAnswers = [];
    private NativeTutorialExamResult? _nativeExamResult;
    private int _nativeInformationNode;
    private readonly Stack<int> _nativeInformationHistory = [];
    private readonly Dictionary<int, int> _nativeInformationSelections = [];

    public NativeTutorialExamProgress? NativeTutorialExamProgress => _nativeExamProgress;
    public NativeTutorialExamResult? NativeTutorialExamResult => _nativeExamResult;
    public NativeTutorialInformationPage? CurrentNativeInformationPage => _nativeExamCatalog is null || _nativeExamProgress is null ? null : _nativeExamCatalog.InformationPage(_nativeInformationNode, _nativeTutorialUnlocks?.Invoke() ?? new HashSet<int>(), _nativeExamProgress);

    public void ConnectNativeTutorialExam(NativeTutorialExamCatalog catalog, Func<NativeTutorialExamProgress> context)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(context);
        _nativeExamCatalog = catalog;
        _nativeExamContext = context;
        _nativeTutorialExamConnected = true;
        void RegisterExam(string id, string title, Func<MenuController, IReadOnlyList<MenuEntry>> entries, Func<MenuController, IReadOnlyList<string>> details)
        {
            Register(new MenuScreenDefinition { Id = id, Title = title, Help = title, Entries = entries, Details = details, BuildContent = (controller, panel) => panel.Children.Add(controller.BuildNativeExamPage()) });
        }
        RegisterExam("tutorial-native-test", "TEST", controller => controller.NativeExamStartEntries(), controller => [controller.NativeExamStartText()]);
        RegisterExam("tutorial-native-review", "Review", controller => Enumerable.Range(1, controller._nativeExamProgress?.PassedTests ?? 0).Select(level => new MenuEntry($"review-{level}", level.ToString(CultureInfo.InvariantCulture), "Review", "tutorial-native-exam-question", new MenuAction("native-exam-review", level.ToString(CultureInfo.InvariantCulture)))).ToArray(), controller => []);
        RegisterExam("tutorial-native-exam-question", "TEST", controller => controller.NativeExamAnswerEntries(), controller => [controller.CurrentNativeExamQuestion()?.Text ?? ""]);
        RegisterExam("tutorial-native-exam-result", "TEST", controller => [new MenuEntry("end", "END", "END", Action: new MenuAction("native-exam-end", "0"))], controller => [controller.NativeExamResultText()]);
        RegisterExam("tutorial-native-information", "Information", controller => controller.CurrentNativeInformationPage?.Links.Select((link, index) => new MenuEntry($"information-{index}", controller.NativeBrowserText(link.Label), controller.NativeBrowserText(link.Label), Action: new MenuAction("native-exam-information-link", link.TargetId.ToString(CultureInfo.InvariantCulture)))).ToArray() ?? [], controller => [controller.CurrentNativeInformationPage?.Text ?? ""]);
        _view = null;
    }

    private bool TryExecuteNativeTutorialExam(MenuAction action)
    {
        if (!action.Kind.StartsWith("native-exam-", StringComparison.Ordinal))
        {
            return false;
        }
        if (_nativeExamCatalog is null || _nativeExamContext is null)
        {
            throw new NotSupportedException("Original SeeD test and Information resources are not connected.");
        }
        if (action.Kind == "native-exam-open")
        {
            NativeTutorialExamProgress source = _nativeExamContext();
            NativeTutorialExamCatalog.ValidateProgress(source);
            var baseline = (source.PassedTests, source.SeeDRank, source.CharacterLevel);
            if (_nativeExamBaseline != baseline)
            {
                _nativeExamProgress = source;
                _nativeExamBaseline = baseline;
            }
            _nativeExamProgress = _nativeExamProgress! with { Party = source.Party?.DeepCopy(), InformationNumbers = source.InformationNumbers?.ToDictionary(pair => pair.Key, pair => pair.Value) };
            _nativeExamAnswers.Clear();
            _nativeExamResult = null;
            _nativeInformationNode = 0;
            _nativeInformationHistory.Clear();
            _nativeInformationSelections.Clear();
            return true;
        }
        if (_nativeExamProgress is null || !int.TryParse(action.SubjectId, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            throw new ArgumentException("Original tutorial input requires its isolated context and numeric identity.");
        }
        if (action.Kind is "native-exam-begin" or "native-exam-review")
        {
            _nativeExamReview = action.Kind == "native-exam-review";
            if (value is < 1 or > 30 || (_nativeExamReview ? value > _nativeExamProgress.PassedTests : value != _nativeExamProgress.PassedTests + 1 || value > _nativeExamProgress.CharacterLevel || _nativeExamProgress.SeeDRank < 0))
            {
                throw new InvalidOperationException("Original SeeD test is unavailable at the current isolated progress.");
            }
            _nativeExamLevel = value;
            _nativeExamAnswers.Clear();
            _nativeExamResult = null;
            return true;
        }
        if (action.Kind == "native-exam-answer")
        {
            if (value is < 0 or > 1 || _nativeExamAnswers.Count >= 10 || _nativeExamLevel is < 1 or > 30)
            {
                throw new InvalidOperationException("Original SeeD answer is outside its active question.");
            }
            _nativeExamAnswers.Add((byte)value);
            if (_nativeExamAnswers.Count == 10)
            {
                _nativeExamResult = _nativeExamCatalog.Evaluate(_nativeExamProgress, _nativeExamLevel, _nativeExamAnswers, _nativeExamReview);
                _nativeExamProgress = _nativeExamResult.Progress;
                ScreenId = "tutorial-native-exam-result";
                _view = null;
                ScreenOpened?.Invoke(ScreenId);
            }
            else
            {
                _selections[ScreenId] = 0;
                _view = null;
            }
            return true;
        }
        if (action.Kind == "native-exam-information-link")
        {
            NativeTutorialInformationPage page = CurrentNativeInformationPage!;
            if (!page.Links.Any(link => link.TargetId == value))
            {
                throw new InvalidOperationException("Selected Information link is not present in the visible original page.");
            }
            _nativeInformationHistory.Push(_nativeInformationNode);
            OpenNativeInformationNode(value);
            _view = null;
            return true;
        }
        if (action.Kind == "native-exam-end")
        {
            Back();
            return true;
        }
        throw new NotSupportedException($"Unknown original tutorial input '{action.Kind}'.");
    }

    public bool CanOpenNativeTutorialExam(string kind)
    {
        if (!_nativeTutorialExamConnected || _nativeExamContext is null)
        {
            return false;
        }
        NativeTutorialExamProgress source = _nativeExamContext();
        var baseline = (source.PassedTests, source.SeeDRank, source.CharacterLevel);
        NativeTutorialExamProgress progress = _nativeExamBaseline == baseline ? _nativeExamProgress ?? source : source;
        return kind switch
        {
            "review" => progress.PassedTests > 0,
            "test" => progress.SeeDRank >= 0,
            "information" => true,
            _ => false
        };
    }

    private bool TryNavigateNativeTutorialExam(MenuInput input)
    {
        if (!_nativeTutorialExamConnected || !ScreenId.StartsWith("tutorial-native-", StringComparison.Ordinal))
        {
            return false;
        }
        if (ScreenId == "tutorial-native-exam-question" && input == MenuInput.Cancel)
        {
            return true;
        }
        if (ScreenId != "tutorial-native-information" || CurrentNativeInformationPage is not NativeTutorialInformationPage page)
        {
            return false;
        }
        if (input == MenuInput.Cancel)
        {
            if (_nativeInformationHistory.TryPop(out int previous))
            {
                OpenNativeInformationNode(previous);
            }
            else if (page.Node.OriginId != ushort.MaxValue)
            {
                OpenNativeInformationNode(page.Node.OriginId);
            }
            else
            {
                Back();
            }
            _view = null;
            return true;
        }
        int destination = input switch
        {
            MenuInput.Left or MenuInput.PageLeft or MenuInput.PageUp => page.Node.LeftId,
            MenuInput.Right or MenuInput.PageRight or MenuInput.PageDown => page.Node.RightId,
            _ => ushort.MaxValue
        };
        if (destination != ushort.MaxValue)
        {
            OpenNativeInformationNode(destination);
            _view = null;
            return true;
        }
        return false;
    }

    private void OpenNativeInformationNode(int node)
    {
        _nativeInformationSelections[_nativeInformationNode] = SelectedIndex;
        _nativeInformationNode = node;
        _selections[ScreenId] = _nativeInformationSelections.GetValueOrDefault(node);
    }

    private IReadOnlyList<MenuEntry> NativeExamStartEntries()
    {
        if (_nativeExamProgress is null || _nativeExamProgress.PassedTests >= 30 || _nativeExamProgress.PassedTests + 1 > _nativeExamProgress.CharacterLevel || _nativeExamProgress.SeeDRank < 0)
        {
            return [new MenuEntry("back", "GO BACK", "GO BACK", "tutorial")];
        }
        return [new MenuEntry("yes", "YES", "YES", "tutorial-native-exam-question", new MenuAction("native-exam-begin", (_nativeExamProgress.PassedTests + 1).ToString(CultureInfo.InvariantCulture))), new MenuEntry("no", "NO", "NO", "tutorial")];
    }

    private string NativeExamStartText()
    {
        if (_nativeExamProgress is null || _nativeExamCatalog is null)
        {
            return "";
        }
        int prompt = _nativeExamProgress.PassedTests >= 30 ? 5 : _nativeExamProgress.PassedTests + 1 > _nativeExamProgress.CharacterLevel ? 10 : _nativeExamProgress.SeeDRank == 31 ? 6 : _nativeExamProgress.PassedTests == 0 ? 0 : 1;
        return NativeExamText(_nativeExamCatalog.Prompts[prompt], prompt == 10 ? _nativeExamProgress.CharacterLevel : _nativeExamProgress.PassedTests, _nativeExamProgress.PassedTests + 1, _nativeExamProgress.SeeDRank);
    }

    private NativeTutorialExamQuestion? CurrentNativeExamQuestion()
    {
        return _nativeExamCatalog is null || _nativeExamLevel is < 1 or > 30 || _nativeExamAnswers.Count >= 10 ? null : _nativeExamCatalog.Levels[_nativeExamLevel - 1].Questions[_nativeExamAnswers.Count];
    }

    private IReadOnlyList<MenuEntry> NativeExamAnswerEntries()
    {
        return CurrentNativeExamQuestion()?.Options.Select((option, index) => new MenuEntry(index.ToString(CultureInfo.InvariantCulture), option, option, Action: new MenuAction("native-exam-answer", index.ToString(CultureInfo.InvariantCulture)))).ToArray() ?? [];
    }

    private string NativeExamResultText()
    {
        if (_nativeExamResult is null || _nativeExamCatalog is null)
        {
            return "";
        }
        int number = _nativeExamResult.Passed ? _nativeExamLevel : _nativeExamResult.Score;
        return NativeExamText(_nativeExamCatalog.Prompts[_nativeExamResult.PromptIndex], number, _nativeExamLevel, _nativeExamProgress!.SeeDRank);
    }

    private static string NativeExamText(string text, int cardLevel, int currentValue, int rank)
    {
        int options = text.IndexOf("{Option ", StringComparison.Ordinal);
        string body = options >= 0 ? text[..options] : text;
        return body.Replace("{Dialog CardLevel}", cardLevel.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal).Replace("{Dialog CurrentValue}", currentValue.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal).Replace("{Dialog SelectedMagic}", rank == 31 ? "A" : rank.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private UiElement BuildNativeExamPage()
    {
        if (ScreenId == "tutorial-native-information" && CurrentNativeInformationPage is NativeTutorialInformationPage information)
        {
            NativeTutorialDocumentElement document = NativeTutorialDocument(information.Text, 24);
            document.Id = "native-information-document";
            for (int index = 0; index < information.Links.Length; index++)
            {
                int selected = index;
                document.AddLink(information.Links[index].LineIndex, new UiButton
                {
                    Id = $"information-{index}",
                    DrawBackground = false,
                    HighlightSelection = false,
                    PointerSize = 20 * TextScale,
                    Click = () => Activate(selected)
                });
            }
            var informationPage = Grid([0.8f, 7.2f], [1], 5);
            Place(informationPage, NativeTutorialHeading(information.Node.Title), 0);
            Place(informationPage, Panel(null, document), 1);
            return informationPage;
        }
        string title = _nativeExamReview && ScreenId is "tutorial-native-exam-question" or "tutorial-native-exam-result" ? "Review" : CurrentScreen.Title;
        string text = string.Join("\n", CurrentScreen.Details(this));
        var page = Grid([0.8f, 5.2f, 2.5f], [1], 5);
        Place(page, NativeTutorialHeading(title), 0);
        NativeTutorialDocumentElement body = NativeTutorialDocument(text, 25);
        body.Id = "native-exam-document";
        Place(page, Panel(null, body), 1);
        Place(page, Panel(null, SecondaryCommands()), 2);
        return page;
    }

    private UiElement NativeTutorialHeading(string title)
    {
        var heading = Grid([1], [3.7f, 1.3f], 5);
        Place(heading, Panel(null, Label(NativeBrowserText(title), 24)), 0);
        Place(heading, Panel(null, Label("Tutorial", 27)), 0, 1);
        return heading;
    }

    private NativeTutorialDocumentElement NativeTutorialDocument(string text, float fontSize)
    {
        string expanded = DialogueTextFormatter.ExpandButtonBindings(text, buttons => ButtonPromptText.Mark(CurrentBindingText(KeyboardInput.NativeButtonAction(buttons))));
        return new NativeTutorialDocumentElement(expanded, fontSize * TextScale, (_readPreferences?.Invoke() ?? new UiPreferences()).UiFontFamily);
    }
}
