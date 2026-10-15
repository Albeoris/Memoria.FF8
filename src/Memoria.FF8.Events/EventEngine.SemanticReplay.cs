using Memoria.FF8.Runtime;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool semanticInputInferred;
    public event Action<ReplaySemanticTrigger>? SemanticTriggerAdmitted;
    public bool CanAdmitSemanticInput => CanAdmitSemanticTrigger();

    /// <summary>Reissues a historical external event cause through the current native admission contract. It does not reproduce a physical approach.</summary>
    public PlayerActionResult ApplySemanticTrigger(ReplaySemanticTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        trigger.Validate();
        ValidateSemanticProducer(trigger);
        return RunReplay(ReplayBoundary(ReplayEntryKind.SemanticTrigger) with { SemanticTrigger = trigger }, () =>
        {
            if (!CanAdmitSemanticTrigger())
            {
                return new PlayerActionResult(false, NextTrigger, 0, "The current native field does not admit an external event stimulus.");
            }
            bool previous = semanticInputInferred;
            semanticInputInferred = trigger.Inferred;
            try
            {
                bool accepted;
                ActorState? actor = trigger.ObjectId < 0 ? null : actors[trigger.ObjectId];
                switch (trigger.Kind)
                {
                    case ReplaySemanticTriggerKind.ActorInteraction:
                        accepted = actor is { Active: true, TalkEnabled: true } && Scene.NativeActorObjectIds.Contains(actor.ObjectId) && TalkTo(actor.ObjectId);
                        if (accepted)
                        {
                            QueueConfirmEdge();
                        }
                        break;
                    case ReplaySemanticTriggerKind.PushContact:
                        accepted = actor is { Active: true, PushEnabled: true } && Scene.NativeActorObjectIds.Contains(actor.ObjectId) && AdmitNativePushContact(actor);
                        break;
                    case ReplaySemanticTriggerKind.DoorEntry:
                    case ReplaySemanticTriggerKind.DoorAction:
                    case ReplaySemanticTriggerKind.DoorCrossing:
                    case ReplaySemanticTriggerKind.DoorExit:
                    case ReplaySemanticTriggerKind.DoorFacing:
                    case ReplaySemanticTriggerKind.DoorContact:
                        accepted = actor!.LineEnabled && Scene.NativeDoorDriverVersion == 2 && AdmitNativeDoorEvents(actor, trigger.EventBits, trigger.Contact, trigger.LocalScript);
                        if (accepted && trigger.Kind == ReplaySemanticTriggerKind.DoorAction)
                        {
                            QueueConfirmEdge();
                        }
                        break;
                    case ReplaySemanticTriggerKind.AreaEntry:
                    case ReplaySemanticTriggerKind.AreaExit:
                        accepted = actor!.DoorLineEnabled;
                        if (accepted)
                        {
                            CommitInfAreaContact(actor, trigger.Contact, trigger.Contact, trigger.InfIndex);
                        }
                        break;
                    case ReplaySemanticTriggerKind.FieldGateway:
                        accepted = !Scene.MapJumpDisabled;
                        if (accepted)
                        {
                            PrepareInfGatewayAdmission(ResolveSemanticGateway(trigger.InfIndex))();
                        }
                        break;
                    default:
                        throw new NotSupportedException("This semantic trigger belongs to a different native scene owner.");
                }
                return new PlayerActionResult(accepted, NextTrigger, 0);
            }
            finally
            {
                semanticInputInferred = previous;
            }
        });
    }

    /// <summary>Current producer metadata for explicit legacy inference. These candidates are not evidence that a past admission occurred.</summary>
    public IReadOnlyList<ReplaySemanticTrigger> GetSemanticTriggerCandidates()
    {
        List<ReplaySemanticTrigger> candidates = [];
        if (program is null || NativeRuntimeProfile is not { IsVerified: true })
        {
            return candidates;
        }
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            EventObject obj = program.Objects[objectId];
            if (obj.Scripts.Count > 2)
            {
                candidates.Add(CreateSemanticTrigger(ReplaySemanticTriggerKind.ActorInteraction, objectId, 2, 7));
            }
            if (obj.Scripts.Count > 3)
            {
                candidates.Add(CreateSemanticTrigger(ReplaySemanticTriggerKind.PushContact, objectId, 3, 6));
            }
        }
        foreach (EventObject obj in program.Objects.Where(obj => obj.IsDoor))
        {
            for (int local = 2; local < Math.Min(8, obj.Scripts.Count); local++)
            {
                if (EmptyNativeLineScript(actors[obj.Index], local))
                {
                    continue;
                }
                byte bits = local switch { 2 => NativeDoorAction, 3 => NativeDoorFacing, 4 => NativeDoorCrossing, 6 => NativeDoorExit, 7 => NativeDoorEntry, _ => 0 };
                candidates.Add(CreateSemanticTrigger(SemanticDoorKind(local), obj.Index, local, 9 - local, eventBits: bits, contact: local == 5));
            }
        }
        int[] areas = program.NativeObjects.Where(id => program.Objects[id].IsArea).ToArray();
        for (int index = 0; index < InfAreaTriggers.Count; index++)
        {
            InfAreaTrigger inf = InfAreaTriggers[index];
            if (inf.NativeIndex < 0 || (uint)inf.AreaIndex >= areas.Length || program.Objects[areas[inf.AreaIndex]].Scripts.Count < 3)
            {
                continue;
            }
            candidates.Add(CreateSemanticTrigger(ReplaySemanticTriggerKind.AreaEntry, areas[inf.AreaIndex], 2, 6, inf.NativeIndex, contact: true));
            candidates.Add(CreateSemanticTrigger(ReplaySemanticTriggerKind.AreaExit, areas[inf.AreaIndex], 2, 6, inf.NativeIndex));
        }
        foreach (InfGateway gateway in InfGateways)
        {
            if (gateway.NativeIndex >= 0 && gateway.Destination.FieldId != 0x7FFF && (gateway.Destination.FieldId < 72 || gateway.Destination.Triangle != 0xFFFF))
            {
                candidates.Add(CreateSemanticGateway(gateway));
            }
        }
        return candidates.Select(candidate => candidate with { Inferred = true }).ToArray();
    }

    /// <summary>Pure potential exit producers from current literal bytecode/INF metadata. The current VM must still take the branch and produce the requested exit.</summary>
    public IReadOnlyList<ReplaySemanticTrigger> GetSemanticExitCandidates(SceneKind destinationScene, int destinationFieldId)
    {
        return GetSemanticTriggerCandidates().Where(IsSemanticTargetEnabled).Where(candidate =>
        {
            if (candidate.Kind == ReplaySemanticTriggerKind.FieldGateway)
            {
                FieldTransition destination = ResolveSemanticGateway(candidate.InfIndex).Destination;
                return destinationScene == SceneKind.Field && destination.FieldId == destinationFieldId || destinationScene == SceneKind.WorldMap && destination.FieldId < 72;
            }
            EventScript script = program!.Objects[candidate.ObjectId].Scripts[candidate.LocalScript];
            for (int index = script.Start; index < script.End; index++)
            {
                JsmOperation operation = program.Operations[index];
                if (destinationScene == SceneKind.WorldMap && operation.Opcode == Jsm.Opcode.WORLDMAPJUMP)
                {
                    return true;
                }
                int operands = operation.Opcode switch
                {
                    Jsm.Opcode.MAPJUMP or Jsm.Opcode.PREMAPJUMP => 4,
                    Jsm.Opcode.MAPJUMP3 or Jsm.Opcode.DISCJUMP => 5,
                    Jsm.Opcode.MAPJUMPO => 2,
                    Jsm.Opcode.PREMAPJUMP2 => 1,
                    _ => 0
                };
                if (destinationScene != SceneKind.Field || operands == 0 || index - operands < script.Start)
                {
                    continue;
                }
                // Only an adjacent literal operand group is recognized. Dynamic
                // expressions, cross-script requests and branches are not solved.
                if (Enumerable.Range(index - operands, operands).All(position => program.Operations[position].Opcode == Jsm.Opcode.PSHN_L) && unchecked((ushort)program.Operations[index - operands].Parameter) == destinationFieldId)
                {
                    return true;
                }
            }
            return false;
        }).ToArray();
    }

    private bool IsSemanticTargetEnabled(ReplaySemanticTrigger trigger)
    {
        if (trigger.Kind == ReplaySemanticTriggerKind.FieldGateway)
        {
            return !Scene.MapJumpDisabled;
        }
        ActorState actor = actors[trigger.ObjectId];
        return trigger.Kind switch
        {
            ReplaySemanticTriggerKind.ActorInteraction => actor.Active && actor.TalkEnabled,
            ReplaySemanticTriggerKind.PushContact => actor.Active && actor.PushEnabled,
            ReplaySemanticTriggerKind.AreaEntry or ReplaySemanticTriggerKind.AreaExit => actor.DoorLineEnabled,
            ReplaySemanticTriggerKind.DoorEntry or ReplaySemanticTriggerKind.DoorAction or ReplaySemanticTriggerKind.DoorCrossing or ReplaySemanticTriggerKind.DoorExit or ReplaySemanticTriggerKind.DoorFacing or ReplaySemanticTriggerKind.DoorContact => actor.LineEnabled && Scene.NativeDoorDriverVersion == 2,
            _ => false
        };
    }

    /// <summary>Proves identical bounded door-exit bytecode or equal current INF destination packets. It never equates scripts to physical gateways.</summary>
    public bool AreSemanticExitCandidatesEquivalent(IReadOnlyList<ReplaySemanticTrigger> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count < 2 || program is null || NativeRuntimeProfile is not { IsVerified: true })
        {
            return false;
        }
        (Jsm.Opcode Opcode, int Parameter)[]? expected = null;
        FieldTransition? gatewayDestination = null;
        ReplaySemanticTrigger first = candidates[0];
        foreach (ReplaySemanticTrigger candidate in candidates)
        {
            if (candidate is null || candidate.Kind == ReplaySemanticTriggerKind.WorldFieldEntry)
            {
                return false;
            }
            try
            {
                candidate.Validate();
                ValidateSemanticProducer(candidate);
            }
            catch (InvalidDataException)
            {
                return false;
            }
            if (first.Kind == ReplaySemanticTriggerKind.FieldGateway)
            {
                if (candidate.Kind != first.Kind || !IsSemanticTargetEnabled(candidate))
                {
                    return false;
                }
                FieldTransition destination = ResolveSemanticGateway(candidate.InfIndex).Destination;
                if (gatewayDestination is not null && destination != gatewayDestination)
                {
                    return false;
                }
                gatewayDestination ??= destination;
                continue;
            }
            if (candidate.Kind == ReplaySemanticTriggerKind.FieldGateway)
            {
                return false;
            }
            if (!IsSemanticTargetEnabled(candidate) || candidate.Kind != first.Kind || candidate.LocalScript != first.LocalScript || candidate.NativeSlot != first.NativeSlot || candidate.EventBits != first.EventBits || candidate.Contact != first.Contact || !program.Objects[candidate.ObjectId].IsDoor)
            {
                return false;
            }
            EventScript script = program.Objects[candidate.ObjectId].Scripts[candidate.LocalScript];
            if (program.Operations[script.Start].Opcode != Jsm.Opcode.LBL || program.Operations[script.Start].Parameter != script.Id)
            {
                return false;
            }
            (Jsm.Opcode Opcode, int Parameter)[] body = Enumerable.Range(script.Start + 1, script.End - script.Start - 1).Select(index => (program.Operations[index].Opcode, program.Operations[index].Parameter)).ToArray();
            if (expected is not null && !expected.SequenceEqual(body) || !HasPureCurrentExitPrefix(candidate.ObjectId, script))
            {
                return false;
            }
            expected ??= body;
        }
        return true;
    }

    private bool HasPureCurrentExitPrefix(int objectId, EventScript script)
    {
        return TryReadPureCurrentExitPrefix(objectId, script, out _, out _);
    }

    private bool TryReadPureCurrentExitPrefix(int objectId, EventScript script, out JsmOperation? terminal, out int[] arguments)
    {
        terminal = null;
        arguments = [];
        // Only these existing Execute cases return after operating on the detached
        // operand stack. They do not emit CommandExecuted or publish event owners.
        Fiber probe = new(objectId, script, 0, 0, false) { Position = script.Start + 1 };
        try
        {
            while (probe.Position > script.Start && probe.Position < script.End)
            {
                int index = probe.Position;
                JsmOperation operation = program!.Operations[index];
                if (operation.Opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.CAL)
                {
                    if (UnsupportedNativeScalarAccess(probe, operation) is not null)
                    {
                        return false;
                    }
                    probe.Position++;
                    Execute(probe, operation);
                    if (probe.Stack.HasUncertainty)
                    {
                        return false;
                    }
                    continue;
                }
                if (operation.Opcode is Jsm.Opcode.JPF or Jsm.Opcode.JMP)
                {
                    long target = (long)index + operation.Parameter;
                    if (operation.Parameter <= 0 || target <= script.Start || target >= script.End)
                    {
                        return false;
                    }
                    bool jump = operation.Opcode == Jsm.Opcode.JMP || Pop(probe, 1)[0] == 0;
                    probe.Position = jump ? (int)target : index + 1;
                    continue;
                }
                if (operation.Opcode is Jsm.Opcode.NOP or Jsm.Opcode.UCOFF or Jsm.Opcode.LINEOFF)
                {
                    probe.Position++;
                    continue;
                }
                int operands = operation.Opcode switch
                {
                    Jsm.Opcode.MAPJUMP => 4,
                    Jsm.Opcode.MAPJUMP3 or Jsm.Opcode.DISCJUMP => 5,
                    Jsm.Opcode.MAPJUMPO => 2,
                    Jsm.Opcode.WORLDMAPJUMP when !Scene.MapJumpDisabled => 3,
                    _ => 0
                };
                // Reached writes, requests, UI, RNG, actor-local inputs and RET
                // before departure are unsupported proof paths, not executed.
                if (operands == 0 || probe.Stack.Count < operands)
                {
                    return false;
                }
                terminal = operation;
                arguments = probe.Stack.Take(operands).Reverse().ToArray();
                return true;
            }
        }
        catch (Exception exception) when (exception is EventScriptException or InvalidOperationException or InvalidDataException or ArgumentException or ArithmeticException or NotSupportedException)
        {
            return false;
        }
        return false;
    }

    /// <summary>Proves current pure field exits for inferred inputs under explicit completed-visit spatial abstraction.</summary>
    public bool CanCanonicalizeLegacySpatialExits(IReadOnlyList<ReplaySemanticTrigger> candidates, SceneKind destinationScene, int destinationFieldId)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (!IsSemanticSpatialReplay || candidates.Count < 2 || program is null || NativeRuntimeProfile is not { IsVerified: true } || Scene.MapJumpDisabled || destinationScene != SceneKind.Field)
        {
            return false;
        }
        bool? resetLeaderDefaults = null;
        foreach (ReplaySemanticTrigger candidate in candidates)
        {
            if (candidate is not { Inferred: true })
            {
                return false;
            }
            try
            {
                candidate.Validate();
                ValidateSemanticProducer(candidate);
                if (!IsSemanticTargetEnabled(candidate))
                {
                    return false;
                }
                FieldTransition? destination;
                if (candidate.Kind == ReplaySemanticTriggerKind.FieldGateway)
                {
                    destination = ResolveSemanticGateway(candidate.InfIndex).Destination;
                    // An unclassified extra WORD is not proved to be only spatial.
                    if (destination.NativeExtraWord is not null)
                    {
                        return false;
                    }
                }
                else
                {
                    EventScript script = program.Objects[candidate.ObjectId].Scripts[candidate.LocalScript];
                    if (!TryReadPureCurrentExitPrefix(candidate.ObjectId, script, out JsmOperation? terminal, out int[] arguments))
                    {
                        return false;
                    }
                    destination = terminal!.Opcode switch
                    {
                        Jsm.Opcode.MAPJUMP => new FieldTransition(unchecked((ushort)arguments[0]), unchecked((short)arguments[1]), unchecked((short)arguments[2]), unchecked((ushort)terminal.Parameter), unchecked((byte)arguments[3])),
                        //521B1D's extra WORD is the known MAPJUMP3 height operand.
                        Jsm.Opcode.MAPJUMP3 => new FieldTransition(unchecked((ushort)arguments[0]), unchecked((short)arguments[1]), unchecked((short)arguments[2]), unchecked((ushort)terminal.Parameter), unchecked((byte)arguments[4]), unchecked((short)arguments[3])),
                        Jsm.Opcode.MAPJUMPO => FieldTransition.FromMapJumpOrigin(arguments[0], arguments[1]),
                        _ => null
                    };
                }
                if (destination is null || destination.DiscJump is not null || destination.FieldId != destinationFieldId || destination.FieldId is < 72 or > ushort.MaxValue or 0x7FFF || !float.IsFinite(destination.X) || !float.IsFinite(destination.Y) || destination.X is < short.MinValue or > short.MaxValue || destination.Y is < short.MinValue or > short.MaxValue || destination.X != MathF.Truncate(destination.X) || destination.Y != MathF.Truncate(destination.Y) || destination.Triangle is < 0 or > short.MaxValue || destination.Direction is < 0 or > byte.MaxValue)
                {
                    return false;
                }
                bool resets = destination.Triangle == short.MaxValue;
                if (resetLeaderDefaults is bool expectedReset && resets != expectedReset)
                {
                    return false;
                }
                resetLeaderDefaults = resets;
            }
            catch (Exception exception) when (exception is EventScriptException or InvalidOperationException or InvalidDataException or ArgumentException or ArithmeticException or NotSupportedException)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Legacy-only policy proof for choosing a current arrival packet after physical approach was discarded. This is not packet equivalence.</summary>
    public bool CanCanonicalizeLegacyGatewayEntries(IReadOnlyList<ReplaySemanticTrigger> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count < 2 || program is null || NativeRuntimeProfile is not { IsVerified: true } || Scene.MapJumpDisabled)
        {
            return false;
        }
        FieldTransition? first = null;
        foreach (ReplaySemanticTrigger candidate in candidates)
        {
            if (candidate is not { Kind: ReplaySemanticTriggerKind.FieldGateway, Inferred: true })
            {
                return false;
            }
            try
            {
                candidate.Validate();
                ValidateSemanticProducer(candidate);
            }
            catch (InvalidDataException)
            {
                return false;
            }
            FieldTransition destination = ResolveSemanticGateway(candidate.InfIndex).Destination;
            if (destination.FieldId is < 72 or > ushort.MaxValue or 0x7FFF || destination.DiscJump is not null || !float.IsFinite(destination.X) || !float.IsFinite(destination.Y) || destination.X is < short.MinValue or > short.MaxValue || destination.Y is < short.MinValue or > short.MaxValue || destination.X != MathF.Truncate(destination.X) || destination.Y != MathF.Truncate(destination.Y) || destination.Triangle is < 0 or > short.MaxValue || destination.Direction is < 0 or > byte.MaxValue)
            {
                return false;
            }
            if (first is not null && (destination.FieldId != first.FieldId || destination.NativeExtraWord != first.NativeExtraWord || (destination.Triangle == short.MaxValue) != (first.Triangle == short.MaxValue)))
            {
                return false;
            }
            first ??= destination;
        }
        return true;
    }

    /// <summary>Whether this current verified producer has an unfinished original VM fiber; it neither starts nor resumes it.</summary>
    public bool IsSemanticProducerPending(ReplayScriptGoal goal)
    {
        ValidateSemanticScriptGoal(goal);
        return fibers.Any(fiber => !fiber.Complete && fiber.ObjectId == goal.ObjectId && fiber.Script.Id == goal.ScriptId);
    }

    private bool CanAdmitSemanticTrigger()
    {
        return program is not null && defaultsStarted && NativeRuntimeProfile is { IsVerified: true } && Scene.NativeActorLayoutVersion == 1 && Scene.NativeFieldLoadVersion == 1 && !transitionRequested && LastFault is null && IsUserControlEnabled && !services.IsMoviePlaying && PartyMenu is null && PendingInteractiveBattle is null && CurrentDialogue is null && PlayerActor is { Active: true, Visible: true };
    }

    private void ValidateSemanticProducer(ReplaySemanticTrigger trigger)
    {
        if (program is null || NativeRuntimeProfile is not { IsVerified: true } || trigger.FieldId != FieldId || trigger.ProgramHash != programHash || !trigger.Inferred && trigger.ExecutableHash is null || trigger.ExecutableHash is string executable && executable != NativeRuntimeProfile.ExecutableHash)
        {
            throw new InvalidDataException("Semantic input does not identify the current verified native field program.");
        }
        if (trigger.Kind == ReplaySemanticTriggerKind.FieldGateway)
        {
            if (trigger.InfIndex < 0 || trigger != (CreateSemanticGateway(ResolveSemanticGateway(trigger.InfIndex)) with { Inferred = trigger.Inferred, ExecutableHash = trigger.ExecutableHash, Sequence = trigger.Sequence }))
            {
                throw new InvalidDataException("Semantic gateway lost its current original INF producer.");
            }
            return;
        }
        if ((uint)trigger.NativeObjectId >= program.NativeObjects.Count || program.NativeObjects[trigger.NativeObjectId] != trigger.ObjectId || (uint)trigger.ObjectId >= program.Objects.Count)
        {
            throw new InvalidDataException("Semantic input lost its original native object-table identity.");
        }
        EventObject obj = program.Objects[trigger.ObjectId];
        if ((uint)trigger.LocalScript >= obj.Scripts.Count || obj.Scripts[trigger.LocalScript].Id != trigger.ScriptId)
        {
            throw new InvalidDataException("Semantic input does not identify a current declared native script.");
        }
        bool valid = trigger.Kind switch
        {
            ReplaySemanticTriggerKind.ActorInteraction => !obj.IsDoor && !obj.IsArea && obj.BackgroundModuleIndex is null && trigger.LocalScript == 2 && trigger.NativeSlot == 7 && trigger.InfIndex == -1 && trigger.EventBits == 0 && !trigger.Contact,
            ReplaySemanticTriggerKind.PushContact => !obj.IsDoor && !obj.IsArea && obj.BackgroundModuleIndex is null && trigger.LocalScript == 3 && trigger.NativeSlot == 6 && trigger.InfIndex == -1 && trigger.EventBits == 0 && !trigger.Contact,
            ReplaySemanticTriggerKind.AreaEntry or ReplaySemanticTriggerKind.AreaExit => obj.IsArea && trigger.LocalScript == 2 && trigger.NativeSlot == 6 && trigger.EventBits == 0 && trigger.Contact == (trigger.Kind == ReplaySemanticTriggerKind.AreaEntry) && trigger.InfIndex >= 0 && InfAreaTriggers.Count(item => item.NativeIndex == trigger.InfIndex) == 1 && InfAreaTriggers.Single(item => item.NativeIndex == trigger.InfIndex).AreaIndex == Array.IndexOf(program.NativeObjects.Where(id => program.Objects[id].IsArea).ToArray(), trigger.ObjectId),
            ReplaySemanticTriggerKind.DoorEntry or ReplaySemanticTriggerKind.DoorAction or ReplaySemanticTriggerKind.DoorCrossing or ReplaySemanticTriggerKind.DoorExit or ReplaySemanticTriggerKind.DoorFacing or ReplaySemanticTriggerKind.DoorContact => obj.IsDoor && trigger.LocalScript is >= 2 and <= 7 && trigger.NativeSlot == 9 - trigger.LocalScript && trigger.Kind == SemanticDoorKind(trigger.LocalScript) && trigger.InfIndex == -1 && NativeDoorRequestedLocals(trigger.EventBits, trigger.Contact).Contains(trigger.LocalScript),
            _ => false
        };
        if (!valid)
        {
            throw new InvalidDataException("Semantic input kind does not match its current native admission producer.");
        }
    }

    private ReplaySemanticTrigger CreateSemanticTrigger(ReplaySemanticTriggerKind kind, int objectId, int local, int slot, int infIndex = -1, byte eventBits = 0, bool contact = false)
    {
        int nativeId = Array.IndexOf(program!.NativeObjects.ToArray(), objectId);
        int scriptId = (uint)local < program.Objects[objectId].Scripts.Count ? program.Objects[objectId].Scripts[local].Id : -1;
        return new ReplaySemanticTrigger(1, kind, FieldId, programHash!, nativeId, objectId, scriptId, slot, local, infIndex, eventBits, contact, ExecutableHash: NativeRuntimeProfile?.ExecutableHash);
    }

    private ReplaySemanticTrigger CreateSemanticGateway(InfGateway gateway)
    {
        return new ReplaySemanticTrigger(1, ReplaySemanticTriggerKind.FieldGateway, FieldId, programHash!, -1, -1, -1, -1, -1, gateway.NativeIndex, Inferred: gateway.NativeIndex < 0, ExecutableHash: NativeRuntimeProfile?.ExecutableHash);
    }

    private InfGateway ResolveSemanticGateway(int nativeIndex)
    {
        InfGateway[] matching = InfGateways.Where(gateway => gateway.NativeIndex == nativeIndex && nativeIndex >= 0).ToArray();
        return matching.Length == 1 ? matching[0] : throw new InvalidDataException("Semantic gateway requires one current original INF index.");
    }

    private static ReplaySemanticTriggerKind SemanticDoorKind(int local)
    {
        return local switch
        {
            2 => ReplaySemanticTriggerKind.DoorAction,
            3 => ReplaySemanticTriggerKind.DoorFacing,
            4 => ReplaySemanticTriggerKind.DoorCrossing,
            5 => ReplaySemanticTriggerKind.DoorContact,
            6 => ReplaySemanticTriggerKind.DoorExit,
            7 => ReplaySemanticTriggerKind.DoorEntry,
            _ => throw new InvalidDataException("Unknown native direct door script.")
        };
    }

    private void RecordSemanticAdmission(ReplaySemanticTrigger trigger)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return;
        }
        ReplaySemanticTrigger admitted = trigger with { Inferred = trigger.Inferred || semanticInputInferred, Sequence = replayCapture is ReplayCapture capture ? capture.NextSequence++ : -1 };
        replayCapture?.SemanticAdmissions.Add(admitted);
        SemanticTriggerAdmitted?.Invoke(admitted);
    }
}
