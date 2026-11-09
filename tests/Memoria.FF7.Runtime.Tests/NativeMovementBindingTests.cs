using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMovementBindingTests
{
    public static void Run()
    {
        Ff7FieldHost host = Build(1);
        var walk = new FieldHostRequest(1, 0, 2, 16, new FieldActorMove(0, 0));
        Equal(true, host.Execute(walk).Completed);
        Equal(0, host.GetActor(0).Animation);
        Equal<int?>(1, host.GetActor(0).RequestedAnimation);
        Equal(3, host.GetActor(0).LastAnimationFrame);
        Equal(0, host.GetActor(0).AnimationFrame16);
        Ff7FieldHost cold = Build(1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        for (int tick = 0; tick < 4; tick++)
        {
            host.AdvanceActors();
            cold.AdvanceActors();
            Equal(16, host.GetActor(0).AnimationFrame16);
            Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(cold.Capture()));
        }
        host = Build(1, host.Capture() with { Actors = [host.GetActor(0) with { MoveSpeed = 1537 }] });
        Equal(true, host.Execute(walk with { Id = 2 }).Completed);
        Equal(0, host.GetActor(0).Animation);
        Equal<int?>(2, host.GetActor(0).RequestedAnimation);
        host.Execute(new(3, 0, 2, 16, new FieldControlCommand(FieldControlKind.PlayerLocked, 1)));
        host.AdvanceActors();
        Equal<int?>(null, host.GetActor(0).RequestedAnimation);
        Equal(0, host.GetActor(0).Animation);

        host = Build(3);
        host.Execute(walk);
        Equal(1, host.GetActor(0).Animation);
        Equal<int?>(null, host.GetActor(0).RequestedAnimation);
        host.AdvanceActors();
        host.AdvanceActors();
        Equal(32, host.GetActor(0).AnimationFrame16);
        host = Build(3, host.Capture() with { Actors = [host.GetActor(0) with { MoveSpeed = 1537 }] });
        host.Execute(walk with { Id = 2 });
        Equal(2, host.GetActor(0).Animation);
        Equal<int?>(null, host.GetActor(0).RequestedAnimation);
    }

    private static Ff7FieldHost Build(int animationCount, Ff7FieldProcess? process = null)
    {
        byte[] script = new byte[34];
        BinaryPrimitives.WriteUInt16LittleEndian(script, 0x0502);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(4), 32);
        BinaryPrimitives.WriteInt16LittleEndian(script.AsSpan(8), 512);
        var descriptor = new PcFieldModelDescriptor("fixture", 0, "fixture", "512", [], 0, Enumerable.Range(0, animationCount).Select(index => $"clip{index}").ToArray());
        var field = new Ff7FieldResources("binding-fixture", null!, PcFieldScripts.Read(script), new(0, 512, [descriptor]), [], [], null!, null!, null!);
        process ??= new() { BlinkStateInitialized = true, Actors = [new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, MoveSpeed = 1536, AnimationFrame16 = 32 }] };
        return new(field, (_, animation) => animation < animationCount ? 4 : throw new Exception("A rejected model binding must not resolve an absent clip."), process);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
