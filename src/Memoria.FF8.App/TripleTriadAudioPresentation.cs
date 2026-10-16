using Memoria.FF8.GameUi;

namespace Memoria.FF8;

/// <summary>Device-only card-module sound and music; no VM receipts or saved match changes.</summary>
internal sealed class TripleTriadAudioPresentation : IDisposable
{
    private readonly FieldAudioPresentation output;
    private readonly TripleTriadController controller;
    private bool disposed;

    public TripleTriadAudioPresentation(FieldAudioPresentation output, TripleTriadController controller)
    {
        this.output = output;
        this.controller = controller;
        output.StopAll();
        output.PlayTripleTriadMusic();
        controller.SoundRequested += Play;
    }

    private void Play(TripleTriadSound cue)
    {
        if (cue == TripleTriadSound.Win)
        {
            output.PlayTripleTriadMusic(victory: true);
        }
        else if (cue == TripleTriadSound.Place)
        {
            output.PlayTripleTriadMusic();
        }
        output.PlayTripleTriadSound(cue);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        controller.SoundRequested -= Play;
        output.StopAll();
    }
}
