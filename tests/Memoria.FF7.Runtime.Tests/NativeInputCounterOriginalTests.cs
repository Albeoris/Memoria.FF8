using System.Diagnostics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeInputCounterOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_COUNTER_INPUT_SAVE") ?? throw new InvalidOperationException("Select an external native counter-input continuation.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        byte before42 = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42);
        byte before52 = session.Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0x52);
        byte before61 = session.Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0x61);
        byte beforeGrants = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0F);
        if ((before42 & 0x80) != 0 || (before52 & 0x80) != 0 || (before61 & 1) != 0 || beforeGrants == byte.MaxValue)
        {
            throw new InvalidOperationException("Select a native continuation before this permanent reward has been granted.");
        }
        int anchors = 0;
        // This bounded resource check stops before choice settlement and does not persist choice anchors.
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => $"unpersisted-choice-{++anchors}");
        session.NativeInteractionScriptRequested += admission =>
        {
            if (admission.Entity is 7 or 8 && admission.Accepted && admission.Started)
            {
                Console.WriteLine($"Native console admission t{session.Machine.Tick}: {admission}");
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        void Heartbeat(string phase)
        {
            if (controller.Steps % 100 != 0)
            {
                return;
            }
            Ff7Dialogue? dialogue = session.Host.Process.Dialogues.FirstOrDefault(item => !item.Completed);
            Console.WriteLine($"{phase}: callbacks={controller.Steps}, tick={session.Machine.Tick}, dialogue={dialogue?.RequestId}/{dialogue?.Window}/{dialogue?.Text}, phase={dialogue?.Presentation?.NativeState}, token={dialogue?.TokenPosition}, status={policy.Status}.");
        }
        controller.Start();
        bool consoleContinuation = policy.State.SourceGoal is { Entity: 7, Script: 2, AcceptedEntry: not null };
        bool IntroReturned() => (session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x86) & 0x10) != 0 && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled && policy.State.Active is null;
        bool bound = consoleContinuation || IntroReturned() || policy.State.SourceGoal is { Entity: 8, Script: 4, AcceptedEntry: not null };
        for (int attempt = 0; !bound && attempt < 128 && controller.Error is null; attempt++)
        {
            try
            {
                policy.SetSourceGoal("line:1:entity:8:script:4");
                bound = true;
                break;
            }
            catch (InvalidOperationException error) when (error.Message == "The current native event must return before changing its planning destination.")
            {
                controller.Advance(1);
            }
        }
        if (!bound)
        {
            throw new InvalidOperationException("The native console did not become ready for its source goal.", controller.Failure);
        }
        Stopwatch prerequisite = Stopwatch.StartNew();
        while (!consoleContinuation && controller.Error is null && !controller.Paused && !IntroReturned() && controller.Steps < 3000 && prerequisite.Elapsed.TotalSeconds < 90)
        {
            controller.Advance(1);
            Heartbeat("Native introduction");
        }
        if (!consoleContinuation && (controller.Error is not null || !IntroReturned() || policy.State.SourceGoal is not { Completed: true }))
        {
            throw new InvalidOperationException($"The actual protected console introduction did not return: steps={controller.Steps}, status={policy.Status}, error={controller.Error}.", controller.Failure);
        }
        if (!consoleContinuation)
        {
            Console.WriteLine($"Native protected introduction returned: callbacks={controller.Steps}, seconds={prerequisite.Elapsed.TotalSeconds:F3}.");
            controller.Start();
            policy.SetSourceGoal("line:0:entity:7:script:2");
        }
        List<ushort> presses = [];
        bool timerStarted = false;
        session.Host.NativeOperationExecuted += (request, reply) => timerStarted |= request.Command is FieldTimerCommand;
        session.Machine.InstructionExecuted += (entity, priority, pc, opcode) =>
        {
            if (entity == 4 && opcode == 0x95)
            {
                presses.Add(session.Machine.Input.RawPressed);
            }
        };
        Stopwatch timer = Stopwatch.StartNew();
        bool Awarded() => session.Machine.Variables.ReadByte(FieldBank.MapByte, 9) == 4 && session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42) == (before42 | 0x80) && session.Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0x52) == (before52 | 0x80) && session.Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0x61) == (before61 | 1) && session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0F) == beforeGrants + 1;
        int boundSteps = int.TryParse(Environment.GetEnvironmentVariable("MEMORIA_FF7_COUNTER_INPUT_STEPS"), out int maximum) ? maximum : 5000;
        while (controller.Error is null && !controller.Paused && !Awarded() && controller.Steps < boundSteps && timer.Elapsed.TotalSeconds < 90)
        {
            controller.Advance(1);
            Heartbeat("Native console");
            if (policy.State.PendingRestore is not null)
            {
                throw new InvalidOperationException("The native counter continuation requested a choice replay.");
            }
        }
        byte counter = session.Machine.Variables.ReadByte(FieldBank.MapByte, 9);
        byte consoleFlags = session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x86);
        bool materiaAward = Awarded();
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Console.WriteLine($"Original counter input: steps={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, counter={counter}, presses={string.Join(',', presses.Select(value => value.ToString("X4")))}, consoleFlags={consoleFlags:X2}, rewardDelta={materiaAward}, source={policy.State.SourceGoal?.Completed}/{policy.State.SourceGoal?.AcceptedEntry}, active={policy.State.Active?.Accepted}/{policy.State.Active?.LinePhase}, pose={player?.X4096},{player?.Y4096},{player?.Triangle}, pending={session.HasPendingTick}, locks={session.Host.Process.PlayerLocked}/{session.Host.Process.MenuDisabled}, status={policy.Status}, error={controller.Error}.");
        if (controller.Error is not null || !materiaAward || timerStarted || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("The native finite counter sequence did not reach its actual permanent reward boundary.", controller.Failure);
        }
        Console.WriteLine("Diagnostic native counter and permanent reward delta verified; this fixture does not establish owned control return or provide restorable choice anchors.");
    }
}
