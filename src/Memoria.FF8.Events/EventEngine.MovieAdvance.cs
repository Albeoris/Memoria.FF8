using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>The presentation owner's authenticated consecutive decoded-frame path.</summary>
    public Func<int, int>? AdvanceMovieFrames { get; set; }

    private int AdvanceMovieWait(int count)
    {
        Func<int, int> advance = AdvanceMovieFrames ?? throw new NotSupportedException("Ordinary movie waiting requires its authenticated media clock callback owner.");
        NativeMovieSource? source = services.MovieSource;
        long firstFrame = Frame;
        for (int index = 0; index < count && services.IsMoviePlaying && services.MovieSource == source && !transitionRequested && LastFault is null && !dialogues.Values.Any(dialogue => dialogue.CanConfirm); index++)
        {
            int decoded = advance(1);
            if (decoded is < 0 or > 1)
            {
                throw new InvalidDataException("Bounded media owner advanced outside its requested single decoded-frame budget.");
            }
            if (decoded == 0)
            {
                break;
            }
        }
        return checked((int)(Frame - firstFrame));
    }
}
