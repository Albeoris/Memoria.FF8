using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class DialoguePresentation
{
    public static PixelBounds Bounds(DialogueState dialogue, DialogueWindow? window, PixelBounds viewport, float textWidth, float textHeight)
    {
        float scale = viewport.Width / 320f;
        float width = Math.Clamp(window?.Width ?? textWidth / scale + 16, 16, 304);
        float height = Math.Clamp(window?.Height ?? textHeight / scale + 17, 17, 216);
        float x = window?.X ?? dialogue.X;
        float y = window?.Y ?? dialogue.Y;
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
        return new PixelBounds(viewport.X + (int)MathF.Round(x * scale), viewport.Y + (int)MathF.Round(y * scale), Math.Max(1, (int)MathF.Round(width * scale)), Math.Max(1, (int)MathF.Round(height * scale)));
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
