using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardAutoPlayTests
{
    internal static void Run()
    {
        if (Ff7SnowboardAutoPlay.ChooseDirection(new(100, -100), Vector2.Zero, 0, 0, 2) != 0x8000 || Ff7SnowboardAutoPlay.ChooseDirection(new(-100, -100), Vector2.Zero, 0, 0, 2) != 0x2000 || Ff7SnowboardAutoPlay.ChooseDirection(new(0, -100), Vector2.Zero, 0, 0, 2) != 0x1000 || Ff7SnowboardAutoPlay.ChooseDirection(new(100, -100), new(0, -100), 0, 600, 2) != 0x2000)
        {
            throw new InvalidOperationException("The snowboard input advice lost native heading recovery, alignment or reversed push steering.");
        }
        if (Ff7SnowboardAutoPlay.ChooseDirection(new(100, 0), new(0, -100), 0, 600, 1) != 0x4000)
        {
            throw new InvalidOperationException("The snowboard input advice did not brake before a turn beyond the forward diagonal.");
        }
        if (Ff7SnowboardAutoPlay.ChooseDirection(new(4, -100), Vector2.Zero, 0, 0, 1) != 0x1000)
        {
            throw new InvalidOperationException("The snowboard input advice oscillated across its two-substep heading quantum instead of pushing forward.");
        }
        byte[] tile = new byte[76 * Ff7SnowboardTerrain.TileSize];
        tile[0x254] = 8;
        tile[0x255] = 0;
        tile[0x256] = 1;
        int vertex = 8 + 10 * 8;
        tile[vertex] = 100;
        tile[vertex + 4] = 56;
        tile[vertex + 5] = 255;
        byte[] before = tile.ToArray();
        Ff7SnowboardCourseData course = new(1, 0, new byte[] { 0 }, [], [], [], new short[] { 1024 }, new[] { 10, 20, 30 }, 0);
        Ff7SnowboardNativeData data = new(new[] { course }, tile, [], []);
        Vector3 target = Ff7SnowboardAutoPlay.ReadTarget(data, 0, 0, 0);
        (short x, short z) = Ff7SnowboardCourseNavigation.Rotate(100, -200, 1024);
        if (target != new Vector3((10 + x) * 4096, 20 * 4096, (30 + z) * 4096) || x >= 0 || z >= 0 || !tile.AsSpan().SequenceEqual(before))
        {
            throw new InvalidOperationException("The native snowboard target used setup's inverse rotation or changed the source collision geometry.");
        }
        tile[0] = 3;
        int edge = 8 + 13 * 8;
        tile[edge] = 44;
        tile[edge + 1] = 1;
        tile[edge + 4] = 56;
        tile[edge + 5] = 255;
        course = course with { Connections = 0x100, Angles = new short[] { 0 } };
        data = data with { Courses = new[] { course, course } };
        before = tile.ToArray();
        if (Ff7SnowboardAutoPlay.ReadTarget(data, 0, 0, 0) != new Vector3(210 * 4096, 20 * 4096, -170 * 4096) || !tile.AsSpan().SequenceEqual(before))
        {
            throw new InvalidOperationException("The snowboard fork advice did not use its admitted right-half geometry without changing native state.");
        }
        data = data with { Courses = new[] { course with { Connections = 0xFF00 } } };
        if (Ff7SnowboardAutoPlay.ReadTarget(data, 0, 0, 0).X != 110 * 4096)
        {
            throw new InvalidOperationException("The snowboard fork advice selected an absent native branch.");
        }
        Console.WriteLine("Native snowboard heading recovery, action-specific steering and forward triangle advice passed.");
    }
}
