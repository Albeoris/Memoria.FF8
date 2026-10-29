using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

[CollectionDefinition("Tutorial authoring bindings", DisableParallelization = true)]
[Collection("Localization tracing")]
public sealed class NativeTutorialAuthoringBindingCollection
{
}

[Collection("Tutorial authoring bindings")]
public sealed class NativeTutorialAuthoringBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParsedAndTrimmedRunsRetainTheirOwnCanonicalIdentity(bool document)
    {
        bool enabled = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = true;
        try
        {
            const string template = "  Before {Key CharacterAbility} After{Line}{Color Green}Same wording";
            foreach (string key in new[] { "$en_mngrp_slot001_records_0000", "$en_mngrp_slot002_records_0000" })
            {
                LocalizationTextBinding binding = new(key, template, template, template) { Operation = 7, ResourceFingerprint = "fixture", Arguments = ["captured context"] };
                string displayed = document ? template : template.Replace("{Line}", "\n", StringComparison.Ordinal).Replace("{Color Green}", "", StringComparison.Ordinal);
                string input = LocalizationTextTrace.Attach(displayed, binding);
                UiElement element = document ? new NativeTutorialDocumentElement(input, 22, SystemFontCatalog.DefaultFamily) : new NativeTutorialTextElement(input, 384, SystemFontCatalog.DefaultFamily);
                element.Arrange(new UiRect(10, 20, 500, 180));
                BindingCanvas canvas = new();
                element.Draw(canvas, new UiTheme());
                Assert.NotEmpty(canvas.Drawn);
                Assert.All(canvas.Drawn, item =>
                {
                    LocalizationTextBinding retained = Assert.IsType<LocalizationTextBinding>(LocalizationTextTrace.Find(item.Text));
                    Assert.Equal(key, retained.Key);
                    Assert.Equal(template, retained.Template);
                    Assert.Equal(7, retained.Operation);
                    Assert.Equal("fixture", retained.ResourceFingerprint);
                    Assert.Equal("captured context", Assert.Single(retained.Arguments));
                    Assert.True(item.Bounds.Width > 0 && item.Bounds.Height > 0);
                });
            }
        }
        finally
        {
            LocalizationTextTrace.Enabled = enabled;
        }
    }

    [Fact]
    public void CompositionKeepsIdenticalWordingInDistinctExplicitOwnerSpans()
    {
        bool enabled = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = true;
        try
        {
            string left = LocalizationTextTrace.Attach("same", new("left", "left source", "left template", "same"));
            string right = LocalizationTextTrace.Attach("same", new("right", "right source", "right template", "same"));
            string joined = LocalizationTextComposition.Join(" / ", [LocalizationTextComposition.Concat("[", left, "]"), right]);
            Assert.Equal("[same] / same", joined);
            LocalizationTextBinding binding = Assert.IsType<LocalizationTextBinding>(LocalizationTextTrace.Find(joined));
            Assert.Empty(binding.Key);
            Assert.Equal(new[] { (1, 4, "left"), (9, 4, "right") }, binding.Fragments.Select(fragment => (fragment.Start, fragment.Length, fragment.Binding.Key)));
            string withRuntimeText = LocalizationTextComposition.Concat(joined, " ", new string("same".AsSpan()));
            Assert.Equal(2, LocalizationTextTrace.Find(withRuntimeText)!.Fragments.Count);
            Assert.All(binding.Fragments, fragment => Assert.Empty(fragment.Binding.Fragments));
        }
        finally
        {
            LocalizationTextTrace.Enabled = enabled;
        }
    }

    [Fact]
    public void FormattedCompositionTracksReorderingAlignmentAndRuntimeArguments()
    {
        bool enabled = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = true;
        try
        {
            string left = LocalizationTextTrace.Attach("same", new("left", "native left", "left template", "same"));
            string right = LocalizationTextTrace.Attach("same", new("right", "native right", "right template", "same"));
            const string raw = "{1} {0,6} {1}";
            string template = LocalizationTextTrace.Attach(raw, new("row", raw, raw, raw));
            string result = LocalizationTextComposition.Format(template, left, right);
            Assert.Equal(string.Format(System.Globalization.CultureInfo.CurrentCulture, raw, left, right), result);
            LocalizationTextBinding binding = Assert.IsType<LocalizationTextBinding>(LocalizationTextTrace.Find(result));
            Assert.Equal("row", binding.Key);
            Assert.Equal(raw, binding.Template);
            Assert.Equal(new[] { (0, 4, "right"), (7, 4, "left"), (12, 4, "right") }, binding.Fragments.Select(fragment => (fragment.Start, fragment.Length, fragment.Binding.Key)));
            Assert.Same(left, binding.Arguments[0]);
            Assert.Same(right, binding.Arguments[1]);
            const string countTemplate = "Found {0}: {1:N0}";
            string count = LocalizationTextComposition.Format(LocalizationTextTrace.Attach(countTemplate, new("count", countTemplate, countTemplate, countTemplate)), left, 42);
            Assert.Equal(countTemplate, LocalizationTextTrace.Find(count)!.Template);
            Assert.Equal(42, LocalizationTextTrace.Find(count)!.Arguments[1]);
            Assert.Single(LocalizationTextTrace.Find(count)!.Fragments);
            Assert.Equal("{same}", LocalizationTextComposition.Format("{{{0}}}", left));
        }
        finally
        {
            LocalizationTextTrace.Enabled = enabled;
        }
    }

    [Fact]
    public void ButtonExpansionRetainsTemplateAndArgumentsBeforeDocumentConstruction()
    {
        bool enabled = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = true;
        try
        {
            const string template = "Press {Key 35} to continue";
            LocalizationTextBinding binding = new("$en_tutorial_0000", template, template, template) { Arguments = [42] };
            string expanded = DialogueTextFormatter.ExpandButtonBindings(LocalizationTextTrace.Attach(template, binding), _ => "Confirm");
            Assert.Equal("Press Confirm to continue", expanded);
            LocalizationTextBinding retained = Assert.IsType<LocalizationTextBinding>(LocalizationTextTrace.Find(expanded));
            Assert.Equal(template, retained.Template);
            Assert.Equal(42, Assert.Single(retained.Arguments));
        }
        finally
        {
            LocalizationTextTrace.Enabled = enabled;
        }
    }

    private sealed class BindingCanvas : IUiCanvas
    {
        public List<(string Text, UiRect Bounds)> Drawn { get; } = [];
        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            if (text.Length != 0)
            {
                Drawn.Add((text, bounds));
            }
        }
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment alignment) => Text(text, fontSize, bounds, color);
        public void Fill(UiRect bounds, UiColor color)
        {
        }
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
        public void MenuPointer(UiRect bounds)
        {
        }
        public void Image(UiImageSource source, UiRect bounds)
        {
        }
        public void PushClip(UiRect bounds)
        {
        }
        public void PopClip()
        {
        }
    }
}
