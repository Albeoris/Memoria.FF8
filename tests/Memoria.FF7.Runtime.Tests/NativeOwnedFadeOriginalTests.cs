using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeOwnedFadeOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_FADE_SAVE") ?? throw new InvalidOperationException("Select an external owned fade-wait fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        using Ff7Session session = Ff7Session.Restore(installation, source);
        FieldHostRequest wait = session.Machine.Capture().PendingRequests!.Single(request => request.Command is FieldFadeCommand { Wait: true });
        Ff7FadeProcess initialFade = session.Host.Process.FadeState ?? throw new InvalidOperationException("The fixture has no native fade.");
        if (initialFade.Type != 1 || initialFade.Completed || initialFade.Speed <= 0)
        {
            throw new InvalidOperationException("The fixture must own an unfinished native fade-in.");
        }
        int bound = (initialFade.Position + initialFade.Speed - 1) / initialFade.Speed + 1;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Owned fade continuation must not explore a new choice."));
        FadeClock clock = new();
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, clock: clock, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int poll = 0; poll < bound && controller.Error is null && session.Machine.Capture().PendingRequests!.Any(request => request.Id == wait.Id); poll++)
        {
            clock.Seconds += 3;
            controller.Advance(1);
        }
        bool pending = session.Machine.Capture().PendingRequests!.Any(request => request.Id == wait.Id);
        Console.WriteLine($"Owned fade result: entity={wait.Entity}, priority={wait.Priority}, pc={wait.ProgramCounter:X4}, callbacks={controller.Steps}/{bound}, tick={session.Machine.Tick}, fade={session.Host.Process.FadeState}, waiting={pending}, error={controller.Error}.");
        if (controller.Error is not null || pending || session.Host.Process.FadeState is not { Completed: true } || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The exact owned native fade wait did not complete through normal autoplay callbacks.", controller.Failure);
        }
        using Ff7Session stationary = Ff7Session.Restore(installation, source);
        FadeClock stoppedClock = new();
        Ff7AutoPlayController stopped = new(stationary, _ => default, clock: stoppedClock, advanceTick: () => { });
        stopped.Start();
        for (int poll = 0; poll < bound && stopped.Error is null; poll++)
        {
            stoppedClock.Seconds += 3;
            stopped.Advance(1);
        }
        if (stopped.Failure is not TimeoutException || stationary.Host.Process.FadeState != initialFade)
        {
            throw new InvalidOperationException("An unchanged owned finite fade bypassed the idle deadline.");
        }
    }

    private sealed class FadeClock : TimeProvider
    {
        public long Seconds { get; set; }
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Seconds;
    }
}
