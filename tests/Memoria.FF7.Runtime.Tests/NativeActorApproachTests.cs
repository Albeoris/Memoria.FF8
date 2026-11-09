using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeActorApproachTests
{
    public static void Run()
    {
        Ff7FieldHost fixture = Build(new() { Actors = [Actor(7, 0, 10), Actor(1, 1, 300)] });
        Ff7NativeMovement movement = fixture.Movement!;
        Ff7Actor actor = Actor(7, 0, 10) with { X4096 = 0, Y4096 = 0 };
        Equal(true, movement.StepToActor(actor, 87 * 4096 + 123, 0, 30, [actor], [], true).Arrived);
        Equal(false, movement.StepToActor(actor, 88 * 4096, 0, 30, [actor], [], true).Arrived);
        Equal(0, movement.StepToActor(actor, 87 * 4096 + 123, 0, 30, [actor], [], true).Actor.X4096);
        Equal(4096 + 123, movement.StepToActor(actor, 4096 + 123, 0, 0, [actor], [], true).Actor.X4096);
        actor = actor with { CollisionRadius = 65535 };
        Equal(false, movement.StepToActor(actor, 65 * 4096, 0, 1, [actor], [], true).Arrived);
        actor = actor with { CollisionRadius = 1 };
        Equal(true, movement.StepToActor(actor, 64 * 4096, 0, -1, [actor], [], true).Arrived);
        Equal(false, movement.StepToActor(actor, 65 * 4096, 0, -1, [actor], [], true).Arrived);

        FieldHostRequest request = new(1700, 7, 2, 16, new FieldActorMove(0, 0, TargetEntity: 1));
        Equal(false, fixture.Execute(request).Completed);
        Equal(0, fixture.GetActor(7).Animation);
        Equal(300 * 4096, fixture.Process.Motions.Single().EntityTarget!.Y4096);
        fixture = MoveTarget(fixture, 20);
        Ff7FieldHost cold = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(fixture.Capture()))!);
        fixture.AdvanceActors();
        cold.AdvanceActors();
        Equal(JsonSerializer.Serialize(fixture.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Equal(false, fixture.Process.Motions.Single().Completed);
        Equal(true, fixture.GetActor(7).Y4096 > 10 * 4096);
        Equal(false, fixture.Execute(request).Completed);
        Equal(2, fixture.GetActor(7).Animation);
        Equal(20 * 4096, fixture.Process.Motions.Single().EntityTarget!.Y4096);
        fixture.AdvanceActors();
        Equal(true, fixture.Process.Motions.Single().Completed);
        int stoppedY = fixture.GetActor(7).Y4096;
        fixture = MoveTarget(fixture, 500);
        cold = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(fixture.Capture()))!);
        Equal(true, fixture.Execute(request).Completed);
        Equal(true, cold.Execute(request).Completed);
        Equal(stoppedY, fixture.GetActor(7).Y4096);
        Equal(0, fixture.Process.Motions.Length);
        Equal(0, fixture.GetActor(7).AnimationStatus);
        Equal(JsonSerializer.Serialize(fixture.Capture()), JsonSerializer.Serialize(cold.Capture()));
        var cancelled = request with { Id = 1701 };
        Equal(false, fixture.Execute(cancelled).Completed);
        fixture.Cancel(cancelled);
        Equal(0, fixture.Process.Motions.Length);
    }

    private static Ff7Actor Actor(int entity, int slot, int y) => new() { Entity = entity, Slot = slot, Model = slot, X4096 = 10 * 4096, Y4096 = y * 4096, MoveSpeed = 4096, CollisionRadius = 30, CollisionEnabled = false };

    private static Ff7FieldHost MoveTarget(Ff7FieldHost host, int y)
    {
        return Build(host.Capture() with { Actors = host.Process.Actors.Select(actor => actor.Entity == 1 ? actor with { Y4096 = y * 4096 } : actor).ToArray() });
    }

    private static Ff7FieldHost Build(Ff7FieldProcess process)
    {
        byte[] script = new byte[34];
        BinaryPrimitives.WriteUInt16LittleEndian(script, 0x0502);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(4), 32);
        BinaryPrimitives.WriteInt16LittleEndian(script.AsSpan(8), 512);
        PcWalkmeshTriangle triangle = new(0, new Vector3(0, 0, 0), new Vector3(0, 1000, 0), new Vector3(1000, 0, 0), -1, -1, -1);
        var field = new Ff7FieldResources("approach-fixture", null!, PcFieldScripts.Read(script), null!, [], [triangle], null!, null!, null!);
        short[] directions = new short[512];
        directions[257] = -4096;
        return new Ff7FieldHost(field, (_, _) => 4, process) { Movement = new([triangle], directions, new byte[129]) };
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
