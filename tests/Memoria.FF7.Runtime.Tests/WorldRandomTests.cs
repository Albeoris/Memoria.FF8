using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldRandomTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed FFVII world random stream: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        byte[] variables = new byte[1280];
        variables[0] = 152;
        byte[] party = [0, 1, 2];
        using Ff7WorldRuntime world = new(installation, variables, 1, party);
        world.Advance();
        Ff7WorldProcess initial = world.Process;
        Check(initial.BattleState is { RandomIndex: 0x208 }, "Entering the original world must seed its RNG even with encounters disabled.");
        int quakePc = 0x225E;
        Check(world.Machine.OwnsHostOperation(quakePc, WorldOperation.Earthquake), "The fixture must use an original earthquake opcode.");
        Check(world.Resources.ReadEvents().Code.Skip(0x3091).Take(2).SequenceEqual(new ushort[] { 0x11B, 16 }), "The original WM0 model event must retain its random selector operand.");

        byte[] random = new byte[0x209];
        random[1] = 192;
        random[2] = 0;
        random[3] = 77;
        random[4] = 128;
        random[5] = 64;
        random[6] = 221;
        world.Restore(initial with
        {
            MainState = 1,
            MovementEnabled = true,
            ControlLock = 0,
            BattleState = initial.BattleState! with { Random = random, RandomIndex = 0 }
        });
        world.Execute(new(initial.Vm.NextRequestId, -1, -1, quakePc, new(WorldOperation.Earthquake, [1])));
        world.SetMovement(1, 0, true);
        world.Advance();
        Check(world.Process.ScreenShake is { Enabled: true, X: 2, Y: -4 } && world.Process.BattleState?.RandomIndex == 3, "Earthquake must consume X then Y before the native horizontal input sample.");
        Check(world.ReadSpecial(16, -1) == 128, "Script selector 16 must read the next byte from the same world stream.");

        using Ff7WorldRuntime cold = new(installation, world.Machine.CopyPersistent(), 1, party);
        cold.Restore(JsonSerializer.Deserialize<Ff7WorldProcess>(JsonSerializer.Serialize(world.Process))!);
        world.SetMovement(0, 1, true);
        cold.SetMovement(0, 1, true);
        world.Advance();
        cold.Advance();
        Check(world.Process.ScreenShake is { X: -2, Y: 2 } && world.Process.BattleState?.RandomIndex == 6, "Vertical input alone must leave the stream after the two earthquake draws.");
        Check(JsonSerializer.Serialize(world.Process) == JsonSerializer.Serialize(cold.Process), "Cold restore must retain the exact next earthquake and random state.");
        world.Execute(new(initial.Vm.NextRequestId, -1, -1, quakePc, new(WorldOperation.Earthquake, [0])));
        world.Advance();
        Check(world.Process.ScreenShake is null && world.Process.BattleState?.RandomIndex == 6, "Disabling earthquake must publish zero projection offsets without drawing.");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new Exception(message);
        }
    }
}
