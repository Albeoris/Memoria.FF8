using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceAbilityEditorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-gf-editor-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void MouseCatalogAndToolbarAppendWithoutMutatingThePlaythrough()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        string before = JsonSerializer.Serialize(fixture.Session.State);
        string available = Assert.Single(Render(fixture.Menu).Texts, text => text.Value.StartsWith("+ Ability4 ·", StringComparison.Ordinal)).Value;
        Click(fixture.Menu, available);
        Click(fixture.Menu, "Append");
        Click(fixture.Menu, "Save mod");
        GuardianForceAbilityPatchOperation operation = Assert.Single(fixture.Saved!.Operations);
        Assert.Equal(4, operation.Ability);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
    }

    [Fact]
    public void SourceComparisonIsReadOnlyForControllerStartingStateChanges()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        Click(fixture.Menu, "Preview");
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.Contains("Source (read-only)", StringComparison.Ordinal));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Alternate);
        Assert.Contains("read-only", fixture.Menu.Message, StringComparison.OrdinalIgnoreCase);
        Click(fixture.Menu, "Save mod");
        Assert.Empty(fixture.Saved!.Operations);
    }

    [Fact]
    public void TargetsIncludeDisabledPackagesAndAllSixteenGfsWithoutAcquiringThem()
    {
        string package = Path.Combine(root, "Mods", "Memoria", "disabled");
        Directory.CreateDirectory(Path.Combine(package, "gameplay"));
        File.WriteAllText(Path.Combine(package, "mod.ini"), "enabled = 0\n");
        File.WriteAllBytes(Path.Combine(package, GuardianForceAbilityMods.PayloadPath), GuardianForceAbilityMods.Serialize(new()
        {
            DocumentId = "disabled-document"
        }));
        using ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        Fixture fixture = Create(runtime);
        fixture.Menu.Open("gf-ability-targets");
        Assert.Contains(fixture.Menu.Entries, entry => entry.Id == "Memoria/disabled" && entry.Enabled);
        fixture.Menu.Execute(new("gf-editor-target", "Memoria/disabled"));
        Assert.Equal("gf-ability-editor", fixture.Menu.ScreenId);
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.Contains("disabled", StringComparison.Ordinal));
        fixture.Menu.Navigate(MenuInput.PageLeft);
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.Contains("GF15 · Not acquired", StringComparison.Ordinal));
        Assert.Equal(0, fixture.Session.CharacterVitals!.GuardianForces[15].Flags & 1);
        for (int id = 0; id < 16; id++)
        {
            fixture.Menu.Navigate(MenuInput.PageRight);
            Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.Contains("GF" + id + " ·", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void RebuildingTheViewRetainsRowFocusAndUndoKeepsSessionPure()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        string before = JsonSerializer.Serialize(fixture.Session.State);
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Down);
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.StartsWith("Description2\n", StringComparison.Ordinal));
        fixture.Menu.InvalidateLiveData();
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.StartsWith("Description2\n", StringComparison.Ordinal));
        fixture.Menu.Navigate(MenuInput.Alternate);
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.Contains("Ability2 · Initially learned", StringComparison.Ordinal));
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.Contains(Render(fixture.Menu).Texts, text => text.Value.Contains("Ability2 · Unlearned", StringComparison.Ordinal));
        Click(fixture.Menu, "Save mod");
        Assert.Empty(fixture.Saved!.Operations);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
    }

    [Fact]
    public void KeyboardAppendAndStartingStateProduceOnlySelectedContribution()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        string before = JsonSerializer.Serialize(fixture.Session.State);
        fixture.Menu.Navigate(MenuInput.FocusLeft);
        for (int i = 0; i < 3; i++)
        {
            fixture.Menu.Navigate(MenuInput.Down);
        }
        fixture.Menu.Navigate(MenuInput.Confirm);
        fixture.Menu.Navigate(MenuInput.Confirm);
        fixture.Menu.Navigate(MenuInput.FocusRight);
        for (int i = 0; i < 3; i++)
        {
            fixture.Menu.Navigate(MenuInput.Down);
        }
        fixture.Menu.Navigate(MenuInput.Alternate);
        Click(fixture.Menu, "Save mod");
        GuardianForceAbilityPatchOperation operation = Assert.Single(fixture.Saved!.Operations);
        Assert.Equal(GuardianForceAbilityOperationKind.EnsureAbility, operation.Kind);
        Assert.Equal(4, operation.Ability);
        Assert.True(operation.InitialLearned);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
    }

    [Fact]
    public void ApplyReviewRejectsAChangedPlaythrough()
    {
        Fixture fixture = Create();
        fixture.Menu.Execute(new("gf-editor-target", "new"));
        fixture.Menu.Navigate(MenuInput.FocusRight);
        fixture.Menu.Navigate(MenuInput.Alternate);
        Click(fixture.Menu, "Apply once");
        Assert.Equal("gf-ability-review", fixture.Menu.ScreenId);
        fixture.Session.SetCharacterVitals(fixture.Session.CharacterVitals! with
        {
            DreamEnabled = true
        });
        string before = JsonSerializer.Serialize(fixture.Session.State);
        fixture.Menu.Execute(new("gf-editor-apply"));
        Assert.Contains("stale", fixture.Menu.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("gf-ability-review", fixture.Menu.ScreenId);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Session.State));
    }

    [Fact]
    public void ReplacementSwapToggleAndRowUndoKeepStableIdentityAndLearningPriority()
    {
        NativeCharacterVitals source = Source();
        GuardianForceAbilityDraft draft = new(source.ResolveGuardianForceTemplates(), source.MenuDefinitions.AbilityPoints, new()
        {
            DocumentId = "selected"
        });
        draft.Replace(0, 1, 4);
        draft.Toggle(0, 4);
        draft.Swap(0, 4, 3);
        Assert.Equal(new[] { 3, 2, 4 }, draft.Preview.Templates[0].Select(entry => entry.Ability));
        Assert.True(draft.Preview.Templates[0][2].InitialLearned);
        Assert.Equal(0, draft.Preview.Templates[0][2].LearningPriority);
        draft.Undo(0, 4);
        Assert.Equal(new[] { 4, 2, 3 }, draft.Preview.Templates[0].Select(entry => entry.Ability));
        draft.Undo(0, 4);
        Assert.False(draft.Preview.Templates[0][0].InitialLearned);
        draft.Undo(0, 4);
        Assert.Equal(new[] { 1, 2, 3 }, draft.Preview.Templates[0].Select(entry => entry.Ability));
        Assert.False(draft.Dirty);
        Assert.Empty(draft.ExportDocument.Operations);
    }

    private Fixture Create(IModService? mods = null)
    {
        NativeCharacterVitals source = Source();
        GameSession session = new(new GameState { CharacterVitals = source });
        Fixture fixture = new(new MenuController(() => new MenuData()), session);
        if (mods is not null)
        {
            fixture.Menu.ConnectMods(mods);
        }
        fixture.Menu.ConnectGuardianForceEditor(() => new(source, session.CharacterVitals, [], [], []), id => "GF" + id, id => "Ability" + id, id => "Description" + id, action => action.ToString(),
            (document, resets, explicitResets) => new(session.PreviewGuardianForceAbilities(GuardianForceAbilityComposer.Compose(source.ResolveGuardianForceTemplates(), [new("Selected", document)], source.MenuDefinitions.AbilityPoints).Templates), document, resets),
            review => { session.ApplyGuardianForceAbilities(review.Preview); return "Applied"; },
            (document, package, independent) => { fixture.Saved = document; return null; });
        fixture.Menu.Open("gf-ability-targets");
        return fixture;
    }

    private sealed class Fixture(MenuController menu, GameSession session)
    {
        public MenuController Menu { get; } = menu;
        public GameSession Session { get; } = session;
        public GuardianForceAbilityPatchDocument? Saved
        {
            get; set;
        }
    }

    internal static NativeCharacterVitals Source()
    {
        NativeStatCurve curve = new(1, 0, 1, 10, 10);
        NativeStatModifier modifier = new(0, 0, 0);
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = Enumerable.Range(0, 16).Select(_ => ImmutableArray.Create(new GuardianForceAbilityTemplate(1, false, 20), new GuardianForceAbilityTemplate(2, false, 20, LearningPriority: 1), new GuardianForceAbilityTemplate(3, false, 20, LearningPriority: 2))).ToImmutableArray();
        return new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = new('0', 64),
            KernelSha256 = new('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new(10, 10), 0, new(0, 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 1, 2],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(id => new NativeGuardianForceVital(0, (byte)(id == 15 ? 0 : 1), 10, new(0, 0), 10, new byte[21].ToImmutableArray(), 0, 0)).ToArray(),
            Definitions = new(Enumerable.Repeat(curve, 11).ToImmutableArray(), Enumerable.Repeat((byte)1, 57).ToImmutableArray(), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), Enumerable.Repeat((byte)20, 116).ToImmutableArray(), new byte[19].ToImmutableArray(), new byte[5].ToImmutableArray())
            {
                Templates = templates,
                SourceTemplates = templates
            },
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
    }

    private static RecordingCanvas Render(MenuController menu)
    {
        UiView view = menu.BuildView();
        view.Resize(1280, 896);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        return canvas;
    }

    private static void Click(MenuController menu, string label)
    {
        RecordingCanvas canvas = Render(menu);
        UiRect bounds = Assert.Single(canvas.Texts, text => text.Value == label).Bounds;
        UiView view = menu.BuildView();
        float x = bounds.X + bounds.Width / 2;
        float y = bounds.Y + bounds.Height / 2;
        view.Pointer(x, y, true);
        view.Pointer(x, y, false);
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(string Value, UiRect Bounds)> Texts { get; } = [];
        public UiSize MeasureText(string text, float fontSize, float maximumWidth) => new(Math.Min(maximumWidth, text.Length * fontSize / 2), fontSize);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color) => Texts.Add((text, bounds));
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment) => Text(text, fontSize, bounds, color);
        public void Fill(UiRect bounds, UiColor color)
        {
        }
        public void Image(UiImageSource source, UiRect bounds)
        {
        }
        public void MenuPointer(UiRect bounds)
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

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
