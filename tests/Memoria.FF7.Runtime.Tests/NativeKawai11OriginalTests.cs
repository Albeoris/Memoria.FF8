using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeKawai11OriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_KAWAI_SAVE") ?? throw new InvalidOperationException("Select a native field-effect fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The reached model callback must precede another choice."));
        byte[] initialize = session.Field.Scripts.Data.Span.Slice(0x6B4, 22).ToArray();
        byte[] activate = session.Field.Scripts.Data.Span.Slice(0x6CD, 4).ToArray();
        if (initialize[0] != 0x28 || initialize[1] != 22 || initialize[2] != 11 || !activate.AsSpan().SequenceEqual(new byte[] { 0x28, 4, 11, 1 }))
        {
            throw new InvalidOperationException("The original effect source differs from its reached instruction packets.");
        }
        List<(FieldHostRequest Request, bool Pending)> admissions = [];
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request is { Entity: 6, Priority: 6, Command: FieldModelEffectCommand { Operation: 11, Wait: false } } && reply.Completed)
            {
                admissions.Add((request, session.Host.Process.Actors.Single(actor => actor.Entity == 6).NativeModelCompletion is { Operation: 11, Pending: true }));
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        bool fulfilled = false;
        while (timer.Elapsed.TotalSeconds < 15 && controller.Running && controller.Error is null && !fulfilled)
        {
            controller.Advance(1);
            fulfilled = admissions.Count == 2 && session.Host.Process.Actors.Single(actor => actor.Entity == 6).NativeModelCompletion is { Operation: 11, Payload: [1], Pending: false };
        }
        Console.WriteLine($"Native KAWAI11 entry: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, packets={string.Join(',', admissions.Select(item => item.Request.ProgramCounter.ToString("X4")))}, pendingOnAdmission={admissions.All(item => item.Pending)}, eligibleCompletion={fulfilled}, error={controller.Failure?.Message ?? controller.Error}");
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) || controller.Error is not null || session.MapId != 701 || admissions.Count != 2 || !admissions.All(item => item.Pending) || admissions[0].Request.ProgramCounter != 0x6B4 || admissions[1].Request.ProgramCounter != 0x6CD || !((FieldModelEffectCommand)admissions[0].Request.Command).Payload!.AsSpan().SequenceEqual(initialize.AsSpan(3)) || !((FieldModelEffectCommand)admissions[1].Request.Command).Payload!.AsSpan().SequenceEqual(activate.AsSpan(3)) || !fulfilled)
        {
            throw new InvalidOperationException("The original entry did not retain its native pending and eligible-completion callbacks.", controller.Failure);
        }
    }
}
