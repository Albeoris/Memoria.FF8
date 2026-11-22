using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRearmedInteractionOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_REARMED_INTERACTION_SAVE") ?? throw new InvalidOperationException("Select a rearmed native interaction fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The regression stops at the first real choice before checkpoint selection."));
        bool selected = false;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput action = policy.ChooseInput(driver);
            if (session.MapId == 356 && policy.State.Active is { Accepted: false } active && active.Generation == session.FieldGeneration)
            {
                selected |= active is { Entity: 22, Script: 1, Kind: Ff7AutoPlayTargetKind.ActorTalk };
                if (!selected)
                {
                    throw new InvalidOperationException($"A known source's new native branch lost priority to {active.EventId}.");
                }
            }
            return action;
        }
        Ff7AutoPlayController controller = new(session, Choose, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        FieldHostRequest? Prompt() => session.Machine.Capture().PendingRequests?.FirstOrDefault(request => request.Command is FieldDialogueCommand { FirstChoice: not null, LastChoice: not null });
        while (controller.Error is null && Prompt() is null && timer.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
        }
        FieldHostRequest? prompt = Prompt();
        Console.WriteLine($"Native rearmed interaction: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, selected={selected}, accepted={policy.State.Active?.Accepted}, prompt={prompt}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        bool owned = prompt is not null && policy.State.Active?.ExecutionProvenance?.Owners.Any(owner => owner.Entity == prompt.Entity && owner.Priority == prompt.Priority) == true;
        if (controller.Error is not null || !selected || !owned || policy.State.Active is not { Entity: 22, Accepted: true } || (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xB1) & 8) == 0 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The genuinely rearmed native interaction did not reach its own first choice prompt.", controller.Failure);
        }
    }
}
