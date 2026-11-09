using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeJumpTests
{
    public static void Run()
    {
        Ff7FieldHost host = Host(true);
        FieldHostRequest request = new(500, 0, 6, 16, new FieldActorJump(7, 5, 1, 3));
        Equal(false, host.Execute(request).Completed);
        Equal(2, host.GetActor(0).Animation);
        host.AdvanceActors();
        Equal(0, host.GetActor(0).Jump!.Step);
        Equal(0, host.GetActor(0).Z4096);
        Equal(16, host.GetActor(0).AnimationFrame16);
        Equal(2, host.GetActor(0).LastAnimationFrame);
        host.AdvanceActors();
        Equal(9557, host.GetActor(0).X4096);
        Equal(6826, host.GetActor(0).Y4096);
        Equal(29653, host.GetActor(0).Z4096);
        Ff7FieldHost restored = Host(true, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        for (int tick = 0; tick < 2; tick++)
        {
            AdvanceBoth();
        }
        Equal(40959, host.GetActor(0).Z4096);
        Equal(0, host.GetActor(0).Triangle);
        Equal(false, host.GetActor(0).Jump!.Completed);
        AdvanceBoth();
        Equal(true, host.GetActor(0).Jump!.Completed);
        Equal(1, host.GetActor(0).Triangle);
        Equal(40959, host.GetActor(0).Z4096);
        Equal(32, host.GetActor(0).AnimationFrame16);
        AdvanceBoth();
        Equal(0, host.GetActor(0).AnimationFrame16);
        Equal(40959, host.GetActor(0).Z4096);
        Equal(true, host.Execute(request).Completed);
        Equal(true, restored.Execute(request).Completed);
        Equal(true, host.GetActor(0).Jump is null);
        Ff7FieldHost npc = Host(false);
        npc.Execute(request);
        for (int tick = 0; tick < 6; tick++)
        {
            npc.AdvanceActors();
        }
        Equal(16, npc.GetActor(0).AnimationFrame16);
        Equal(1, npc.GetActor(0).LastAnimationFrame);
        Equal(true, npc.Execute(request).Completed);

        void AdvanceBoth()
        {
            host.AdvanceActors();
            restored.AdvanceActors();
            Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
    }

    private static Ff7FieldHost Host(bool player, Ff7FieldProcess? process = null)
    {
        PcWalkmeshTriangle first = new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), -1, -1, -1);
        PcWalkmeshTriangle second = new(1, new(0, 0, 10), new(0, 100, 10), new(100, 0, 10), -1, -1, -1);
        Ff7FieldResources field = new("jump-fixture", null!, null!, null!, [], [first, second], null!, null!, null!);
        process ??= new() { Actors = [new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = player, Animation = 2, LastAnimationFrame = 1 }] };
        return new(field, (_, _) => 3, process) { Movement = new([first, second], new short[512], new byte[129]) };
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
