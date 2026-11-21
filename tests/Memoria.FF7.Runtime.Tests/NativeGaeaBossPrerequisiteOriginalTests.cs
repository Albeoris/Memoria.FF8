using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGaeaBossPrerequisiteOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_BOSS_SAVE") ?? throw new InvalidOperationException("Select a native boss-line fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native boss-line admission has no choice checkpoint."));
        byte flags = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x86);
        if (policy.State.DestinationField != 695)
        {
            policy.SetDestinationField(695);
        }
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        string? first = null;
        while (timer.Elapsed.TotalSeconds < 30 && controller.Running && controller.Error is null && session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle)
        {
            controller.Advance(1);
            first ??= policy.State.Active?.EventId;
        }
        Ff7SessionSave current = session.Capture();
        FieldHostRequest? request = current.Process.GlobalRequestId is long id ? current.Vm.PendingRequests?.SingleOrDefault(pending => pending.Id == id) ?? current.Process.Receipts.SingleOrDefault(receipt => receipt.Request.Id == id)?.Request : null;
        Console.WriteLine($"Native boss admission: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, initial={first}, active={policy.State.Active?.EventId}/{policy.State.Active?.Accepted}/{policy.State.Active?.Returned}, flags86={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x86)}, request={request?.Entity}/{request?.Priority}/{request?.ProgramCounter:X4}/{request?.Command}, error={controller.Failure?.Message ?? controller.Error}");
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) || controller.Error is not null || policy.State.Active is not { Entity: 2, Script: 2, Accepted: true, Returned: false, CompletionRecorded: false, ExecutionProvenance.RootReturned: false } || session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x86) != (flags | 4) || session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle || request is not { Entity: 2, Priority: 1, ProgramCounter: 0x478, Command: FieldBattleCommand { Formation: 740 } })
        {
            throw new InvalidOperationException("The native boss source did not admit its actual battle while retaining unreturned ownership.", controller.Failure);
        }
    }
}
