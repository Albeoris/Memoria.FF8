using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeDiscChangeOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_DISC_SAVE") ?? throw new InvalidOperationException("Select a native disc-transition fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        bool pending = false;
        bool acknowledged = false;
        session.NativeTickCompleted += tick =>
        {
            if (session.Host.Process.DiscChange is { Disc: 2, Completed: true } change && session.Host.Process.GlobalRequestId == change.RequestId && session.Machine.Capture().Entities[0].ProgramCounter == 0x0476)
            {
                pending = true;
            }
            acknowledged |= pending && session.Host.Process.GlobalCommand != Ff7GlobalCommand.DiscChange && session.Host.Process.DiscChange is null;
        };
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        bool ready = false;
        for (int polls = 0; polls < 100000 && controller.Steps < 1000 && timer.Elapsed.TotalSeconds < 20 && controller.Error is null && !ready; polls++)
        {
            controller.Advance(1);
            ready = session.MapId == 634 && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled && session.Host.Process.GlobalCommand == Ff7GlobalCommand.None;
        }
        Console.WriteLine($"Native disc continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, pending={pending}, acknowledged={acknowledged}, map={session.MapId}, tick={session.Machine.Tick}, ready={ready}, disc={session.Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0)}, error={controller.Error}");
        bool sourceUnchanged = hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)));
        if (!sourceUnchanged || !pending || !acknowledged || !ready || controller.Error is not null || session.Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0) != 2)
        {
            throw new InvalidOperationException("The native disc request did not preserve its first yield and media acknowledgement before ordinary field arrival, or changed the original fixture.");
        }
    }
}
