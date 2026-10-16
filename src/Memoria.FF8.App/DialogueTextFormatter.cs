using System.Text.RegularExpressions;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal static partial class DialogueTextFormatter
{
    public static string Format(string encoded, IReadOnlyDictionary<int, string>? names = null, NativeMessageVariables? messageVariables = null)
    {
        return Parse(encoded, names, messageVariables).Text;
    }

    public static DialogueDocument Parse(string encoded, IReadOnlyDictionary<int, string>? names = null, NativeMessageVariables? messageVariables = null)
    {
        string expanded = NativeMessageVariables.Expand(encoded, messageVariables);
        List<DialoguePage> pages = [];
        List<DialogueLine> lines = [];
        List<DialogueRun> runs = [];
        Color color = Color.White;
        bool blink = false;
        bool speakerMarker = false;
        int sourceLine = 0;
        int cursor = 0;
        DialoguePageBoundary boundary = DialoguePageBoundary.Initial;

        void Append(string text)
        {
            string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            string[] pieces = normalized.Split('\n');
            for (int index = 0; index < pieces.Length; index++)
            {
                AddRun(runs, pieces[index], color, blink);
                if (index < pieces.Length - 1)
                {
                    FinishLine();
                }
            }
        }

        void FinishLine()
        {
            lines.Add(new DialogueLine(runs.ToArray(), sourceLine++, IsSpeaker: speakerMarker));
            runs.Clear();
            speakerMarker = false;
        }

        void FinishPage(DialoguePageBoundary nextBoundary)
        {
            FinishLine();
            pages.Add(new DialoguePage(lines.ToArray(), Boundary: boundary));
            lines.Clear();
            boundary = nextBoundary;
        }

        foreach (Match match in InlineTag().Matches(expanded))
        {
            Append(expanded[cursor..match.Index]);
            string kind = match.Groups[1].Value;
            string parameter = match.Groups[2].Value;
            switch (kind)
            {
                case "Line":
                    FinishLine();
                    break;
                case "Page":
                    FinishPage(DialoguePageBoundary.Page);
                    break;
                case "Next":
                    FinishPage(DialoguePageBoundary.Next);
                    break;
                case "End":
                case "Pause":
                    break;
                case "Speaker":
                    speakerMarker = true;
                    break;
                case "Color":
                    (color, blink) = ResolveColor(ParseEnum<TextTagColor>(parameter, kind));
                    break;
                case "Char":
                    Append(CharacterName(ParseEnum<TextTagCharacter>(parameter, kind), names));
                    break;
                case "Name":
                    Append(MiscName(ParseEnum<TextTagName>(parameter, kind), names));
                    break;
                case "Term":
                    Append(TermName(ParseEnum<TextTagTerm>(parameter, kind)));
                    break;
                case "Key" when parameter == "65":
                    // Native common message4 uses key65 for the junctioned-magic J marker.
                    Append("J");
                    break;
                default:
                    // Preserve unrelated placeholders until their owning UI supplies a presentation contract.
                    Append(match.Value);
                    break;
            }
            cursor = match.Index + match.Length;
        }
        Append(expanded[cursor..]);
        FinishPage(DialoguePageBoundary.Initial);
        return Decorate(pages);
    }

    private static T ParseEnum<T>(string value, string tag) where T : struct, Enum
    {
        if (Enum.TryParse(value, out T result) && Enum.IsDefined(result))
        {
            return result;
        }
        throw new NotSupportedException($"Dialogue {tag} parameter '{value}' is unsupported.");
    }

    private static string CharacterName(TextTagCharacter character, IReadOnlyDictionary<int, string>? names)
    {
        int identity = character switch
        {
            >= TextTagCharacter.Squall and <= TextTagCharacter.Ward => GameNames.CharacterSubject((int)character - (int)TextTagCharacter.Squall),
            TextTagCharacter.Angelo1 or TextTagCharacter.Angelo2 => 2,
            TextTagCharacter.UnknownCharacter3 => GameNames.GrieverSubject,
            _ => -1
        };
        if (identity >= 0 && names?.TryGetValue(identity, out string? customName) == true)
        {
            return customName;
        }
        return character switch
        {
            TextTagCharacter.Angelo1 or TextTagCharacter.Angelo2 => "Angelo",
            TextTagCharacter.UnknownCharacter3 => GameNames.DefaultName(GameNames.GrieverSubject),
            TextTagCharacter.UnknownCharacter2 or TextTagCharacter.UnknownCharacter4 => throw new NotSupportedException($"Dialogue character {character} has no resolved name."),
            _ => character.ToString()
        };
    }

    private static string MiscName(TextTagName name, IReadOnlyDictionary<int, string>? names)
    {
        if (name is >= TextTagName.Quetzacotl and <= TextTagName.Eden)
        {
            int identity = (int)name - (int)TextTagName.Quetzacotl;
            int subject = GameNames.GuardianForceSubject(identity);
            return names?.GetValueOrDefault(subject) ?? GameNames.DefaultName(subject);
        }
        return name switch
        {
            TextTagName.Squall => CharacterName(TextTagCharacter.Squall, names),
            TextTagName.Rinoa => CharacterName(TextTagCharacter.Rinoa, names),
            TextTagName.Angelo => CharacterName(TextTagCharacter.Angelo1, names),
            _ => name.ToString()
        };
    }

    private static string TermName(TextTagTerm term)
    {
        return term switch
        {
            TextTagTerm.FishermansHorizon => "Fisherman's Horizon",
            TextTagTerm.EastAcademy => "East Academy",
            TextTagTerm.DesertPrison => "D-District Prison",
            TextTagTerm.TrabiaGarden => "Trabia Garden",
            TextTagTerm.LunarBase => "Lunar Base",
            TextTagTerm.ShumiVillage => "Shumi Village",
            TextTagTerm.DelingCity => "Deling City",
            TextTagTerm.BalambGarden => "Balamb Garden",
            TextTagTerm.EastAcademyStation => "East Academy Station",
            TextTagTerm.DolletStation => "Dollet Station",
            TextTagTerm.DesertPrisonStation => "D-District Prison Station",
            TextTagTerm.LunarGate => "Lunar Gate",
            TextTagTerm.UltimeciaCastle => "Ultimecia Castle",
            _ => term.ToString()
        };
    }

    private static (Color Color, bool Blink) ResolveColor(TextTagColor color)
    {
        return color switch
        {
            TextTagColor.Disabled => (Color.DarkGray, false),
            TextTagColor.Grey => (Color.Gray, false),
            TextTagColor.Yellow => (Color.Yellow, false),
            TextTagColor.Red => (Color.Red, false),
            TextTagColor.Green => (Color.Lime, false),
            TextTagColor.Blue => (Color.CornflowerBlue, false),
            TextTagColor.Purple => (Color.Violet, false),
            TextTagColor.White => (Color.White, false),
            TextTagColor.DisabledBlink => (Color.DarkGray, true),
            TextTagColor.GreyBlink => (Color.Gray, true),
            TextTagColor.YellowBlink => (Color.Yellow, true),
            TextTagColor.RedBlink => (Color.Red, true),
            TextTagColor.GreenBlink => (Color.Lime, true),
            TextTagColor.BlueBlink => (Color.CornflowerBlue, true),
            TextTagColor.PurpleBlink => (Color.Violet, true),
            TextTagColor.WhiteBlink => (Color.White, true),
            _ => throw new NotSupportedException($"Dialogue color {color} is unsupported.")
        };
    }

    private static DialogueDocument Decorate(List<DialoguePage> pages)
    {
        List<DialoguePage> result = [];
        string? speaker = null;
        string openingQuote = "";
        string closingQuotes = "";
        int thoughtDepth = 0;
        foreach (DialoguePage page in pages)
        {
            DialogueLine[] lines = page.Lines.ToArray();
            int first = Array.FindIndex(lines, line => line.Text.Trim().Length > 0);
            int next = first < 0 ? -1 : Array.FindIndex(lines, first + 1, line => line.Text.Trim().Length > 0);
            if (first >= 0 && (lines[first].IsSpeaker || openingQuote.Length == 0 && thoughtDepth == 0 && next >= 0 && IsBlockStart(lines[next].Text) && !IsBlockStart(lines[first].Text)))
            {
                lines[first] = lines[first] with { IsSpeaker = true };
                speaker = lines[first].Text.Trim();
            }
            for (int index = 0; index < lines.Length; index++)
            {
                DialogueLine line = lines[index];
                if (line.IsSpeaker)
                {
                    continue;
                }
                string trimmed = line.Text.Trim();
                string opening = "";
                string closing = "";
                bool quoteStart = openingQuote.Length == 0 && TryOpeningQuote(trimmed, out opening, out closing);
                if (quoteStart)
                {
                    openingQuote = opening;
                    closingQuotes = closing;
                }
                line = line with { HangingIndent = openingQuote, IsQuoteStart = quoteStart };
                if (openingQuote.Length > 0 && trimmed.Length > (quoteStart ? openingQuote.Length : 0) && closingQuotes.Contains(trimmed[^1]))
                {
                    openingQuote = "";
                    closingQuotes = "";
                }
                bool startsThought = thoughtDepth == 0 && trimmed.Length > 0 && trimmed[0] is '(' or '（';
                if (thoughtDepth > 0 || startsThought)
                {
                    List<DialogueRun> thoughtRuns = [];
                    foreach (DialogueRun run in line.Runs)
                    {
                        foreach (char character in run.Text)
                        {
                            if (character is '(' or '（')
                            {
                                thoughtDepth++;
                            }
                            AddRun(thoughtRuns, character.ToString(), thoughtDepth > 0 ? Color.Gray : run.Color, run.Blink);
                            if (character is ')' or '）' && thoughtDepth > 0)
                            {
                                thoughtDepth--;
                            }
                        }
                    }
                    line = line with { Runs = thoughtRuns.ToArray(), IsThought = true };
                }
                lines[index] = line;
            }
            result.Add(page with { Lines = lines, Speaker = speaker });
        }
        return new DialogueDocument(result);
    }

    private static bool IsBlockStart(string text)
    {
        string trimmed = text.TrimStart();
        return trimmed.Length > 0 && (trimmed[0] is '(' or '（' || TryOpeningQuote(trimmed, out _, out _));
    }

    private static bool TryOpeningQuote(string text, out string opening, out string closing)
    {
        opening = text.Length == 0 ? "" : text[..1];
        // European/Russian/Japanese codepages include corner, guillemet and curly quotation marks.
        // Unicode quotation marks cover pasted or translated dialogue, including reversed Russian curls.
        closing = opening switch
        {
            "\"" => "\"",
            "'" => "'",
            "“" => "”",
            "”" => "“”",
            "„" => "“”",
            "‟" => "”",
            "‘" => "’",
            "‚" => "‘’",
            "‛" => "’",
            "«" => "»",
            "»" => "«",
            "‹" => "›",
            "›" => "‹",
            "「" => "」",
            "『" => "』",
            "｢" => "｣",
            "〝" => "〞〟",
            "＂" => "＂",
            "＇" => "＇",
            _ => ""
        };
        return closing.Length > 0;
    }

    private static void AddRun(List<DialogueRun> runs, string text, Color color, bool blink)
    {
        if (text.Length == 0)
        {
            return;
        }
        if (runs.Count > 0 && runs[^1].Color == color && runs[^1].Blink == blink)
        {
            runs[^1] = runs[^1] with { Text = runs[^1].Text + text };
        }
        else
        {
            runs.Add(new DialogueRun(text, color, blink));
        }
    }

    [GeneratedRegex(@"\{([^\s}]+)(?:\s+([^}]+))?\}")]
    private static partial Regex InlineTag();
}
