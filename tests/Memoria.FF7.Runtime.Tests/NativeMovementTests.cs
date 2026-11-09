using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;

internal static class NativeMovementTests
{
    public static void Run()
    {
        CardinalStepAndArrival();
        TriangleAndBlockedAccess();
        CollisionAndPushOwnership();
        HeightAndSlopeRounding();
    }

    public static void RunInstalled()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }
        var installation = Ff7Installation.Discover(root);
        var native = Ff7PcExecutableData.Load(installation);
        var field = Ff7FieldResources.Load(installation, "md1stin");
        var movement = new Ff7NativeMovement(field.Walkmesh, native.NativeSinCos, native.NativeArcAngles);
        int slopes = 0;
        foreach (PcWalkmeshTriangle triangle in field.Walkmesh)
        {
            int x = ((int)triangle.A.X + (int)triangle.B.X + (int)triangle.C.X) * 4096 / 3;
            int y = ((int)triangle.A.Y + (int)triangle.B.Y + (int)triangle.C.Y) * 4096 / 3;
            int z = movement.ResolveHeight(x / 4096, y / 4096, triangle.Index);
            var actor = Actor(0, 0, 0) with { Triangle = triangle.Index, X4096 = x, Y4096 = y, Z4096 = z * 4096 };
            var result = movement.StepDirection(actor, 64, [actor], []);
            Equal(true, result.Actor.Triangle >= 0 && result.Actor.Triangle < field.Walkmesh.Count);
            if (triangle.A.Z != triangle.B.Z || triangle.A.Z != triangle.C.Z)
            {
                slopes++;
            }
        }
        Equal(39, field.Walkmesh.Count);
        Equal(13, slopes);
    }

    public static void RunProbeCarry()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(100, 300, 0), new(100, 100, 0), new(0, 100, 0), -1, 1, 3),
            new(1, new(0, 100, 0), new(100, 100, 0), new(100, 0, 0), 0, 2, -1),
            new(2, new(100, 100, 0), new(200, 100, 0), new(100, 0, 0), -1, -1, 1),
            new(3, new(100, 300, 0), new(0, 100, 0), new(0, 300, 0), 0, -1, -1)
        ];
        Ff7NativeMovement movement = Movement(mesh);
        Ff7Actor actor = Actor(0, 74, 103) with { X4096 = 74 * 4096 + 1564, Y4096 = 103 * 4096 + 2072, CollisionRadius = 34, PlayerControlled = true, Direction = 37 };
        Ff7Actor cold = JsonSerializer.Deserialize<Ff7Actor>(JsonSerializer.Serialize(actor))!;
        bool arrived = false;
        for (int step = 0; step < 128 && !arrived; step++)
        {
            Ff7NativeMoveResult next = movement.Step(actor, 83, 102, [actor], [], true);
            Ff7NativeMoveResult restored = movement.Step(cold, 83, 102, [cold], [], true);
            Equal(next.Actor, restored.Actor);
            Equal(next.Arrived, restored.Arrived);
            if (step == 0)
            {
                Equal(true, next.Moved);
                Equal(true, next.Actor.X4096 > actor.X4096);
            }
            actor = next.Actor with { Direction = 37 };
            cold = JsonSerializer.Deserialize<Ff7Actor>(JsonSerializer.Serialize(restored.Actor with { Direction = 37 }))!;
            arrived = next.Arrived;
        }
        Equal(true, arrived);
        Equal(83 * 4096, actor.X4096);
        Equal(102 * 4096, actor.Y4096);
        InstalledProbeCarry();
    }

    private static void InstalledProbeCarry()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        string? expected = Environment.GetEnvironmentVariable("MEMORIA_FF7_MOVEMENT_EXPECTED_STEPS");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(expected) || !int.TryParse(Environment.GetEnvironmentVariable("MEMORIA_FF7_MOVEMENT_ENTITY"), out int entity) || !short.TryParse(Environment.GetEnvironmentVariable("MEMORIA_FF7_MOVEMENT_TARGET_X"), out short targetX) || !short.TryParse(Environment.GetEnvironmentVariable("MEMORIA_FF7_MOVEMENT_TARGET_Y"), out short targetY))
        {
            Console.WriteLine("SKIP original-resource movement probe continuation: optional fixture inputs are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7Actor actor = session.Host.GetActor(entity);
        Ff7NativeMovement movement = session.Host.Movement!;
        foreach (string row in expected.Split(';'))
        {
            int[] values = row.Split(',').Select(int.Parse).ToArray();
            Equal(3, values.Length);
            Ff7NativeMoveResult result = movement.Step(actor, targetX, targetY, session.Host.Process.Actors.Select(item => item.Slot == actor.Slot ? actor : item).ToArray(), session.Host.Process.BlockedTriangles, session.Host.Process.PlayerLocked);
            Equal(true, result.Moved);
            Equal(false, result.Arrived);
            Equal(values[0], result.Actor.X4096);
            Equal(values[1], result.Actor.Y4096);
            Equal(values[2], result.Actor.Triangle);
            actor = result.Actor with { Direction = actor.Direction };
        }
        Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    private static void CardinalStepAndArrival()
    {
        var movement = Movement(FlatMesh());
        var actor = Actor(0, 10, 10);
        var result = movement.Step(actor, 30, 10, [actor], []);
        Equal(true, result.Moved);
        Equal(false, result.Arrived);
        Equal(14 * 4096, result.Actor.X4096);
        Equal(10 * 4096, result.Actor.Y4096);
        Equal(64, result.Actor.Direction);
        result = movement.Step(result.Actor, 17, 10, [actor], []);
        Equal(true, result.Arrived);
        Equal(17 * 4096, result.Actor.X4096);
        Equal((byte)0, movement.Direction(0, -100));
        Equal((byte)128, movement.Direction(0, 100));
        Equal((byte)192, movement.Direction(-100, 0));
    }

    private static void TriangleAndBlockedAccess()
    {
        var movement = Movement(FlatMesh());
        var actor = Actor(0, 49, 49);
        var crossed = movement.StepDirection(actor, 96, [actor], []);
        Equal(true, crossed.Moved);
        Equal(1, crossed.Actor.Triangle);
        var blocked = movement.StepDirection(actor, 96, [actor], new byte[] { 2 });
        Equal(false, blocked.Moved);
        Equal(0, blocked.Actor.Triangle);
        Equal(actor.X4096, blocked.Actor.X4096);
    }

    private static void CollisionAndPushOwnership()
    {
        var movement = Movement(FlatMesh());
        var actor = Actor(0, 10, 10) with { PlayerControlled = true, CollisionRadius = 10 };
        var other = Actor(1, 14, 10) with { CollisionRadius = 30 };
        var result = movement.StepDirection(actor, 64, [actor, other], [], playerLocked: false);
        Equal(false, result.Moved);
        Equal(1, result.PushEntities.Single());
        result = movement.StepDirection(actor, 64, [actor, other with { CollisionEnabled = false }], [], playerLocked: false);
        Equal(true, result.Moved);
        Equal(0, result.PushEntities.Length);
        result = movement.StepDirection(actor, 64, [actor, other with { Z4096 = 128 * 4096 }], [], playerLocked: false);
        Equal(true, result.Moved);
    }

    private static void HeightAndSlopeRounding()
    {
        var mesh = new[] { new PcWalkmeshTriangle(0, new Vector3(0, 0, 0), new Vector3(0, 100, 100), new Vector3(100, 0, 0), -1, -1, -1) };
        var movement = Movement(mesh);
        Equal(25, movement.ResolveHeight(25, 25, 0));
        var actor = Actor(0, 10, 10) with { Z4096 = 10 * 4096 };
        var result = movement.StepDirection(actor, 128, [actor], []);
        Equal(true, result.Moved);
        Equal(10 * 4096 + 11588, result.Actor.Y4096);
        Equal(12 * 4096, result.Actor.Z4096);
    }

    private static Ff7NativeMovement Movement(IReadOnlyList<PcWalkmeshTriangle> mesh)
    {
        // Authored analytic fixtures exercise table consumption; original lookup validation belongs to resources.
        var vectors = new short[512];
        for (int direction = 0; direction < 256; direction++)
        {
            vectors[direction * 2] = (short)Math.Round(Math.Sin(direction * Math.PI / 128) * 4096);
            vectors[direction * 2 + 1] = (short)Math.Round(Math.Cos(direction * Math.PI / 128) * 4096);
        }
        var angles = new byte[129];
        for (int index = 0; index < angles.Length; index++)
        {
            angles[index] = (byte)Math.Round(Math.Asin(index / 128.0) * 128 / Math.PI);
        }
        return new Ff7NativeMovement(mesh, vectors, angles);
    }

    private static PcWalkmeshTriangle[] FlatMesh() =>
    [
        new(0, new Vector3(0, 0, 0), new Vector3(0, 100, 0), new Vector3(100, 0, 0), -1, 1, -1),
        new(1, new Vector3(0, 100, 0), new Vector3(100, 100, 0), new Vector3(100, 0, 0), -1, -1, 0)
    ];
    private static Ff7Actor Actor(int entity, int x, int y) => new() { Slot = entity, Entity = entity, Model = entity, X4096 = x * 4096, Y4096 = y * 4096, MoveSpeed = 1024, CollisionEnabled = true };
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
