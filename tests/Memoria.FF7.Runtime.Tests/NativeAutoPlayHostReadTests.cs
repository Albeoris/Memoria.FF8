using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayHostReadTests
{
    public static void Run()
    {
        Ff7Progress progress = new() { InitialKernelData = [], Characters = [], Party = [255, 255, 255], Inventory = [new(23, 1), new(24, 2)], Materia = [], Gil = 100, AvailableCharacters = 1 << 3 };
        Ff7FieldProcess process = new();
        FieldHostRead[] gil = [new(FieldHostReadKind.Gil)];
        string gilSignature = Read(gil, progress);
        Check(gilSignature != Read(gil, progress with { Gil = 101 }), "A gil query did not refresh when native money changed.");
        Check(gilSignature == Read(gil, progress with { Inventory = [new(23, 2)] }), "Unrelated inventory changes invalidated a gil-only dependency.");
        FieldHostRead[] item = [new(FieldHostReadKind.ItemQuantity, 23)];
        string itemSignature = Read(item, progress);
        Check(itemSignature != Read(item, progress with { Inventory = [new(23, 2), new(24, 2)] }), "A specific native item quantity did not refresh.");
        Check(itemSignature == Read(item, progress with { Inventory = [new(23, 1), new(24, 3)], Gil = 101 }), "An unrelated item or gil change invalidated a specific item read.");
        FieldHostRead[] party = [new(FieldHostReadKind.PartyAvailable, 3)];
        Check(Read(party, progress) != Read(party, progress with { AvailableCharacters = 0 }), "A native party-availability bit did not refresh.");
        Check(Read(party, progress) == Read(party, progress with { AvailableCharacters = (1 << 3) | (1 << 4) }), "An unrelated party bit changed an availability dependency.");
        Check(!Ff7AutoPlayDiscovery.ReadHostConditions([new(FieldHostReadKind.Random)], progress, process).Complete, "An unsupported random host dependency was silently admitted.");
        Check(!Ff7AutoPlayDiscovery.ReadHostConditions([new(FieldHostReadKind.PreviousMap)], progress, process).Complete, "An absent native previous-map fact was silently supplied.");
        FieldHostRead[] movieFrame = [new(FieldHostReadKind.MovieFrame)];
        Ff7AutoPlayHostConditions unpreparedMovie = Ff7AutoPlayDiscovery.ReadHostConditions(movieFrame, progress, process);
        Ff7FieldProcess preparedMovie = process with { Movie = new(5, false, 0, false, null) };
        Ff7AutoPlayHostConditions preparedFrame = Ff7AutoPlayDiscovery.ReadHostConditions(movieFrame, progress, preparedMovie);
        Check(unpreparedMovie.Complete && preparedFrame.Complete && unpreparedMovie.Fingerprint != preparedFrame.Fingerprint, "Preflight lost the distinction between an unprepared movie and prepared frame zero.");
        Check(preparedFrame.Fingerprint != Ff7AutoPlayDiscovery.ReadHostConditions(movieFrame, progress, preparedMovie with { Movie = preparedMovie.Movie! with { Frame = 1 } }).Fingerprint, "Native movie-frame progress did not refresh its dependency.");
        Check(process.Movie is null && preparedMovie.Movie!.Frame == 0, "Movie dependency observation changed the source presentation state.");
        Check(Ff7AutoPlayDiscovery.ReadHostConditions([new(FieldHostReadKind.MusicPlaying)], progress, process, () => false).Fingerprint != Ff7AutoPlayDiscovery.ReadHostConditions([new(FieldHostReadKind.MusicPlaying)], progress, process, () => true).Fingerprint, "A native playing-state change did not refresh its query dependency.");

        string Read(FieldHostRead[] reads, Ff7Progress current)
        {
            Ff7AutoPlayHostConditions result = Ff7AutoPlayDiscovery.ReadHostConditions(reads, current, process);
            Check(result.Complete, "A supported external host dependency was not projected.");
            return result.Fingerprint;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
