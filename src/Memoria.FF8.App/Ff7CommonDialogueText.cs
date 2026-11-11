using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF8.Events;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed record Ff7CommonDialogueText(DialogueDocument Document, int VisibleCharacters, int RetiredRows = 0)
{
    internal static Ff7CommonDialogueText Create(Ff7Dialogue dialogue, IReadOnlyList<FieldTextToken> tokens, Func<FieldTextToken, string> expand, Func<FieldTextToken, string> buttonLabel)
    {
        int page = 0;
        int row = 0;
        int visibleCharacters = 0;
        int retiredRows = Ff7DialoguePresentationPolicy.RetiredRows(dialogue);
        List<DialogueLine> lines = [];
        List<DialogueRun> runs = [];
        Color color = Color.White;
        void Add(string value, int index)
        {
            if (row < retiredRows || value.Length == 0)
            {
                return;
            }
            if (runs.LastOrDefault() is DialogueRun previous && previous.Color == color && !previous.IsButton)
            {
                runs[^1] = previous with { Text = previous.Text + value };
            }
            else
            {
                runs.Add(new DialogueRun(value, color));
            }
            if (index < dialogue.TokenPosition)
            {
                visibleCharacters += UiTextReveal.CharacterCount(value);
            }
            else if (index == dialogue.TokenPosition)
            {
                int remaining = dialogue.Presentation?.ExpansionOffset ?? 0;
                visibleCharacters += UiTextReveal.CharacterCount(UiTextReveal.VisiblePrefix(value, ref remaining));
            }
        }
        for (int index = 0; index < tokens.Count; index++)
        {
            FieldTextToken token = tokens[index];
            if (token.Kind == FieldTextTokenKind.NewPage)
            {
                if (page == dialogue.Page)
                {
                    break;
                }
                page++;
                continue;
            }
            if (page != dialogue.Page)
            {
                continue;
            }
            switch (token.Kind)
            {
                case FieldTextTokenKind.Glyph:
                case FieldTextTokenKind.CharacterName:
                case FieldTextTokenKind.PartyName:
                case FieldTextTokenKind.DecimalVariable:
                case FieldTextTokenKind.HexVariable:
                case FieldTextTokenKind.RightAlignedVariable:
                    Add(expand(token), index);
                    break;
                case FieldTextTokenKind.NewLine:
                    if (row >= retiredRows)
                    {
                        lines.Add(new DialogueLine(runs.ToArray(), row));
                    }
                    row++;
                    runs.Clear();
                    break;
                case FieldTextTokenKind.ControllerButton:
                    if (row < retiredRows)
                    {
                        break;
                    }
                    DialogueRun[] bindings = ButtonPromptText.BindingRuns(buttonLabel(token), color).ToArray();
                    runs.AddRange(bindings);
                    if (index < dialogue.TokenPosition)
                    {
                        visibleCharacters += bindings.Sum(run => run.IsButton ? 1 : UiTextReveal.CharacterCount(run.Text));
                    }
                    break;
                case FieldTextTokenKind.Color:
                    color = token.Argument switch
                    {
                        0 => Color.Gray,
                        1 => Color.CornflowerBlue,
                        2 => Color.Red,
                        3 => Color.Violet,
                        4 => Color.LimeGreen,
                        5 => Color.Cyan,
                        6 => Color.Yellow,
                        7 => Color.White,
                        _ => throw new InvalidDataException("FFVII dialogue color exceeds its native palette.")
                    };
                    break;
                case FieldTextTokenKind.Tab:
                    Add("    ", index);
                    break;
                case FieldTextTokenKind.ChoiceTab:
                case FieldTextTokenKind.FixedSpacing:
                case FieldTextTokenKind.Wait:
                case FieldTextTokenKind.Pause:
                    break;
                default:
                    throw new NotSupportedException($"FFVII dialogue token {token.Kind} requires its native layout or variable binding.");
            }
        }
        if (row >= retiredRows)
        {
            lines.Add(new DialogueLine(runs.ToArray(), row));
        }
        return new(new DialogueDocument([new DialoguePage(lines)]), visibleCharacters, retiredRows);
    }
}
