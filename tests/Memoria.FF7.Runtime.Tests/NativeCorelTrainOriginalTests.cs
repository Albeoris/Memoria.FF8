using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCorelTrainOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_COREL_TRAIN_SAVE") ?? throw new InvalidOperationException("Select a native lever fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during the native lever sequence."));
        bool firstChord = false;
        bool secondChord = false;
        bool prepared = false;
        session.Host.NativeOperationExecuted += (request, _) => prepared |= request.Command is FieldMovieCommand { Operation: FieldMovieOperation.Prepare, MovieId: 35 };
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput action = policy.ChooseInput(driver);
            firstChord |= action.Held == 0x1010;
            secondChord |= action.Held == 0x4040;
            return action;
        }
        Ff7AutoPlayController controller = new(session, Choose, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (!prepared && controller.Error is null && timer.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
        }
        int phase = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x16);
        Console.WriteLine($"Native Corel levers: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, phase={phase}, firstChord={firstChord}, secondChord={secondChord}, prepared35={prepared}, pc={session.Machine.Capture().Entities[11].ProgramCounter:X4}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !firstChord || !secondChord || phase != 0 || !prepared || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The real lever sequence did not reach its native successful movie boundary.", controller.Failure);
        }
    }
}
