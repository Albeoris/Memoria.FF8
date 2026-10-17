using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class DialoguePresentation
{
    internal const float Padding = 8;

    public static IEnumerable<DialogueState> DrawOrder(IEnumerable<DialogueState> dialogues)
    {
        // Native channel submission gives lower channels the nearer depth.
        // The host paints without that depth buffer, so submit foreground channels last.
        return dialogues.OrderByDescending(dialogue => dialogue.Channel);
    }

    public static PixelBounds Bounds(DialogueState dialogue, DialogueWindow? window, PixelBounds viewport, float textWidth, float textHeight)
    {
        float scale = viewport.Width / 320f;
        float width = textWidth / scale + 2 * Padding;
        float height = textHeight / scale + 2 * Padding;
        float x = window?.X ?? dialogue.X;
        float y = window?.Y ?? dialogue.Y;
        if (window is not null)
        {
            // Native WINSIZE (529A20) positions the authored rectangle before MES opens it.
            // Tightening a short presentation page must not move that established origin down.
            (x, y) = Position(unchecked((short)window.X), unchecked((short)window.Y), unchecked((short)window.Width), unchecked((short)window.Height));
        }
        (x, y) = Position(x, y, width, height);
        return new PixelBounds(viewport.X + (int)MathF.Round(x * scale), viewport.Y + (int)MathF.Round(y * scale), Math.Max(1, (int)MathF.Ceiling(width * scale)), Math.Max(1, (int)MathF.Ceiling(height * scale)));
    }

    private static (float X, float Y) Position(float x, float y, float width, float height)
    {
        // Shared native WINSIZE/AMES screen limits (529A20/5291E0).
        if (x + width >= 304)
        {
            x = 312 - width;
        }
        if (y + height >= 224)
        {
            y = 224 - height;
        }
        x = Math.Max(8, x);
        y = Math.Max(8, y);
        return (x, y);
    }

    public static PlayerAction? ResolveInput(DialogueState dialogue, InputFrame input, ref int selectedChoice, string enteredName)
    {
        if (!dialogue.CanConfirm)
        {
            return null;
        }
        if (dialogue.FirstChoice is int first && dialogue.LastChoice is int last)
        {
            selectedChoice = Math.Clamp(selectedChoice + input.MenuVertical, first, last);
            if (input.Cancel && !input.Confirm && dialogue.CancelChoice is int cancelChoice)
            {
                selectedChoice = cancelChoice;
            }
        }
        if (input.Confirm)
        {
            return dialogue.IsNamePrompt ? new PlayerAction(PlayerActionKind.EnterName, Text: enteredName) : dialogue.FirstChoice.HasValue ? new PlayerAction(PlayerActionKind.Choose, selectedChoice) : new PlayerAction(PlayerActionKind.Confirm);
        }
        if (input.Cancel && !dialogue.IsNamePrompt && dialogue.FirstChoice is null)
        {
            return new PlayerAction(PlayerActionKind.Cancel);
        }
        return null;
    }

    public static (int Selection, string Name) ResolveDraft(DialogueState? dialogue, DialogueDraft? draft, string defaultName)
    {
        if (dialogue is not null && draft is not null && draft.Channel == dialogue.Channel && draft.MessageId == dialogue.MessageId)
        {
            return (draft.SelectedChoice, draft.EnteredName);
        }
        return (dialogue?.InitialChoice ?? dialogue?.FirstChoice ?? 0, defaultName);
    }
}
