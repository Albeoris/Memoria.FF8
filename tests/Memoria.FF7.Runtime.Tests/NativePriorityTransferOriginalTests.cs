using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePriorityTransferOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PRIORITY_TRANSFER_SAVE") ?? throw new InvalidOperationException("Select a native priority-transfer fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        if (!session.Field.Scripts.Data.Span.Slice(0x649, 2).SequenceEqual(new byte[] { 7, 0xE1 }))
        {
            throw new InvalidOperationException("The native priority-transfer packet differs from the selected fixture.");
        }
        int entry = session.Field.Scripts.Entities[13].ScriptOffsets[1];
        bool transferred = false;
        bool continued = false;
        session.Machine.InstructionExecuted = (entity, priority, pc, opcode) =>
        {
            if (entity == 13 && priority == 0 && pc == 0x649 && opcode == 7)
            {
                transferred = true;
            }
            else if (transferred && entity == 13)
            {
                continued |= priority == 7 && pc == entry;
            }
        };
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice."));
        policy.SetDestinationField(705);
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Steps < 1600 && timer.Elapsed.TotalSeconds < 15 && controller.Running && controller.Error is null && !continued)
        {
            controller.Advance(1);
        }
        long callbacks = controller.Steps;
        var worker = session.Machine.Capture().Entities[13];
        Console.WriteLine($"Worker PC={worker.ProgramCounter:X4}/P{worker.Priority}, variables={Convert.ToHexString(session.Machine.Variables.CopyMap().AsSpan(0, 20))}");
        Console.WriteLine($"Native priority transfer: callbacks={callbacks}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, source07={transferred}, targetP7={continued}, entry={entry:X4}, status={policy.Status}, error={controller.Error}");
        if (controller.Error is not null || transferred && !continued || session.MapId != 710 || !SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The bounded original continuation failed or did not honor its reached priority transfer.");
        }
    }
}
