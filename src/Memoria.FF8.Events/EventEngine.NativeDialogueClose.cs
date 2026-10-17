namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool WaitForNativeDialogueClose(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return false;
        }
        int channel = fiber.Stack.Peek();
        if ((uint)channel >= 8)
        {
            throw new NotSupportedException("WINCLOSE requires an original window channel from zero through seven.");
        }
        if (!dialogues.TryGetValue(channel, out DialogueState? dialogue))
        {
            return false;
        }
        NativeDialogueWindowState window = dialogue.NativeWindow ?? throw new NotSupportedException("WINCLOSE requires the retained native window progress and delta.");
        if (window.Progress == 0)
        {
            return false;
        }
        // 529B60/4A0660 writes the closing delta and returns1 without popping.
        window.Delta = -512;
        fiber.Position--;
        WaitFrames(fiber, 1);
        return true;
    }

    private void StepNativeDialogueWindows(int samples)
    {
        bool changed = false;
        foreach (DialogueState dialogue in dialogues.Values.ToArray())
        {
            if (dialogue.NativeWindow is not NativeDialogueWindowState window)
            {
                continue;
            }
            // 4767B0/49A970 runs two UI samples, four for a movie callback.
            // Each49FEB0 adds signed WORD30 to WORD28 and clamps to0..4096.
            int progress = window.Progress;
            for (int sample = 0; sample < samples; sample++)
            {
                progress = Math.Clamp(progress + window.Delta, 0, 4096);
            }
            if (progress != window.Progress)
            {
                // Persistent BYTE212 channels remain occupied at zero until
                // WINCLOSE clears both masks;529FF0's early cleanup excludes them.
                window.Progress = progress;
                changed = true;
            }
        }
        if (changed)
        {
            RefreshCurrentDialogue();
        }
    }

    private static void ValidateNativeDialogueWindows(IEnumerable<DialogueState> savedDialogues, bool native)
    {
        foreach (DialogueState dialogue in savedDialogues)
        {
            if (dialogue.NativeWindow is not NativeDialogueWindowState window)
            {
                if (native)
                {
                    throw new InvalidDataException("Saved native dialogue requires explicit window progress and delta; migrate its window state before loading.");
                }
                continue;
            }
            if (!native || window.Version != 1 || window.Progress is < 0 or > 4096 || window.Delta is not (-512 or 0 or 512))
            {
                throw new InvalidDataException("Saved native dialogue window has invalid progress, delta or version.");
            }
        }
    }
}
