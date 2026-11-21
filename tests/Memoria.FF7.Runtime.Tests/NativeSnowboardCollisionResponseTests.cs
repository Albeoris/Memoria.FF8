using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardCollisionResponseTests
{
    internal static void Run()
    {
        Ff7SnowboardNativeData data = Fixture();
        Ff7SnowboardCourseNavigation navigation = new(data);
        Ff7SnowboardCollisionResponse response = new(data);
        Ff7SnowboardCourseCollision side = new(1, 6, 1, 75, 0, 0, -1);
        Vector3 incoming = new(300, -50, 400);
        Ff7SnowboardCollisionResult reflected = response.Apply(navigation, side, incoming, 0, 1536, 0);
        Check(reflected.Velocity == new Vector3(-61.58763122558594f, -50, 496.19244384765625f) && reflected.ReflectionAngle == 3596 && reflected.Action == 0 && reflected.Sound is null, "The side collision changed its native float reflection or sampled an uncommitted attempted course.");

        Ff7SnowboardCollisionResult damped = response.Apply(navigation, side, incoming, 22, 256, 0);
        Check(damped.Velocity == new Vector3(-46.19072341918945f, -50, 372.14434814453125f) && damped.Action == 22 && damped.Sound == new Ff7SnowboardCollisionSound(3, 309, 0), "The retained-action collision did not apply native three-quarter damping and sound.");

        Ff7SnowboardCourseCollision boundary = side with { Kind = 2 };
        Ff7SnowboardCollisionResult reversed = response.Apply(navigation, boundary, new(0, 3, 400), 1, 256, 0);
        Check(reversed.Velocity == new Vector3(-3.496911085676402e-05f, 3, -400) && reversed.Action == 12 && reversed.Sound == new Ff7SnowboardCollisionSound(3, 239, 0), "The direction-boundary response replaced native float sine or action selection.");

        Ff7SnowboardCollisionResult actionSuppressed = response.Apply(navigation, side, incoming, 2, 1536, 0);
        Ff7SnowboardCollisionResult slow = response.Apply(navigation, side, incoming, 1, 127, 0);
        Check(actionSuppressed.Velocity == new Vector3(0, -50, 0) && actionSuppressed.Action == 2 && actionSuppressed.Sound is null && slow.Velocity == new Vector3(0, -50, 0), "An airborne or slow response retained planar motion or modified vertical motion.");

        Ff7SnowboardNativeData thresholds = Fixture();
        byte[] actions = thresholds.CollisionActions.ToArray();
        actions[0] = 3;
        actions[1] = 4;
        actions[2] = 5;
        thresholds = thresholds with { CollisionActions = actions };
        Ff7SnowboardCollisionResponse bins = new(thresholds);
        Ff7SnowboardCourseNavigation thresholdNavigation = new(thresholds);
        Check(bins.Apply(thresholdNavigation, side, incoming, 0, 512, 0).Action == 3 && bins.Apply(thresholdNavigation, side, incoming, 0, 513, 0).Action == 4 && bins.Apply(thresholdNavigation, side, incoming, 0, 1280, 0).Action == 4 && bins.Apply(thresholdNavigation, side, incoming, 0, 1281, 0).Action == 5, "The response speed bins lost their strict native thresholds.");

        Check(navigation.Reconcile(new(25 * 4096, 0, -25 * 4096), default).Accepted && navigation.CurrentTriangle == 71, "The response fixture did not select its highest native surface triangle.");
        Ff7SnowboardCollisionResult surface = response.Apply(navigation, side, new(0, 0, 400), 1, 256, 0);
        Check(surface.Velocity == new Vector3(-198.22605895996094f, 0, 347.42828369140625f) && surface.Action == 9, "The side response discarded the retained triangle's boundary angle or animation-side adjustment.");
        Check(incoming == new Vector3(300, -50, 400) && navigation.CurrentCourse == 0 && navigation.CurrentSegment == 0 && navigation.CurrentTriangle == 71, "Collision response mutated its source motion or navigation owner.");
    }

    private static Ff7SnowboardNativeData Fixture()
    {
        byte[] geometry = new byte[76 * Ff7SnowboardTerrain.TileSize];
        for (int tile = 0; tile < 76; tile++)
        {
            Span<byte> data = geometry.AsSpan(tile * Ff7SnowboardTerrain.TileSize, Ff7SnowboardTerrain.TileSize);
            BinaryPrimitives.WriteInt16LittleEndian(data[0x3E..], 1000);
            BinaryPrimitives.WriteInt16LittleEndian(data[(8 + 8)..], 100);
            BinaryPrimitives.WriteInt16LittleEndian(data[(8 + 16 + 4)..], -100);
            for (int index = 0; index < 3; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(data[(0x0E + index * 8)..], (short)(1000 + index * 1000));
                BinaryPrimitives.WriteInt16LittleEndian(data[(0x26 + index * 8)..], (short)(-1000 - index * 1000));
            }
            for (int index = 0; index < 12; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(data[(0x4E + index * 8)..], index == 11 ? (short)-169 : (short)169);
            }
            for (int triangle = 0; triangle < 72; triangle++)
            {
                data[0x254 + triangle * 4 + 1] = 1;
                data[0x254 + triangle * 4 + 2] = 2;
            }
        }
        Ff7SnowboardCourseData[] courses = Enumerable.Range(0, 7).Select(_ => new Ff7SnowboardCourseData(0xFF000002, 0x101, new byte[2], new byte[2], new byte[2], new int[2], new short[2], new int[6], 1)).ToArray();
        byte[] actions = Enumerable.Repeat((byte)0xFF, 32 * 9).ToArray();
        new byte[] { 8, 8, 10, 8, 8, 12, 12, 12, 12 }.CopyTo(actions, 9);
        Array.Fill(actions, (byte)0xFE, 22 * 9, 9);
        return new(courses, geometry, new byte[64], new byte[7]) { CollisionActions = actions };
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
