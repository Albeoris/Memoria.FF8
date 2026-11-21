using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGaeaIcicleOriginalTests
{
    internal static void Inspect()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        using FileStream input = File.OpenRead(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_ICICLE_SAVE")!);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice."));
        Console.WriteLine($"Native lower cave profile: {FieldProgram.FromPcScripts(Ff7FieldResources.Load(installation, "gaiin_3").Scripts).Identity}");
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Native icicle source: map={session.MapId}, triangle={player.Triangle}, mask={Convert.ToBase64String(session.Host.Process.BlockedTriangles)}, flags83={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x83)}, profile={session.Machine.ProgramIdentity}, reachable={string.Join(',', Ff7AutoplayDriver.ReachableTriangles(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle))}");
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.Kind == Ff7AutoPlayTargetKind.Gateway || target.ChangesField || target.Entity is >= 3 and <= 6 && target.Script == 6))
        {
            var coverage = policy.State.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == target.Id);
            Console.WriteLine($"Native icicle target {target.Id}, complete={target.AnalysisComplete}, reason={target.SupportReason}, destination={target.DestinationField}, navigation={target.MayChangeNavigation}, story={target.ChangesStory}, unblocks={string.Join(',', target.UnblockedTriangles)}, ladders={target.LadderLandings.Length}, jumps={target.PossibleJumpLandings.Length}, contact={new Ff7AutoplayDriver(session).HasNativeGeometricContact(target)}, completed={coverage?.Completed}, deferred={coverage?.PolicyDeferred}");
        }
    }

    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_ICICLE_SAVE") ?? throw new InvalidOperationException("Select a native icicle prerequisite fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The first native battle must precede its post-battle choice."));
        if (policy.State.DestinationField != 694)
        {
            policy.SetDestinationField(694);
        }
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        string? first = null;
        while (timer.Elapsed.TotalSeconds < 90 && controller.Running && controller.Error is null && session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle)
        {
            controller.Advance(1);
            first ??= policy.State.Active?.EventId;
        }
        Ff7SessionSave captured = session.Capture();
        FieldHostRequest? request = captured.Process.GlobalRequestId is long id ? captured.Vm.PendingRequests?.SingleOrDefault(pending => pending.Id == id) ?? captured.Process.Receipts.SingleOrDefault(receipt => receipt.Request.Id == id)?.Request : null;
        Console.WriteLine($"Native icicle admission: steps={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, first={first}, active={policy.State.Active?.EventId}/{policy.State.Active?.Accepted}, command={session.Host.Process.GlobalCommand}, request={request?.Entity}/{request?.Priority}/{request?.ProgramCounter:X4}/{request?.Command}, error={controller.Failure?.Message ?? controller.Error}");
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) || controller.Error is not null || first != "line:0:entity:3:script:5" || session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle || request is not { Entity: 3, Priority: 1, ProgramCounter: 0x559, Command: FieldBattleCommand { Formation: 716 } })
        {
            throw new InvalidOperationException("The native first icicle battle did not retain its actual source and operands.", controller.Failure);
        }
    }
}
