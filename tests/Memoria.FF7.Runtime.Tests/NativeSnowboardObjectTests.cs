using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardObjectTests
{
    internal static void Run()
    {
        Check(Ff7SnowboardObjects.ContactRow(0, -64, 0, 0, 0, 0, 160, 64, 0) == 1, "The lower obstacle height boundary must remain inclusive and select the upper response row.");
        Check(Ff7SnowboardObjects.ContactRow(0, -65, 0, 0, 0, 0, 160, 64, 0) is null, "An actor above the native obstacle height band must not collide.");
        Check(Ff7SnowboardObjects.ContactRow(0, 256, 0, 0, 0, 0, 160, 64, 0) == 0, "The fixed lower height boundary must remain inclusive.");
        Check(Ff7SnowboardObjects.ContactRow(0, 257, 0, 0, 0, 0, 160, 64, 0) is null, "The native contact band does not extend beyond 256 units below an object.");
        Check(Ff7SnowboardObjects.ContactRow(160, 0, 0, 0, 0, 0, 160, 64, 0) == 0, "The exact radial boundary must collide.");
        Check(Ff7SnowboardObjects.ContactRow(160, 0, 17, 0, 0, 0, 160, 64, 0) == 0, "The native integer square root truncation must be applied before comparing the radius.");
        Check(Ff7SnowboardObjects.ContactRow(160, 0, 18, 0, 0, 0, 160, 64, 0) is null, "The contact circle must reject the first integer square root outside its radius.");
        Check(Ff7SnowboardObjects.ContactRow(161, 0, 0, 0, 0, 0, 160, 64, 0) is null, "The native early axis rejection must remain active.");
        Check(Ff7SnowboardObjects.ContactRow(0, 0, 0, 0, 0, 0, 0, 64, 0) is null, "A disabled collision radius must never mark contact.");
        Check(Ff7SnowboardObjects.ContactRow(-100, 30, 50, -100, 40, 50, 128, 192, 10) == 0, "The response split uses strict less-than rather than less-than-or-equal.");
        Check(Ff7SnowboardObjects.ContactRow(-100, 29, 50, -100, 40, 50, 128, 192, 10) == 1, "The alternate response row must use the object's authored split height.");
        ImpactLifecycle();
        VisibleForkWindow();
        ObjectTransform();
        ClearanceHazard();
        PlaneHazard();
        foreach ((short frame, short expected) in new[] { ((short)0, (short)0), ((short)1, (short)-10), ((short)8, (short)-76), ((short)16, (short)-141), ((short)32, (short)-196), ((short)64, (short)0), ((short)121, (short)68) })
        {
            Check(Ff7SnowboardObjects.BendingPitch(frame) == expected, "The bending obstacle lost the native nested sine, intermediate truncation, or signed shift.");
        }
        foreach ((short frame, short expected) in new[] { ((short)0, (short)0), ((short)1, (short)0), ((short)2, (short)1), ((short)16, (short)12), ((short)32, (short)25), ((short)64, (short)50), ((short)128, (short)100) })
        {
            Check(Ff7SnowboardObjects.TopplingPitchDelta(frame) == expected, "The toppling obstacle must add the native half-counter sine delta to its independent pitch.");
        }
    }

    private static void PlaneHazard()
    {
        short[] thresholds = [384, 512, 544, 544, 512, 384, 384, 512, 544, 544, 512, 384];
        Check(!Ff7SnowboardObjects.PlaneCollision(491, 491, 60, 8, 384 * 4096, 0, 60, thresholds), "The repeated plane collision must use a strict native clearance comparison.");
        Check(Ff7SnowboardObjects.PlaneCollision(491, 491, 61, 8, 385 * 4096, 0, 60, thresholds), "The native paired-triangle selector must select the first plane threshold.");
        Check(!Ff7SnowboardObjects.PlaneCollision(491, 491, 59, 8, 1000 * 4096, 0, 60, thresholds), "A negative signed triangle selector must not index the native threshold table.");
        Check(!Ff7SnowboardObjects.PlaneCollision(491, 491, 60, 0x800B, 1000 * 4096, 0, 60, thresholds), "An initialized falling action must not restart the repeated plane reaction.");
        Check(!Ff7SnowboardObjects.PlaneCollision(491, 490, 60, 8, 1000 * 4096, 0, 60, thresholds), "A visible plane producer must wait for its actual logical segment.");
        Check(Ff7SnowboardObjects.PlaneCollision(492, 492, 71, 8, 385 * 4096, 0, 48, thresholds), "The second plane producer must use its own triangle base through the final native triangle.");
        Check(Ff7SnowboardObjects.PlaneReactionVelocity(-300001.75f) == 300001 && Ff7SnowboardObjects.PlaneReactionVelocity(123.5f) == 262144, "The plane reaction must truncate and take the signed velocity magnitude before applying the native minimum downward speed.");
    }

    private static void ClearanceHazard()
    {
        Check(Ff7SnowboardObjects.ClearanceReaction(163, 163, 24, 8, 79 * 4096, 0) is null, "The clearance producer must not react below its native threshold.");
        Check(Ff7SnowboardObjects.ClearanceReaction(163, 163, 24, 8, 80 * 4096, 0) == 12, "The clearance producer must select action12 at its inclusive lower threshold.");
        Check(Ff7SnowboardObjects.ClearanceReaction(163, 163, 24, 8, 447 * 4096, 0) == 12, "Action12 must remain selected below the second threshold.");
        Check(Ff7SnowboardObjects.ClearanceReaction(163, 163, 24, 8, 448 * 4096, 0) == 10, "The second native clearance band must select action10.");
        Check(Ff7SnowboardObjects.ClearanceReaction(163, 163, 47, 8, 455 * 4096, 0) == 10, "The native triangle range and upper clearance band must remain inclusive at their final integer values.");
        Check(Ff7SnowboardObjects.ClearanceReaction(163, 163, 24, 8, 456 * 4096, 0) is null, "The clearance producer must not invent a reaction above its narrow upper band.");
        Check(Ff7SnowboardObjects.ClearanceReaction(163, 163, 23, 8, 100 * 4096, 0) is null && Ff7SnowboardObjects.ClearanceReaction(163, 163, 48, 8, 100 * 4096, 0) is null, "The clearance producer must use its native triangle selector, not a distance-only approximation.");
        Check(Ff7SnowboardObjects.ClearanceReaction(163, 163, 24, 0x800C, 100 * 4096, 0) is null && Ff7SnowboardObjects.ClearanceReaction(163, 163, 24, 0x800A, 100 * 4096, 0) is null, "The action comparison must use the low byte and preserve an already selected reaction.");
        Check(Ff7SnowboardObjects.ClearanceReaction(163, 162, 24, 8, 100 * 4096, 0) is null, "A visible future producer must not react before reaching its actual logical segment.");
    }

    private static void ObjectTransform()
    {
        Ff7SnowboardObjectFrame frame = new(234, new(100 * 4096, 200 * 4096, -300 * 4096), 512, 0, 0, 0) { Pitch = 256, Roll = 128 };
        Matrix4x4 matrix = Ff7SnowboardObjects.BuildObjectTransform(frame, new(10, 20, 30));
        Check(new Vector3(matrix.M11, matrix.M12, matrix.M13) * 4096 == new Vector3(2624, 1652, -2675) && new Vector3(matrix.M21, matrix.M22, matrix.M23) * 4096 == new Vector3(-738, 3711, 1567) && new Vector3(matrix.M31, matrix.M32, matrix.M33) * 4096 == new Vector3(3056, -522, 2675), "The object matrix must use the snowboard geometry mode's native Euler order and quantized coefficients.");
        Check(matrix.Translation == new Vector3(90, 180, -330), "Object translation must shift the integer Q12 position before subtracting the integer native base.");
        Matrix4x4 wrapped = Ff7SnowboardObjects.BuildObjectTransform(new(234, Vector3.Zero, 0, 3, 0, 0), Vector3.Zero);
        Check(wrapped.M11 == -8 && wrapped.M22 == -8 && wrapped.M33 == -8, "Native coefficient scaling must preserve signed WORD wrapping rather than using an inverse or floating scale.");
        Matrix4x4 terrain = Ff7SnowboardObjects.BuildTerrainTransform(1024, new(100, 200, -300), new(10, 20, 30));
        Check(terrain.M13 == -1 && terrain.M31 == 1 && terrain.Translation == new Vector3(90, 180, -330), "Terrain orientation must preserve the raw negative-angle matrix fields and native base subtraction.");
    }

    private static void VisibleForkWindow()
    {
        byte[] geometry = new byte[2 * Ff7SnowboardTerrain.TileSize];
        BinaryPrimitives.WriteInt16LittleEndian(geometry, 3);
        Ff7SnowboardCourseData source = new(0xFF000001, 0x0201, new byte[1], new byte[1], new byte[1], new int[1], new short[1], new int[3], 0);
        Ff7SnowboardCourseData branch = new(2, 0, new byte[] { 1, 1 }, new byte[2], new byte[2], new int[2], new short[2], new int[6], 1);
        Ff7SnowboardNativeData native = new(new[] { source, branch, branch }, geometry, new byte[64], new byte[7]);
        Ff7SnowboardObjectProducer[] producers = Enumerable.Range(0, 604).Select(_ => new Ff7SnowboardObjectProducer(Ff7SnowboardObjectProducerKind.None, [], 0, 0, 0, 0, 0)).ToArray();
        Ff7SnowboardObjects objects = new(new(producers, new short[5]), native);
        objects.BuildVisibleWindow(0, 0, 0, 3);
        Check(objects.VisibleSegments.SequenceEqual(new[] { new Ff7SnowboardVisibleSegment(0, 0), new(1, 0), new(1, 1), new(2, 0), new(2, 1) }), "The native fork window must visit the left branch then restore the right branch's remaining segment count.");
        objects.BuildVisibleWindow(0, 0, 0, 1);
        Check(objects.VisibleSegments.SequenceEqual(new[] { new Ff7SnowboardVisibleSegment(0, 0), new(1, 0), new(2, 0) }), "Both fork entries must remain visible even when the native extra fork decrement exhausts the forward count.");
    }

    private static void ImpactLifecycle()
    {
        Ff7SnowboardCourseData course = new(0xFF000001, 0, new byte[1], new byte[1], new byte[1], new int[1], new short[1], new int[3], 0);
        Ff7SnowboardNativeData native = new(Enumerable.Repeat(course, 7).ToArray(), new byte[76 * Ff7SnowboardTerrain.TileSize], new byte[64], new byte[7]) { CollisionActions = new byte[32 * 9] };
        Ff7SnowboardSimulation simulation = new(native, 1);
        simulation.Rider.Motion.Speed = 512;
        simulation.Rider.Motion.Velocity = new(100, 0, 200);
        byte[] template = new byte[72];
        BinaryPrimitives.WriteInt16LittleEndian(template.AsSpan(0x10), 96);
        BinaryPrimitives.WriteInt16LittleEndian(template.AsSpan(0x12), 256);
        template[0x16] = 128;
        template[0x17] = 128;
        template[0x18] = 128;
        template[0x19] = 128;
        Ff7SnowboardObjectProducer[] producers = Enumerable.Range(0, 604).Select(_ => new Ff7SnowboardObjectProducer(Ff7SnowboardObjectProducerKind.None, [], 0, 0, 0, 0, 0)).ToArray();
        producers[0] = new(Ff7SnowboardObjectProducerKind.ImpactObstacle, template, 0, 0, 0, 1082, 0);
        Ff7SnowboardObjects objects = new(new(producers, new short[5]), native);
        List<Ff7SnowboardObjectSound> sounds = [];
        objects.Sound += sounds.Add;
        objects.Advance(simulation, 0, 0, 0);
        Check((simulation.Rider.Script.ActionWord & 255) == 12 && objects.Frames.Count == 1, "The first contact must select its native action before the obstacle consumes the collision flags.");
        objects.Advance(simulation, 1, 0, 0);
        Check(sounds.SequenceEqual(new[] { new Ff7SnowboardObjectSound(3, 240, 0) }) && objects.Frames[0].PositionQ12 == Vector3.Zero, "The impact callback must run on the following update and begin its flight without integrating immediately.");
        objects.Advance(simulation, 2, 0, 0);
        Check(objects.Frames[0].PositionQ12 == new Vector3(100, -163840, 200) && objects.Frames[0].Pitch == -32, "The native obstacle flight must retain impact velocity, signed vertical velocity, and its independent pitch rotation.");
        Check(sounds.Count == 1, "A consumed impact must not emit its sound again or restart its callback.");

        byte[] decorationTemplate = new byte[72];
        BinaryPrimitives.WriteInt16LittleEndian(decorationTemplate, 271);
        BinaryPrimitives.WriteInt32LittleEndian(decorationTemplate.AsSpan(8), 2);
        producers[0] = new(Ff7SnowboardObjectProducerKind.StaticDecoration, decorationTemplate, 100, 200, 300, 2560, 0);
        Ff7SnowboardObjects decoration = new(new(producers, new short[5]), native);
        for (int tick = 0; tick < 100; tick++)
        {
            decoration.Advance(simulation, tick, 0, 0);
        }
        Check(decoration.Frames.Count == 1 && decoration.Frames[0].ModelIndex == 271 && decoration.Frames[0].NativePrimitiveKind == 2 && decoration.Frames[0].PositionQ12 == new Vector3(100, 200, 300), "A static native decoration must retain its textured consumer and exact position without accidentally running the balloon callback.");
        Check(decoration.ScoreCollected == 0, "A callback-free, noncolliding decoration must not produce collection credit.");

        simulation = new(native, 1);
        simulation.Rider.Motion.Speed = 512;
        BinaryPrimitives.WriteInt32LittleEndian(template.AsSpan(8), 2);
        producers[0] = new(Ff7SnowboardObjectProducerKind.TopplingObstacle, template, 0, 0, 0, 3456, 1);
        Ff7SnowboardObjects toppling = new(new(producers, new short[5]), native);
        List<Ff7SnowboardObjectSound> topplingSounds = [];
        toppling.Sound += topplingSounds.Add;
        for (int tick = 0; tick < 5; tick++)
        {
            toppling.Advance(simulation, tick, 0, 0);
        }
        Check(topplingSounds.SequenceEqual(new[] { new Ff7SnowboardObjectSound(1, 268, 127) }) && toppling.Frames[0].ModelIndex == 231 && toppling.Frames[0].Pitch == 1, "A toppling post must consume impact on the next tick, preserve its explicit volume, and then accumulate its native pitch delta.");

        simulation = new(native, 1);
        producers[0] = new(Ff7SnowboardObjectProducerKind.ImpactObstacle, template, 1000 * 4096, 0, 0, 2560, 2);
        Ff7SnowboardObjects jumping = new(new(producers, new short[5]), native);
        jumping.Advance(simulation, 0, 0, 0);
        Check(jumping.Frames[0].PositionQ12.Y == 0 && simulation.Rider.RandomSeed == 1, "The native zero jump counter must decrement before its first random reset.");
        jumping.Advance(simulation, 1, 0, 0);
        Check(jumping.Frames[0].ModelIndex == 236 && jumping.Frames[0].PositionQ12.Y == -32768 && simulation.Rider.RandomSeed == 2745024, "The jumping obstacle must consume the rider's shared CRT stream at its actual reset callback.");
        jumping.Advance(simulation, 2, 0, 0);
        Check(jumping.Frames[0].PositionQ12.Y == -61440 && simulation.Rider.RandomSeed == 2745024, "The native jump must integrate its retained velocity without consuming another random value.");
        jumping.Advance(simulation, 3, 0, 0);
        Check(jumping.Frames[0].PositionQ12.Y == -86016, "The jumping obstacle must retain native integer gravity and integration order.");

        simulation = new(native, 1);
        producers[0] = new(Ff7SnowboardObjectProducerKind.Balloon, template, 1000 * 4096, 0, 0, 0, 3) { TotalScoreThreshold = 60 };
        foreach (short previousTotal in new short[] { 0, 60, 61 })
        {
            Ff7SnowboardObjects guarded = new(new(producers, new short[5]), native, previousTotal);
            guarded.Advance(simulation, 0, 0, 0);
            Check(guarded.Frames.Count == (previousTotal > 60 ? 1 : 0) && guarded.TotalScore == previousTotal && guarded.ScoreCollected == 0, "The guarded balloon must use the signed previous total score with a strict comparison, independently of current balloon credit.");
        }

        Ff7SnowboardCourseData bounceCourse = new(0xFF000004, 0, new byte[4], new byte[4], new byte[4], new int[4], new short[4], new int[12], 3);
        Ff7SnowboardNativeData bounceNative = native with { Courses = Enumerable.Repeat(bounceCourse, 7).ToArray() };
        simulation = new(bounceNative, 1);
        producers = Enumerable.Range(0, 604).Select(_ => new Ff7SnowboardObjectProducer(Ff7SnowboardObjectProducerKind.None, [], 0, 0, 0, 0, 0)).ToArray();
        byte[] bounceTemplate = new byte[72];
        BinaryPrimitives.WriteInt16LittleEndian(bounceTemplate, 270);
        producers[3] = new(Ff7SnowboardObjectProducerKind.BouncingObstacle, bounceTemplate, 0, 0, 0, 1536, 0) { Bounce = new(1000 * 4096, 2000, 3000, 592000, -65536, 20) };
        Ff7SnowboardObjects bouncing = new(new(producers, new short[5]), bounceNative);
        List<Ff7SnowboardObjectSound> bounceSounds = [];
        bouncing.Sound += bounceSounds.Add;
        bouncing.Advance(simulation, 0, 0, 3);
        Check(bouncing.Frames.Single().PositionQ12.Y == 591824 && simulation.Rider.RandomSeed == 2745024, "A dormant nearby bouncing object must sample the shared CRT stream when it resets its authored origin.");
        bouncing.Advance(simulation, 1, 0, 3);
        Check(bouncing.Frames.Single().PositionQ12.Y == 592000 && bouncing.Frames[0].Roll == 32 && bounceSounds.Single() == new Ff7SnowboardObjectSound(4, 427, 64), "The first bounce must integrate before clamping, retain native roll, and emit its distance-derived volume.");
        bouncing.Advance(simulation, 2, 0, 3);
        Check(bouncing.Frames.Single().PositionQ12.Y == 573568 && bouncing.Frames[0].Roll == 64, "The bouncing object must retain the native halved reflected velocity after gravity.");
        for (int tick = 3; tick <= 21; tick++)
        {
            bouncing.Advance(simulation, tick, 0, 3);
        }
        Check(bouncing.Frames.Count == 0 && simulation.Rider.RandomSeed == 3357800067u, "An unhit bouncing object must return to its hidden delay and consume exactly one further shared random value.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
