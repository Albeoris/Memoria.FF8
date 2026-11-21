using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardSurfaceTests
{
    internal static void Run()
    {
        Ff7SnowboardNativeData data = Fixture();
        Ff7SnowboardCourseNavigation navigation = new(data);
        Check(navigation.Reconcile(new((243 + 10) * 4096, 0, (-76 + 30) * 4096), default).Accepted, "The surface fixture did not reach its native local triangle.");
        Ff7SnowboardSurfaceSample sample = Ff7SnowboardSurface.Sample(data, navigation);
        Check(sample.Normal == new Vector3(1059, 11020, -2539) && sample.FloorHeightQ12 == 61 * 4096 && sample.CourseDirection == 0, "The raw shifted normal or signed integer plane height differs from the native vector.");

        Ff7SnowboardNativeData rotatedData = Fixture(angle: 512);
        Ff7SnowboardSurfaceSample rotated = Ff7SnowboardSurface.Sample(rotatedData, new(rotatedData));
        Check(rotated.Normal == new Vector3(-1046, 11020, -2543) && rotated.CourseDirection == 512, "The surface normal used a normalized vector or the wrong course rotation sign.");

        Ff7SnowboardNativeData quarterData = Fixture(angle: 1024);
        Ff7SnowboardSurfaceSample quarter = Ff7SnowboardSurface.Sample(quarterData, new(quarterData));
        Check(quarter.Normal == new Vector3(-2538, 11020, -1058), "The quarter-turn normal lost the native truncated matrix coefficients.");

        Ff7SnowboardNativeData previousData = Fixture(previousUpperRow: true);
        Ff7SnowboardCourseNavigation nextSegment = new(previousData);
        nextSegment.Reset(0, 1);
        Check(nextSegment.Reconcile(new(243 * 4096, 0, -76 * 4096), default).Accepted, "The next-segment surface fixture lost its local coordinates.");
        Ff7SnowboardSurfaceSample seam = Ff7SnowboardSurface.Sample(previousData, nextSegment);
        Check(seam.FloorHeightQ12 == 142 * 4096 && seam.Normal == sample.Normal, "The seam failed to use the previous tile's relative upper-row heights while retaining the raw current normal.");

        foreach ((short curve, short expected) in new[] { ((short)1000, (short)39), ((short)-1000, (short)4065) })
        {
            Ff7SnowboardNativeData curved = Fixture(curvature: curve);
            Ff7SnowboardCourseNavigation curvedNavigation = new(curved);
            Check(curvedNavigation.Reconcile(new((243 + 10) * 4096, 0, (-76 + 30) * 4096), default).Accepted && Ff7SnowboardSurface.Sample(curved, curvedNavigation).CourseDirection == expected, "The curved tile did not retain the native atan2 argument order and signed curvature branch.");
        }

        Check(Ff7SnowboardSurface.InitialPosition(data) == new Vector3(4755456, 786432, -3244032), "The initial position replaced the native authored midpoint fields with a triangle centroid.");
        Check(Ff7SnowboardSurface.InitialPosition(rotatedData) == new Vector3(5750784, 786432, 1073152), "The initial midpoint used the wrong course rotation sign.");
        Check(Ff7SnowboardSurface.InitialPosition(quarterData) == new Vector3(3403776, 786432, 4833280), "The initial midpoint changed native quarter-turn truncation.");
        Check(navigation.CurrentTriangle == 71 && navigation.LocalX == 243 && navigation.LocalZ == -76, "Surface sampling changed the navigation owner.");
    }

    private static Ff7SnowboardNativeData Fixture(short angle = 0, short curvature = 0, bool previousUpperRow = false)
    {
        byte[] geometry = new byte[76 * Ff7SnowboardTerrain.TileSize];
        for (int tile = 0; tile < 76; tile++)
        {
            Span<byte> data = geometry.AsSpan(tile * Ff7SnowboardTerrain.TileSize, Ff7SnowboardTerrain.TileSize);
            Word(data, 4, curvature);
            Vertex(data, 0, 0, 0, 0);
            Vertex(data, 1, 385, 37, 0);
            Vertex(data, 8, 346, 86, -229);
            Word(data, 0x0E, -332);
            Word(data, 0x16, 1000);
            Word(data, 0x1E, 2634);
            Word(data, 0x22, -39);
            Word(data, 0x172, 384);
            Word(data, 0x26, 0);
            Word(data, 0x2E, -1000);
            Word(data, 0x36, -1643);
            Word(data, 0x38, 800);
            Word(data, 0x3E, 1000);
            for (int triangle = 0; triangle < 72; triangle++)
            {
                int offset = 0x254 + triangle * 4;
                data[offset] = 8;
                data[offset + 1] = 0;
                data[offset + 2] = 1;
            }
        }
        if (previousUpperRow)
        {
            Span<byte> previous = geometry.AsSpan(Ff7SnowboardTerrain.TileSize, Ff7SnowboardTerrain.TileSize);
            Word(previous, 0x15A, 100);
            Word(previous, 0x162, 140);
        }
        Ff7SnowboardCourseData[] courses = new Ff7SnowboardCourseData[7];
        for (int index = 0; index < courses.Length; index++)
        {
            courses[index] = new(0xFF000002, 0x101, previousUpperRow ? new byte[] { 1, 0 } : new byte[2], new byte[2], new byte[2], new int[2], new short[] { angle, angle }, new int[] { 10, 20, 30, 0, 100, 0 }, 1);
        }
        return new(courses, geometry, new byte[64], new byte[7]);
    }

    private static void Vertex(Span<byte> data, int index, short x, short y, short z)
    {
        Word(data, 8 + index * 8, x);
        Word(data, 10 + index * 8, y);
        Word(data, 12 + index * 8, z);
    }

    private static void Word(Span<byte> data, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(data[offset..], value);

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
