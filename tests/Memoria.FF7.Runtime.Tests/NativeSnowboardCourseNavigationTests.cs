using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardCourseNavigationTests
{
    internal static void Run()
    {
        Vector3 inside = new(25 * 4096, 17.75f, -25 * 4096);
        Vector3 previous = new(24 * 4096, 16.75f, -24 * 4096);
        Ff7SnowboardCourseNavigation normal = new(Fixture());
        Ff7SnowboardCourseReconciliation accepted = normal.Reconcile(inside, previous);
        Check(accepted.Accepted && !accepted.RequiresVelocityResponse && accepted.PositionQ12.Y == 17 && accepted.PreviousQ12.Y == 16, "Position reconciliation did not retain native float-to-integer truncation.");
        Check(normal.CurrentTriangle == 71 && normal.SurfaceFlag == 0xAB && normal.LocalX == 25 && normal.LocalZ == -25, "The normal cell did not publish the selected native triangle and local position.");

        Ff7SnowboardCourseNavigation rotated = new(Fixture(angle: 1024));
        Check(rotated.Reconcile(new(-25 * 4096, 0, -25 * 4096), previous).Accepted && rotated.LocalX == 24 && rotated.LocalZ == -24, "Course rotation rounded the native truncated quarter-turn sine to 4096.");

        Ff7SnowboardCourseNavigation next = new(Fixture(handler: 1));
        Check(next.Reconcile(inside, previous).Accepted && next.CurrentSegment == 1 && next.LogicalDistance == 1, "The forward cell did not advance the native course segment.");
        Ff7SnowboardCourseReconciliation end = next.Reconcile(inside, previous);
        Check(end.Accepted && end.CourseEnded && next.CurrentSegment == 1, "The story course ending advanced beyond its allocated final segment.");

        Ff7SnowboardCourseNavigation branch = new(Fixture(handler: 4));
        Check(branch.Reconcile(inside, previous).Accepted && branch.CurrentCourse == 1 && branch.CurrentSegment == 0 && branch.LogicalDistance == 100, "The forward branch did not retain its native destination and course distance.");

        Ff7SnowboardCourseNavigation previousCourse = new(Fixture(handler: 2));
        previousCourse.Reset(1, 0);
        Check(previousCourse.Reconcile(inside, previous).Accepted && previousCourse.CurrentCourse == 0 && previousCourse.CurrentSegment == 1, "The backward segment boundary did not select the predecessor's final segment.");

        Ff7SnowboardCourseNavigation rejected = new(Fixture(handler: 8));
        Ff7SnowboardCourseReconciliation blocked = rejected.Reconcile(inside, previous);
        Check(!blocked.Accepted && blocked.RequiresVelocityResponse && blocked.Collision?.Kind == 1 && blocked.PositionQ12 == new Vector3(previous.X, 16, previous.Z), "A rejected cell did not restore the prior position and expose the required velocity response.");
        Check(rejected.CurrentCourse == 0 && rejected.CurrentSegment == 0 && rejected.CurrentTriangle == 0, "A rejected cell committed speculative course state.");

        Ff7SnowboardCourseNavigation gatedBranch = new(Fixture(handler: 7, branchUnavailable: true));
        Ff7SnowboardCourseReconciliation gated = gatedBranch.Reconcile(new(200 * 4096, 0, -200 * 4096), previous);
        Check(!gated.Accepted && gated.Collision?.Kind == 4 && gatedBranch.CurrentCourse == 0, "The conditional right branch discarded the descriptor availability guard.");

        Ff7SnowboardCourseNavigation missingPredecessor = new(Fixture(handler: 2));
        Check(!missingPredecessor.Reconcile(inside, previous).Accepted && missingPredecessor.CurrentSegment == 0, "The missing predecessor sentinel became a guessed course transition.");

        Ff7SnowboardCourseNavigation saturated = new(Fixture(angle: 512));
        Ff7SnowboardCourseReconciliation overflow = saturated.Reconcile(new(-32768f * 4096, 0, -32768f * 4096), previous);
        Check(overflow.Collision is { LocalX: 0, LocalZ: short.MinValue }, "The signed local rotation wrapped instead of saturating its matrix result.");
    }

    private static Ff7SnowboardNativeData Fixture(byte handler = 0, short angle = 0, bool branchUnavailable = false)
    {
        byte[] tiles = new byte[76 * Ff7SnowboardTerrain.TileSize];
        for (int tile = 0; tile < 76; tile++)
        {
            Span<byte> data = tiles.AsSpan(tile * Ff7SnowboardTerrain.TileSize, Ff7SnowboardTerrain.TileSize);
            Word(data, 0x3E, 1000);
            Word(data, 8 + 8, 100);
            Word(data, 8 + 16 + 4, -100);
            for (int axis = 0; axis < 3; axis++)
            {
                Word(data, 0x0E + axis * 8, (short)(1000 + axis * 1000));
                Word(data, 0x26 + axis * 8, (short)(-1000 - axis * 1000));
            }
            for (int triangle = 0; triangle < 72; triangle++)
            {
                int offset = 0x254 + triangle * 4;
                data[offset] = 0;
                data[offset + 1] = 1;
                data[offset + 2] = 2;
                data[offset + 3] = 0xAB;
            }
        }
        Ff7SnowboardCourseData[] courses = new Ff7SnowboardCourseData[7];
        for (int course = 0; course < courses.Length; course++)
        {
            uint descriptor = 2u | (course == 0 ? 0xFF000000u : 0u) | (course == 1 && branchUnavailable ? 0x10000u : 0u);
            courses[course] = new(descriptor, 0x101, new byte[2], new byte[2], new byte[2], new int[2], new short[] { angle, angle }, new int[6], 1) { StartDistance = (short)(course * 100) };
        }
        return new(courses, tiles, Enumerable.Repeat(handler, 64).ToArray(), new byte[7]);
    }

    private static void Word(Span<byte> target, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(target[offset..], value);

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
