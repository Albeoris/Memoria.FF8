using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldModelPolicyTests
{
    public static void Run()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_MODEL_SAVE");
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (path is null || game is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), source);
        Ff7WorldRuntime initialWorld = session.World ?? throw new InvalidOperationException("The model policy fixture requires its native world continuation.");
        int initialModel = initialWorld.Process.ControlledModel;
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(session.Capture());
        Ff7AutoPlayWorldModelScriptObservation[] offered = initialWorld.ReadAutoPlayModelFrontiers().Where(target => target.Complete && (target.Analysis.MayInteract || target.Analysis.MayEnterField)).ToArray();
        Check(offered.Length > 0, "Read-only native discovery did not offer a complete current-branch model interaction.");
        Check(before.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(session.Capture())), "Native world model discovery changed execution, progress, RNG or presentation state.");
        HashSet<string> offeredIds = offered.Select(target => target.Id).ToHashSet(StringComparer.Ordinal);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded model policy check must not create choice checkpoints."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        Ff7WorldRuntime? observedWorld = null;
        Ff7AutoPlayActiveEvent? accepted = null;
        Ff7WorldModelDispatchObservation? nativeAdmission = null;
        Ff7AutoPlayWorldModelScriptObservation? acceptedSource = null;
        bool changedControl = false;
        bool reachedExpectedModel = false;
        bool completed = false;
        string lastSafePosition = $"field={session.Field.Name}, map={session.MapId}, generation={session.FieldGeneration}, worldTick={initialWorld.Process.Tick}, model={initialModel}";
        controller.Start();
        try
        {
            for (int step = 0; step < 1024 && controller.Running && !completed; step++)
            {
                if (!ReferenceEquals(observedWorld, session.World))
                {
                    if (observedWorld is not null)
                    {
                        observedWorld.NativeModelDispatched -= ObserveDispatch;
                    }
                    observedWorld = session.World;
                    if (observedWorld is not null)
                    {
                        observedWorld.NativeModelDispatched += ObserveDispatch;
                    }
                }
                controller.Advance(1);
                if (controller.Failure is not null)
                {
                    break;
                }
                lastSafePosition = $"field={session.Field.Name}, map={session.MapId}, generation={session.FieldGeneration}, worldTick={session.World?.Process.Tick}, model={session.World?.Process.ControlledModel}";
                if (accepted is null && nativeAdmission is { } observed && policy.State.Active is { Kind: Ff7AutoPlayTargetKind.WorldModel, Accepted: true } admitted && admitted.Entity == observed.Model && admitted.Script == observed.Event && policy.State.WorldDispatch is { } ownership && ownership.Model == observed.Model && ownership.EventId == admitted.EventId && ownership.VisitId == admitted.VisitId && ownership.Entry == observed.Entry && ownership.Tick == observed.Tick)
                {
                    accepted = admitted;
                    acceptedSource = offered.Single(target => target.Id == admitted.EventId);
                }
                if (accepted is not null)
                {
                    WorldScriptAnalysis effects = acceptedSource!.Analysis;
                    bool expectsBoarding = effects.Transitions.Any(transition => transition.Operation == WorldOperation.BoardTransport);
                    HashSet<int> knownNewModels = effects.Transitions.Where(transition => transition.Operation == WorldOperation.SetPlayer && transition.Selector is ushort selector && selector != initialModel).Select(transition => (int)transition.Selector!.Value).ToHashSet();
                    if (expectsBoarding)
                    {
                        knownNewModels.Add(acceptedSource.Model);
                    }
                    bool requiresChangedControl = knownNewModels.Count > 0;
                    bool requiresUnchangedControl = !effects.MayChangeControlledModel || effects.Transitions.Where(transition => transition.Operation is WorldOperation.SetPlayer or WorldOperation.RestoreAttachedRider or WorldOperation.BoardTransport).All(transition => transition.Operation == WorldOperation.SetPlayer && transition.Selector == initialModel);
                    int? currentModel = session.World?.Process.ControlledModel;
                    changedControl |= currentModel is int actualModel && actualModel != initialModel;
                    reachedExpectedModel |= currentModel is int expectedModel && expectedModel != initialModel && knownNewModels.Contains(expectedModel);
                    bool controlMatchesBranch = requiresChangedControl ? reachedExpectedModel : !requiresUnchangedControl || currentModel == initialModel || session.World is null && effects.MayEnterField;
                    var receipt = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == accepted.VisitId).Events.Single(item => item.Event.Id == accepted.EventId);
                    completed = controlMatchesBranch && receipt.Completed && receipt.Conditions?.HasExecutedEntry(accepted.ConditionFingerprint) == true && !(policy.State.Active is { } active && active.VisitId == accepted.VisitId && active.EventId == accepted.EventId && active.Generation == accepted.Generation);
                }
            }
        }
        finally
        {
            if (observedWorld is not null)
            {
                observedWorld.NativeModelDispatched -= ObserveDispatch;
            }
        }
        Ff7AutoPlayActiveEvent? current = policy.State.Active;
        string diagnostics = $"{lastSafePosition}, steps={controller.Steps}, target={current?.Kind}/{current?.EventId}, accepted={accepted?.EventId}, changedControl={changedControl}, status={policy.State.Status}";
        Check(controller.Failure is null, $"Ordinary native model planning failed: {controller.Error}; {diagnostics}");
        Check(accepted is not null && completed, $"Ordinary shared planning did not admit and complete an offered native world model interaction with its current branch's control outcome within its bounded continuation; {diagnostics}");
        Check(policy.State.PendingRestore is null && bytes.SequenceEqual(File.ReadAllBytes(path)), "Native model planning queued a replay or changed its original standalone source.");
        Console.WriteLine($"PASS ordinary native world model interaction admission and exact entry completion after {controller.Steps} steps; native control changed={changedControl}; source unchanged.");

        void ObserveDispatch(Ff7WorldModelDispatchObservation dispatch)
        {
            if (dispatch.Accepted && offeredIds.Contains($"model:{dispatch.Model}:event:{dispatch.Event}"))
            {
                nativeAdmission = dispatch;
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
