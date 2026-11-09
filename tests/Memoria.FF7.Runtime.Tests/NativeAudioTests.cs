using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAudioTests
{
    public static void Run()
    {
        if (Ff7FieldHost.NormalizeNativeMusicTrack(0) != 0 || Ff7FieldHost.NormalizeNativeMusicTrack(1) != 2 || Ff7FieldHost.NormalizeNativeMusicTrack(98) != 98)
        {
            throw new Exception("The admitted native MIDI track alias changed.");
        }
        try
        {
            Ff7FieldHost.NormalizeNativeMusicTrack(99);
            throw new Exception("An absent native music entry was accepted.");
        }
        catch (InvalidDataException)
        {
        }
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original field music continuation; set MEMORIA_FF7_GAME_DIR.");
            return;
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(directory), "md1stin");
        Ff7FieldHost host = new(field, (_, _) => 1, new Ff7FieldProcess { NativeMusicTrack = 2, MusicTicks = 437, SavedNativeMusicTrack = 7, MusicVolume = 83, MusicLock = 1 });
        if (host.ResolveNativeMusicTrack(0) != 2 || host.ResolveNativeMusicTrack(1) != 1)
        {
            throw new Exception("Original AKAO local music identity was lost.");
        }
        host.PlayNativeMusic(1);
        Ff7FieldProcess outgoing = host.CreateNextFieldProcess();
        if (outgoing.NativeMusicTrack != 2 || outgoing.MusicTicks != 437 || outgoing.SavedNativeMusicTrack != 7 || outgoing.MusicVolume != 83 || outgoing.MusicLock != 1 || outgoing.Music is not null || outgoing.Actors.Length != 0)
        {
            throw new Exception("A field change reset global music or retained local field owners.");
        }
        Ff7FieldHost changing = new(field, (_, _) => 1, new Ff7FieldProcess { NativeMusicTrack = 8, MusicTicks = 54, NextFieldMusicTrack = 0 });
        Ff7FieldProcess changed = changing.CreateNextFieldProcess();
        if (changed.NativeMusicTrack != 2 || changed.MusicTicks != 0 || changed.NextFieldMusicTrack != -1)
        {
            throw new Exception("The outgoing field's local next-track identity did not resolve before transition.");
        }
    }
}
