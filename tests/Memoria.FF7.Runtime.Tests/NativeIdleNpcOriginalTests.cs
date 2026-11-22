using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeIdleNpcOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_IDLE_NPC_SAVE") ?? throw new InvalidOperationException("Select a native returned-root idle-worker fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native returned-root continuation has no choice checkpoint."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        bool retired = false;
        bool workerRetained = false;
        bool destination = false;
        while (controller.Error is null && session.FieldGeneration == source.FieldGeneration && timer.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
            if (!destination && policy.State.Active is null)
            {
                retired = true;
                var vm = session.Machine.Capture();
                workerRetained = vm.Entities[10].Priority == 3 && vm.Entities[10].ProgramCounter == 0x1697 && (vm.PendingRequests ?? []).Any(request => request.Entity == 10 && request.Priority == 7 && request.Id == 82);
                policy.SetDestinationField(712);
                destination = true;
            }
        }
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Console.WriteLine($"Native idle worker: retired={retired}, workerRetained={workerRetained}, destination={destination}, callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, pose={player?.X4096},{player?.Y4096},{player?.Triangle}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !retired || !workerRetained || !destination || session.MapId is not (712 or 715) || session.FieldGeneration <= source.FieldGeneration || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The actual returned root and independent idle worker did not permit the native destination exit.", controller.Failure);
        }
    }
}
