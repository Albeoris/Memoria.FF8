using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

[Collection("Localization tracing")]
public sealed class LocalizationControlExpansionSpanTests
{
    [Theory]
    [InlineData("{Char Squall} ", "Leon ")]
    [InlineData("{Var 32} ", "1,234 ")]
    [InlineData("{TermEN Galbadia} ", "Place ")]
    public void NativeGrammarExpansionRemapsTheFollowingCanonicalChildBeforeGlyphInspection(string prefix, string expandedPrefix)
    {
        Trace(() =>
        {
            string encoded = LocalizationTextComposition.Concat(Bound("prefix", prefix), Bound("child", "Same"));
            DialogueDocument document = DialogueTextFormatter.Parse(encoded, new Dictionary<int, string> { [GameNames.CharacterSubject(0)] = "Leon" }, NativeMessageVariables.FromColdProcess().WithWrite(0, 1234), terms: new NativeTextTerms("en", ["Place"]), originalCompatible: true);
            string rendered = document.Text;
            Assert.Equal(expandedPrefix + "Same", rendered);
            LocalizationTextBinding binding = LocalizationTextTrace.Find(rendered)!;
            LocalizationTextFragment child = Assert.Single(binding.Fragments, fragment => fragment.Binding.Key == "child");
            Assert.Equal(expandedPrefix.Length, child.Start);
            Assert.Equal(4, child.Length);
            Assert.Equal("Same", rendered.Substring(child.Start, child.Length));
            LocalizationTextFragment parent = binding.Fragments.First(fragment => fragment.Binding.Key == "prefix");
            Assert.Equal("Leon", parent.Binding.Names[GameNames.CharacterSubject(0)]);
            Assert.Equal(1234, parent.Binding.MessageVariables[0]);
            PortableTextRasterResult raster = SystemTextLayout.RasterizeMapped(rendered, 24, 400, "Arial");
            using (raster.Bitmap)
            {
                var hits = LocalizationFragmentGeometry.Map(binding, raster.Clusters, Vector2.Zero, 1, new(0, 0, 400, 100));
                Assert.Contains(hits, hit => hit.Binding.Key == "prefix");
                Assert.Contains(hits, hit => hit.Binding.Key == "child");
                Assert.True(hits.Where(hit => hit.Binding.Key == "child").Min(hit => hit.Bounds.X) > hits.Where(hit => hit.Binding.Key == "prefix").Max(hit => hit.Bounds.X));
            }
        });
    }

    [Fact]
    public void VariableSpanApiUsesTheExistingGrammarAndPreservesLiteralSourceOffsets()
    {
        const string source = "a{Var32}b{Var 48}c";
        NativeMessageVariables bank = NativeMessageVariables.FromColdProcess().WithWrite(0, 9876);
        IReadOnlyList<NativeMessageExpansionSegment> segments = NativeMessageVariables.ExpandSegments(source, bank);
        Assert.Equal(NativeMessageVariables.Expand(source, bank), string.Concat(segments.Select(segment => segment.Text)));
        Assert.Equal(new[] { "{Var32}", "{Var 48}" }, segments.Where(segment => segment.IsReplacement).Select(segment => source.Substring(segment.SourceStart, segment.SourceLength)));
        Assert.Equal(new[] { "a", "b", "c" }, segments.Where(segment => !segment.IsReplacement).Select(segment => segment.Text));
        Assert.Throws<NotSupportedException>(() => NativeMessageVariables.ExpandSegments("{Var 32}", NativeMessageVariables.Unknown()));
    }

    [Fact]
    public void ButtonExpansionAndPromptSerializationRetainShiftedChildRanges()
    {
        Trace(() =>
        {
            string encoded = LocalizationTextComposition.Concat(Bound("prefix", "{Key Cross} "), Bound("child", "Same"));
            string expanded = DialogueTextFormatter.ExpandButtonBindings(encoded, _ => "Long Key");
            Assert.Equal("Long Key Same", expanded);
            Assert.Equal(9, Assert.Single(LocalizationTextTrace.Find(expanded)!.Fragments, fragment => fragment.Binding.Key == "child").Start);
            string prompts = DialogueTextFormatter.FormatWithPrompts(encoded, buttonBindingHint: _ => "Long Key");
            LocalizationTextFragment child = Assert.Single(LocalizationTextTrace.Find(prompts)!.Fragments, fragment => fragment.Binding.Key == "child");
            Assert.Equal("Same", prompts.Substring(child.Start, child.Length));
            Assert.Equal("Long Key Same", ButtonPromptText.PlainText(prompts));
        });
    }

    private static string Bound(string key, string text) => LocalizationTextTrace.Attach(text, new(key, text, text, text));
    private static void Trace(Action test)
    {
        bool previous = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = true;
        try
        {
            test();
        }
        finally
        {
            LocalizationTextTrace.Enabled = previous;
        }
    }
}
