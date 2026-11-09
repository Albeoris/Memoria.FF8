using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLadderTests
{
    public static void Run()
    {
        foreach (byte mode in new byte[] { 0, 1, 2, 3 })
        {
            ushort keys = Forward(mode);
            Ff7FieldHost host = Host(true, () => keys);
            FieldHostRequest request = Request(400, mode);
            Equal(false, host.Execute(request).Completed);
            host.AdvanceActors();
            Equal(0, host.GetActor(0).Ladder!.Step);
            Equal(4, host.GetActor(0).Ladder!.Steps);
            host.AdvanceActors();
            Equal(1, host.GetActor(0).Ladder!.Step);
            Equal(4 * 4096, host.GetActor(0).Z4096);
            keys = 0;
            host.AdvanceActors();
            Equal(1, host.GetActor(0).Ladder!.Step);
            Equal(16, host.GetActor(0).AnimationFrame16);
            Ff7FieldHost restored = Host(true, () => keys, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
            keys = Backward(mode);
            AdvanceBoth();
            Equal(0, host.GetActor(0).Ladder!.Step);
            Equal(false, host.GetActor(0).Ladder!.Completed);
            keys = Forward(mode);
            for (int tick = 0; tick < 4; tick++)
            {
                AdvanceBoth();
            }
            Equal(4, host.GetActor(0).Ladder!.Step);
            Equal(false, host.GetActor(0).Ladder!.Completed);
            Equal(0, host.GetActor(0).Triangle);
            Equal(16, host.GetActor(0).AnimationFrame16);
            AdvanceBoth();
            Equal(true, host.GetActor(0).Ladder!.Completed);
            Equal(1, host.GetActor(0).Triangle);
            keys = Backward(mode);
            AdvanceBoth();
            Equal(3, host.GetActor(0).Ladder!.Step);
            Equal(true, host.GetActor(0).Ladder!.Completed);
            Equal(true, host.Execute(request).Completed);
            Equal(true, restored.Execute(request).Completed);
            Equal(true, host.GetActor(0).Ladder is null);

            void AdvanceBoth()
            {
                host.AdvanceActors();
                restored.AdvanceActors();
                Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
            }
        }
        Ff7FieldHost npc = Host(false, null);
        FieldHostRequest automatic = Request(401, 3);
        npc.Execute(automatic);
        for (int tick = 0; tick < 6; tick++)
        {
            npc.AdvanceActors();
        }
        Equal(true, npc.Execute(automatic).Completed);
        Equal(16 * 4096, npc.GetActor(0).Z4096);
        Ff7FieldHost cancel = Host(true, () => 0x1000);
        FieldHostRequest cancellation = Request(402, 0);
        cancel.Execute(cancellation);
        cancel.AdvanceActors();
        cancel.AdvanceActors();
        Equal(true, cancel.Execute(cancellation).Completed);
        Equal(0, cancel.GetActor(0).Triangle);
        Equal(0, cancel.GetActor(0).Z4096);
        Ff7FieldHost both = Host(true, () => 0x5000);
        FieldHostRequest simultaneous = Request(403, 0);
        both.Execute(simultaneous);
        both.AdvanceActors();
        both.AdvanceActors();
        Equal(true, both.GetActor(0).Ladder!.Completed);
        Equal(1, both.GetActor(0).Ladder!.Step);
        Equal(0, both.GetActor(0).Triangle);
        Ff7FieldHost locked = Host(true, null, new() { PlayerLocked = true, Actors = [new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true }] });
        locked.Execute(Request(404, 0));
        locked.AdvanceActors();
        locked.AdvanceActors();
        Equal(1, locked.GetActor(0).Ladder!.Step);
    }

    private static Ff7FieldHost Host(bool player, Func<ushort>? input, Ff7FieldProcess? process = null)
    {
        PcWalkmeshTriangle triangle = new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), -1, -1, -1);
        Ff7FieldResources field = new("ladder-fixture", null!, null!, null!, [], [triangle, triangle with { Index = 1 }], null!, null!, null!);
        process ??= new() { Actors = [new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = player }] };
        return new(field, (_, _) => 3, process) { ReadInputHeld = input, Movement = new([triangle], new short[512], new byte[129]) };
    }

    private static FieldHostRequest Request(long id, byte mode) => new(id, 0, 6, 16, new FieldActorLadder(0, 0, 16, 1, mode, 3, 64, 1));
    private static ushort Forward(byte mode) => mode switch
    {
        0 => 0x4000,
        1 => 0x1000,
        2 => 0x2000,
        _ => 0x8000
    };
    private static ushort Backward(byte mode) => mode switch
    {
        0 => 0x1000,
        1 => 0x4000,
        2 => 0x8000,
        _ => 0x2000
    };

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
