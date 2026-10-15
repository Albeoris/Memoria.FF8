using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class DialoguePresentation
{
    internal const float Padding = 8;

    public static PixelBounds Bounds(DialogueState dialogue, DialogueWindow? window, PixelBounds viewport, float textWidth, float textHeight)
    {
        float scale = viewport.Width / 320f;
        float width = textWidth / scale + 2 * Padding;
        float height = textHeight / scale + 2 * Padding;
        float x = window?.X ?? dialogue.X;
        float y = window?.Y ?? dialogue.Y;
        if (x + width > 312)
        {
            x = 312 - width;
        }
        if (y + height > 232)
        {
            y = 232 - height;
        }
        x = Math.Max(8, x);
        y = Math.Max(8, y);
        return new PixelBounds(viewport.X + (int)MathF.Round(x * scale), viewport.Y + (int)MathF.Round(y * scale), Math.Max(1, (int)MathF.Ceiling(width * scale)), Math.Max(1, (int)MathF.Ceiling(height * scale)));
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
