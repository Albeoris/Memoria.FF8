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
        foreach (Ff7Line line in session.Host.Process.Lines)
        {
            Console.WriteLine($"Native line {line.Entity}: touch={line.Touch}, enter={line.Enter}, leave={line.Leave}, across={line.Across}, push={line.Push}, a=({line.X1},{line.Y1},{line.Z1}), b=({line.X2},{line.Y2},{line.Z2})");
        }
        Console.WriteLine($"Native icicle source: map={session.MapId}, triangle={player.Triangle}, mask={Convert.ToBase64String(session.Host.Process.BlockedTriangles)}, flags83={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x83)}, profile={session.Machine.ProgramIdentity}, reachable={string.Join(',', Ff7AutoplayDriver.ReachableTriangles(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle))}");
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.Kind == Ff7AutoPlayTargetKind.Gateway || target.ChangesField || target.Entity is >= 3 and <= 6 && target.Script is 5 or 6))
        {
            FieldNavigationEntryProof proof = session.ReadNavigationEntryProof(target.Entity, target.Script, session.Machine.Input);
            Console.WriteLine($"Native entry {target.Entity}/{target.Script}: noEffect={proof.Complete}/{proof.HasNoEffect}, reason={proof.Reason}, skip={session.Machine.CanSkipScriptEntryForInput(target.Entity, target.Script, session.Machine.Input)}");
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
        byte flags = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x83);
        int stage = Enumerable.Range(0, 3).First(index => (flags & 1 << (index + 2)) == 0);
        int entity = stage + 3;
        int battlePc = new[] { 0x559, 0x5A1, 0x5DB }[stage];
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
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) || controller.Error is not null || first != $"line:{stage}:entity:{entity}:script:5" || session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle || request?.Entity != entity || request.Priority != 1 || request.ProgramCounter != battlePc || request.Command is not FieldBattleCommand battle || battle.Formation != 716 + stage || session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x83) != flags)
        {
            throw new InvalidOperationException("The native first icicle battle did not retain its actual source and operands.", controller.Failure);
        }
    }
}
