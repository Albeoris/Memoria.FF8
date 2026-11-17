using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void InitializeNativeDialogueText(DialogueState dialogue)
    {
        //TODO(TD-012): Unsupported controls and scrolling windows retain the existing semantic input policy until their text process is represented.
        if (dialogue.NativeWindow is not { } window || dialogue.IsNamePrompt)
        {
            return;
        }
        StringBuilder glyphs = new();
        string text = dialogue.Text;
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (current != '{')
            {
                if (char.IsControl(current) && current is not ('\r' or '\n'))
                {
                    return;
                }
                if (current is not ('\r' or '\n'))
                {
                    glyphs.Append(current);
                }
                continue;
            }
            int end = text.IndexOf('}', index + 1);
            if (end < 0)
            {
                return;
            }
            string command = text[(index + 1)..end];
            if (command is "Line" or "End")
            {
                // Authored fixed-height windows can require the native scrolling phase.
                if (command == "Line" && Scene.DialogueWindows.ContainsKey(dialogue.Channel))
                {
                    return;
                }
            }
            else if (command.StartsWith("Char ", StringComparison.Ordinal))
            {
                int subject = Enumerable.Range(0, 11).Select(GameNames.CharacterSubject).FirstOrDefault(subject => GameNames.DefaultName(subject) == command[5..], -1);
                if (subject < 0)
                {
                    return;
                }
                glyphs.Append(Scene.Names.GetValueOrDefault(subject, GameNames.DefaultName(subject)));
            }
            else
            {
                return;
            }
            index = end;
        }
        window.Text = new(glyphs.ToString(), 0, false);
    }

    private void StepNativeDialogueText(DialogueState dialogue)
    {
        if (dialogue.NativeWindow?.Text is not { Ready: false } text)
        {
            return;
        }
        NativeMessageChannelTiming timing = Scene.MessageChannelTiming.GetValueOrDefault(dialogue.Channel, new(4096, 0));
        short speed = unchecked((short)timing.Speed16);
        short phase = unchecked((short)(timing.Counter20 + speed));
        if (speed == 0 || phase >= 4096)
        {
            phase = unchecked((short)(phase - 4096));
            do
            {
                if (text.Position == text.Glyphs.Length)
                {
                    text.Ready = true;
                    break;
                }
                text.Position += char.IsHighSurrogate(text.Glyphs[text.Position]) && text.Position + 1 < text.Glyphs.Length && char.IsLowSurrogate(text.Glyphs[text.Position + 1]) ? 2 : 1;
                if (speed != 0 && phase < 4096)
                {
                    break;
                }
                if (speed != 0)
                {
                    phase = unchecked((short)(phase - 4096));
                }
            }
            while (!text.Ready);
        }
        Scene.MessageChannelTiming[dialogue.Channel] = timing with { Counter20 = unchecked((ushort)phase) };
    }
}
