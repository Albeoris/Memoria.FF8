using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

internal static class NativeChocoboCourseTests
{
    public static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure original resources for the Chocobo course regression.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using LgpArchive archive = LgpArchive.Open(Path.Combine(installation.DataDirectory, "minigame", "chocobo.lgp"));
        byte[] bytes = archive.ReadEntry(archive.Find("chocobo.wat").Single());
        byte[] original = bytes.ToArray();
        foreach (bool shortCourse in new[] { false, true })
        {
            PcChocoboCourse course = Ff7ChocoboResources.LoadCourse(installation, shortCourse);
            Check(course.Track.Count == (shortCourse ? 945 : 1181), "Native course length was not retained.");
            Check(course.Guide.Length == course.Track.Count * 8 && course.MapPointers.Count == course.Track.Count + 4, "Native guidance and visibility owners differ from the course.");
            Check(course.Triangles.Length == (shortCourse ? 10146 : 11849) * 28, "Native triangle storage differs.");
            Check(course.Quads.Length == (shortCourse ? 4157 : 4713) * 40, "Native quad storage differs.");
            Check(course.Track.Any(point => point.Left.X < 0 || point.Left.Y < 0 || point.Left.Z < 0), "Signed native track coordinates were lost.");
            Console.WriteLine($"PASS original Chocobo {(shortCourse ? "short" : "long")} course: {course.Track.Count} sections");
        }
        foreach (int length in new[] { 0, 1, 0x6EBA, 0x9E078, 0x1243F1 })
        {
            Reject(bytes[..length]);
        }
        foreach (int offset in new[] { 0, 0x6EBA, 0x93B2, 0x9E073, 0xA3912, 0x1243EF })
        {
            byte[] corrupt = bytes.ToArray();
            corrupt[offset] ^= 0x7F;
            Reject(corrupt);
        }
        Check(bytes.SequenceEqual(original), "Reading original courses mutated the input resource.");
        Console.WriteLine("PASS Chocobo course truncation, marker/count corruption and immutable input");
    }

    private static void Reject(byte[] bytes)
    {
        try
        {
            PcChocoboCourse.Read(bytes, false);
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("A malformed Chocobo course was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
