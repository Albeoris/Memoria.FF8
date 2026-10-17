using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class DialogueRichTextTests
{
    [Fact]
    public void Button_tags_render_current_keyboard_bindings_with_adjacent_numbers_and_authored_lines()
    {
        const string encoded = "{Key Triangle}4{Line}{Key Square}3 {Key Circle}1{Line}{Key Cross}2";
        Assert.Equal("C4\nS3 V1\nX2", DialogueTextFormatter.Format(encoded));
    }

    [Theory]
    [InlineData(TextTagKey.Triangle, "C")]
    [InlineData(TextTagKey.Circle, "V")]
    [InlineData(TextTagKey.Cross, "X")]
    [InlineData(TextTagKey.Square, "S")]
    [InlineData(TextTagKey.L1, "H")]
    [InlineData(TextTagKey.R1, "G")]
    [InlineData(TextTagKey.UpKey, "Up")]
    [InlineData(TextTagKey.RightKey, "Right")]
    [InlineData(TextTagKey.DownKey, "Down")]
    [InlineData(TextTagKey.LeftKey, "Left")]
    public void Named_and_numeric_button_bindings_preserve_color_and_blink(TextTagKey key, string expected)
    {
        foreach (string parameter in new[] { key.ToString(), ((byte)key).ToString() })
        {
            DialogueRun run = Assert.Single(Assert.Single(Assert.Single(DialogueTextFormatter.Parse($"{{Color GreenBlink}}{{Key {parameter}}}").Pages).Lines).Runs);
            Assert.Equal(new DialogueRun(expected, Color.Lime, true), run);
        }
    }

    [Fact]
    public void Button_symbols_keep_junction_marker_and_unresolved_tags_distinct()
    {
        Assert.Equal("J/{Key 255}/{Key Unknown}", DialogueTextFormatter.Format("{Key 65}/{Key 255}/{Key Unknown}"));
    }

    [Fact]
    public void Page_and_next_boundaries_preserve_source_lines_and_plain_text()
    {
        const string encoded = "One{Line}{Line}Two{Page}Three{Next}Four{End}";
        DialogueDocument document = DialogueTextFormatter.Parse(encoded);
        Assert.Equal("One\n\nTwo\nThree\nFour", DialogueTextFormatter.Format(encoded));
        Assert.Equal([DialoguePageBoundary.Initial, DialoguePageBoundary.Page, DialoguePageBoundary.Next], document.Pages.Select(page => page.Boundary));
        Assert.Equal([0, 1, 2, 3, 4], document.Pages.SelectMany(page => page.Lines).Select(line => line.SourceLineIndex));
        Assert.Equal(3, document.Pages[0].Lines.Count);
        Assert.Single(document.Pages[1].Lines);
        Assert.Single(document.Pages[2].Lines);
    }

    [Fact]
    public void Empty_pages_and_trailing_lines_are_retained()
    {
        DialogueDocument document = DialogueTextFormatter.Parse("{Page}{Next}{Line}");
        Assert.Equal(3, document.Pages.Count);
        Assert.Equal(2, document.Pages[2].Lines.Count);
        Assert.Equal("\n\n\n", document.Text);
        Assert.Equal("One\nTwo\nThree", DialogueTextFormatter.Format("One\r\nTwo\rThree"));
    }

    [Fact]
    public void Character_and_misc_name_references_use_the_native_naming_subjects()
    {
        Dictionary<int, string> names = new() { [0] = "Leon", [1] = "Cecilia", [2] = "Dog", [3] = "Unrelated" };
        const string encoded = "{Char Squall}/{Char Rinoa}/{Char Angelo1}/{Char Angelo2}/{Char Zell}/{Char Quistis}/{Name Squall}/{Name Rinoa}/{Name Angelo}/{Name Shiva}";
        Assert.Equal("Leon/Cecilia/Dog/Dog/Zell/Quistis/Leon/Cecilia/Dog/Shiva", DialogueTextFormatter.Format(encoded, names));
        Assert.Equal("Squall/Rinoa/Angelo", DialogueTextFormatter.Format("{Char Squall}/{Char Rinoa}/{Char Angelo2}"));
    }

    [Theory]
    [InlineData(TextTagTerm.FishermansHorizon, "Fisherman's Horizon")]
    [InlineData(TextTagTerm.BalambGarden, "Balamb Garden")]
    [InlineData(TextTagTerm.TrabiaGarden, "Trabia Garden")]
    [InlineData(TextTagTerm.EastAcademy, "East Academy")]
    [InlineData(TextTagTerm.EastAcademyStation, "East Academy Station")]
    [InlineData(TextTagTerm.DesertPrison, "D-District Prison")]
    [InlineData(TextTagTerm.DesertPrisonStation, "D-District Prison Station")]
    [InlineData(TextTagTerm.LunarGate, "Lunar Gate")]
    [InlineData(TextTagTerm.LunarBase, "Lunar Base")]
    [InlineData(TextTagTerm.ShumiVillage, "Shumi Village")]
    [InlineData(TextTagTerm.DelingCity, "Deling City")]
    [InlineData(TextTagTerm.DolletStation, "Dollet Station")]
    [InlineData(TextTagTerm.UltimeciaCastle, "Ultimecia Castle")]
    [InlineData(TextTagTerm.Balamb, "Balamb")]
    [InlineData(TextTagTerm.Restores, "Restores")]
    public void Place_and_term_references_expand_to_readable_names(TextTagTerm term, string expected)
    {
        Assert.Equal(expected, DialogueTextFormatter.Format($"{{Term {term}}}"));
    }

    [Fact]
    public void All_defined_colors_have_a_semantic_color_and_independent_blink_flag()
    {
        TextTagColor[] colors = Enum.GetValues<TextTagColor>();
        Assert.Equal(16, colors.Length);
        for (int index = 0; index < 8; index++)
        {
            DialogueRun normal = Assert.Single(Assert.Single(Assert.Single(DialogueTextFormatter.Parse($"{{Color {colors[index]}}}text").Pages).Lines).Runs);
            DialogueRun blinking = Assert.Single(Assert.Single(Assert.Single(DialogueTextFormatter.Parse($"{{Color {colors[index + 8]}}}text").Pages).Lines).Runs);
            Assert.False(normal.Blink);
            Assert.True(blinking.Blink);
            Assert.Equal(normal.Color, blinking.Color);
        }
    }

    [Fact]
    public void Inline_colors_preserve_text_and_persist_across_pages_until_changed()
    {
        DialogueDocument document = DialogueTextFormatter.Parse("White{Color Red}Red{Color Red}Again{Page}Still{Color BlueBlink}Blue{Color White}White");
        Assert.Equal(2, document.Pages[0].Lines[0].Runs.Count);
        Assert.Equal(new DialogueRun("White", Color.White), document.Pages[0].Lines[0].Runs[0]);
        Assert.Equal(new DialogueRun("RedAgain", Color.Red), document.Pages[0].Lines[0].Runs[1]);
        Assert.Equal(new DialogueRun("Still", Color.Red), document.Pages[1].Lines[0].Runs[0]);
        Assert.Equal(new DialogueRun("Blue", Color.CornflowerBlue, true), document.Pages[1].Lines[0].Runs[1]);
        Assert.Equal(new DialogueRun("White", Color.White), document.Pages[1].Lines[0].Runs[2]);
    }

    [Theory]
    [InlineData("\"", "\"")]
    [InlineData("'", "'")]
    [InlineData("“", "”")]
    [InlineData("”", "“")]
    [InlineData("„", "“")]
    [InlineData("‟", "”")]
    [InlineData("‘", "’")]
    [InlineData("‚", "‘")]
    [InlineData("‛", "’")]
    [InlineData("«", "»")]
    [InlineData("»", "«")]
    [InlineData("‹", "›")]
    [InlineData("›", "‹")]
    [InlineData("「", "」")]
    [InlineData("『", "』")]
    [InlineData("｢", "｣")]
    [InlineData("〝", "〞")]
    [InlineData("〝", "〟")]
    [InlineData("＂", "＂")]
    [InlineData("＇", "＇")]
    public void Named_speech_recognizes_codepage_and_unicode_quotes_and_hanging_indent(string opening, string closing)
    {
        DialogueDocument document = DialogueTextFormatter.Parse($"Instructor{{Line}}{opening}First{{Line}}second{{Page}}third{closing}{{Line}}After");
        Assert.Equal("Instructor", document.Pages[0].Speaker);
        Assert.Equal("Instructor", document.Pages[1].Speaker);
        Assert.True(document.Pages[0].Lines[0].IsSpeaker);
        DialogueLine first = document.Pages[0].Lines[1];
        Assert.True(first.IsQuoteStart);
        Assert.Equal(opening, first.HangingIndent);
        Assert.False(document.Pages[0].Lines[2].IsQuoteStart);
        Assert.Equal(opening, document.Pages[0].Lines[2].HangingIndent);
        Assert.Equal(opening, document.Pages[1].Lines[0].HangingIndent);
        Assert.Empty(document.Pages[1].Lines[1].HangingIndent);
        Assert.False(document.Pages[1].Lines[0].IsSpeaker);
    }

    [Fact]
    public void Plain_body_is_not_mistaken_for_a_speaker_and_explicit_speaker_is_supported()
    {
        DialoguePage ordinary = Assert.Single(DialogueTextFormatter.Parse("Choose:{Line}Yes{Line}No").Pages);
        Assert.Null(ordinary.Speaker);
        Assert.All(ordinary.Lines, line => Assert.False(line.IsSpeaker));
        DialoguePage explicitSpeaker = Assert.Single(DialogueTextFormatter.Parse("{Speaker}Instructor{Line}Unquoted instruction").Pages);
        Assert.Equal("Instructor", explicitSpeaker.Speaker);
        Assert.True(explicitSpeaker.Lines[0].IsSpeaker);
    }

    [Fact]
    public void Speaker_can_change_at_a_source_page_and_custom_names_are_resolved_first()
    {
        DialogueDocument document = DialogueTextFormatter.Parse("{Char Squall}{Line}“First.”{Next}{Char Rinoa}{Line}“Second.”", new Dictionary<int, string> { [0] = "Leon", [1] = "Cecilia" });
        Assert.Equal("Leon", document.Pages[0].Speaker);
        Assert.Equal("Cecilia", document.Pages[1].Speaker);
        Assert.True(document.Pages[1].Lines[0].IsSpeaker);
        Assert.Equal(2, document.Pages[1].Lines[0].SourceLineIndex);
    }

    [Fact]
    public void Continuing_quoted_text_cannot_be_inferred_as_a_new_speaker()
    {
        DialogueDocument document = DialogueTextFormatter.Parse("Squall{Line}“First{Next}continued{Line}‘nested’{Line}last.”");
        Assert.Equal("Squall", document.Pages[1].Speaker);
        Assert.All(document.Pages[1].Lines, line => Assert.False(line.IsSpeaker));
        Assert.All(document.Pages[1].Lines, line => Assert.Equal("“", line.HangingIndent));
    }

    [Fact]
    public void Unnamed_quoted_text_and_thoughts_do_not_require_a_speaker()
    {
        DialoguePage speech = Assert.Single(DialogueTextFormatter.Parse("「First{Line}second」").Pages);
        Assert.Null(speech.Speaker);
        Assert.True(speech.Lines[0].IsQuoteStart);
        DialoguePage thought = Assert.Single(DialogueTextFormatter.Parse("{Color Red}(Thinking.)").Pages);
        Assert.Null(thought.Speaker);
        Assert.True(thought.Lines[0].IsThought);
        Assert.All(thought.Lines[0].Runs, run => Assert.Equal(new Color(208, 208, 208), run.Color));
    }

    [Theory]
    [InlineData("[brackets]")]
    [InlineData("〖brackets〗")]
    public void Brackets_do_not_create_a_speaker_heading_or_quote_indentation(string text)
    {
        DialoguePage page = Assert.Single(DialogueTextFormatter.Parse($"Location{{Line}}{text}").Pages);
        Assert.Null(page.Speaker);
        Assert.All(page.Lines, line => Assert.False(line.IsSpeaker));
        Assert.All(page.Lines, line => Assert.Empty(line.HangingIndent));
    }

    [Theory]
    [InlineData("Press {Key Unknown} to get off.")]
    [InlineData("{Dialog SelectedGF}")]
    [InlineData("{Option 1}")]
    [InlineData("{Unknown 1}")]
    public void Unrelated_placeholders_remain_literal_text_for_their_owning_ui(string encoded)
    {
        Assert.Equal(encoded, DialogueTextFormatter.Format(encoded));
    }

    [Theory]
    [InlineData("(", ")")]
    [InlineData("（", "）")]
    public void Thoughts_are_grey_across_lines_pages_and_explicit_color_changes(string opening, string closing)
    {
        DialogueDocument document = DialogueTextFormatter.Parse($"{{Char Squall}}{{Line}}{{Color Red}}{opening}First{{Line}}{{Color BlueBlink}}nested (thought){{Next}}last{closing}{{Color Green}}Normal");
        Assert.Equal("Squall", document.Pages[0].Speaker);
        Assert.True(document.Pages[0].Lines[0].IsSpeaker);
        Assert.True(document.Pages[0].Lines[1].IsThought);
        Assert.True(document.Pages[0].Lines[2].IsThought);
        Assert.All(document.Pages[0].Lines.Skip(1).SelectMany(line => line.Runs), run => Assert.Equal(new Color(208, 208, 208), run.Color));
        Assert.Equal(new DialogueRun($"last{closing}", new Color(208, 208, 208), true), document.Pages[1].Lines[0].Runs[0]);
        Assert.Equal(new DialogueRun("Normal", Color.Lime), document.Pages[1].Lines[0].Runs[1]);
    }

    [Fact]
    public void Message_variables_are_expanded_before_building_runs()
    {
        NativeMessageVariables bank = NativeMessageVariables.FromColdProcess().WithWrite(1, -1);
        DialogueDocument document = DialogueTextFormatter.Parse("{Color Yellow}{Var 33}/{Var49}/{Var65}", messageVariables: bank);
        Assert.Equal("4,294,967,295/4294967295/FFFFFFFF", document.Text);
        Assert.Equal(Color.Yellow, document.Pages[0].Lines[0].Runs[0].Color);
        Assert.Throws<NotSupportedException>(() => DialogueTextFormatter.Parse("{Var 33}"));
    }

    [Theory]
    [InlineData("{Color Ultraviolet}")]
    [InlineData("{Char UnknownCharacter2}")]
    [InlineData("{Char 255}")]
    [InlineData("{Term MissingPlace}")]
    [InlineData("{Name MissingName}")]
    public void Unresolved_supported_references_fail_explicitly(string encoded)
    {
        Assert.Throws<NotSupportedException>(() => DialogueTextFormatter.Parse(encoded));
    }
}
