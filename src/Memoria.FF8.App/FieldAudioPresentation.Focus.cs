namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    private bool focusPaused;
    private bool nativeMidiPaused;

    public void SetFocusPaused(bool paused)
    {
        if (focusPaused == paused)
        {
            return;
        }
        focusPaused = paused;
        TryOutput(() => PauseExternalMusic(paused || nativeMidiPaused));
        foreach (Voice voice in voices.Values)
        {
            if (paused)
            {
                voice.Clock.Stop();
                voice.VolumeRamp?.Pause();
                voice.PanRamp?.Pause();
            }
            else
            {
                voice.Clock.Start();
                voice.VolumeRamp?.Resume();
                voice.PanRamp?.Resume();
            }
            TryOutput(() => SetBufferPaused(voice.Primary, paused));
            if (voice.Loop is not null)
            {
                TryOutput(() => SetBufferPaused(voice.Loop, paused));
            }
        }
        if (paused)
        {
            midiRamp?.Pause();
            streamRamp?.Pause();
            TryOutput(() => music?.Pause());
        }
        else
        {
            midiRamp?.Resume();
            streamRamp?.Resume();
            if (!nativeMidiPaused)
            {
                TryOutput(() => music?.Resume());
            }
        }
        if (streamedMusic is not null)
        {
            TryOutput(() => SetBufferPaused(streamedMusic, paused));
        }
    }

    private static void SetBufferPaused(DirectSoundOutput.Buffer buffer, bool paused)
    {
        if (paused)
        {
            buffer.Pause();
        }
        else
        {
            buffer.Resume();
        }
    }
}
