namespace Memoria.FF7.Formats;

public static class PcFieldMapList
{
    public static IReadOnlyList<string> Read(ReadOnlySpan<byte> source)
    {
        BinaryCursor cursor = new(source);
        ushort count = cursor.U16();
        if (cursor.Remaining != count * 32)
        {
            throw new InvalidDataException("FF7 map list does not match its fixed-width name count.");
        }
        string[] names = new string[count];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = cursor.Ascii(32);
        }
        return Array.AsReadOnly(names);
    }
}

public sealed record PcMovieCamera(PcFieldCamera Camera, ushort Flags);
public static class PcMovieCameras
{
    public static IReadOnlyList<PcMovieCamera> Read(ReadOnlySpan<byte> source)
    {
        if (source.Length % 40 != 0)
        {
            throw new InvalidDataException("FF7 movie camera track requires 40-byte records.");
        }
        BinaryCursor cursor = new(source);
        PcMovieCamera[] frames = new PcMovieCamera[source.Length / 40];
        for (int i = 0; i < frames.Length; i++)
        {
            PcFieldCamera camera = PcFieldCameras.Read(cursor.Take(38))[0];
            frames[i] = new PcMovieCamera(camera, cursor.U16());
        }
        return Array.AsReadOnly(frames);
    }
}
