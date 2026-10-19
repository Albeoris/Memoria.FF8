using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed partial class FieldAudioPresentation
{
    internal const int TripleTriadMusicTrack = 70;
    internal const int TripleTriadVictoryTrack = 1;

    public void PlayTripleTriadMusic(bool victory = false)
    {
        // Native5225A0 requests70;538170 builds AKAO byte2, decoded by46B530 as track1.
        TryOutput(() => PlayMusic(victory ? TripleTriadVictoryTrack : TripleTriadMusicTrack, 0, 127, -1));
    }

    internal static uint? ResolveTripleTriadSound(TripleTriadSound cue)
    {
        return cue switch
        {
            TripleTriadSound.Move or TripleTriadSound.Confirm => 1,
            TripleTriadSound.Cancel => 9,
            TripleTriadSound.Error => 16,
            TripleTriadSound.Place or TripleTriadSound.CardReceived => 89,
            TripleTriadSound.NewCard => 18,
            TripleTriadSound.Flip => 90,
            TripleTriadSound.Win or TripleTriadSound.Lose or TripleTriadSound.Draw or TripleTriadSound.Same or TripleTriadSound.Plus or TripleTriadSound.Combo => null,
            _ => throw new ArgumentOutOfRangeException(nameof(cue))
        };
    }

    public void PlayTripleTriadSound(TripleTriadSound cue)
    {
        TryOutput(() =>
        {
            if (ResolveTripleTriadSound(cue) is uint soundId)
            {
                // Native5380D0/538110 ->538020 ->46B2A0;90 uses channel1, other cues use the ordinary queue.
                PlayPresentationSound(soundId, cue == TripleTriadSound.Flip ? -31 : -30);
            }
        });
    }
}
