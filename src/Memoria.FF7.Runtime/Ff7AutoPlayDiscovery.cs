using System.Numerics;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public enum Ff7AutoPlayTargetKind
{
    ActorTalk,
    ActorContact,
    LineTalk,
    LinePush,
    LineCross,
    LineTouch,
    LineEnter,
    LineLeave,
    Gateway
}

public sealed record Ff7AutoPlayTarget(string Id, int Entity, int Script, Ff7AutoPlayTargetKind Kind, string ConditionFingerprint, bool ChangesStory, Vector3 Position, int? Triangle = null, int? GatewayIndex = null, int? DestinationField = null, Vector3? SegmentStart = null, Vector3? SegmentEnd = null, bool AnalysisComplete = true, string? SupportReason = null, bool MayChangeNavigation = false, string? EntryInputContract = null);
public sealed record Ff7AutoPlayObservation(string VisitId, string ProgramIdentity, long Generation, Ff7AutoPlayTarget[] Events, bool CanAcceptInput, bool AnalysisComplete, string? SupportReason = null)
{
    public string NavigationKey { get; init; } = VisitId;
    public FieldVariableRead[] PersistentReads { get; init; } = [];
    public FieldHostRead[] GlobalHostReads { get; init; } = [];
}

/// <summary>Read-only discovery of currently allocated native field interaction owners.</summary>
public sealed partial class Ff7AutoPlayDiscovery(Ff7Session session)
{
    // Optional coverage learned before dispatch-phase entry admission must be observed again.
    private const int EntryAdmissionRevision = 2;
    private readonly Dictionary<(int Entity, int Script), FieldScriptAnalysis> analyses = [];
    private readonly Dictionary<(int Entity, int Script), FieldScriptInputDemand?> entryInputs = [];
    private string? analysisAllocation;

    public Ff7AutoPlayTarget? FindTarget(string id) => Observe().Events.SingleOrDefault(target => target.Id == id);

    public Ff7AutoPlayObservation Observe()
    {
        string identity = session.Machine.ProgramIdentity;
        string visit = $"ff7:{session.MapId}:{identity}";
        Ff7FieldProcess process = session.Host.Process;
        bool canAccept = session.World is null && !session.HasPendingTick && session.Machine.Initialized && process.GlobalCommand == Ff7GlobalCommand.None && !process.PlayerLocked && process.Join is null && process.Transition is null && !process.Dialogues.Any(dialogue => !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)) && process.Actors.Any(actor => actor.PlayerControlled && actor.Ladder is null && actor.Jump is null && !session.Host.HasScriptedMotion(actor));
        List<Ff7AutoPlayTarget> events = [];
        HashSet<FieldVariableRead> persistentReads = [];
        HashSet<FieldHostRead> globalHostReads = [];
        if (session.World is not null)
        {
            return new(visit, identity, session.FieldGeneration, [], false, false, "World discovery requires a world observation adapter.");
        }
        string allocation = session.Machine.ReadScriptAnalysisAllocationFingerprint();
        if (analysisAllocation != allocation)
        {
            analyses.Clear();
            entryInputs.Clear();
            analysisAllocation = allocation;
        }
        foreach (Ff7Actor actor in process.Actors.Where(actor => actor.EntityBound && !actor.PlayerControlled).OrderBy(actor => actor.Entity))
        {
            Vector3 position = new(actor.X4096 / 4096f, actor.Y4096 / 4096f, actor.Z4096 / 4096f);
            if (actor.TalkEnabled)
            {
                AddScript($"actor:{actor.Entity}:script:1", actor.Entity, 1, Ff7AutoPlayTargetKind.ActorTalk, position, actor.Triangle);
            }
            if (actor.CollisionEnabled)
            {
                AddScript($"actor:{actor.Entity}:script:2", actor.Entity, 2, Ff7AutoPlayTargetKind.ActorContact, position, actor.Triangle);
            }
        }
        foreach (Ff7Line line in process.Lines.Where(line => line.Enabled == 1).OrderBy(line => line.Slot))
        {
            Vector3 start = new(line.X1, line.Y1, line.Z1);
            Vector3 end = new(line.X2, line.Y2, line.Z2);
            for (int script = 1; script <= 6; script++)
            {
                Ff7AutoPlayTargetKind kind = script switch
                {
                    1 => Ff7AutoPlayTargetKind.LineTalk,
                    2 => Ff7AutoPlayTargetKind.LinePush,
                    3 => Ff7AutoPlayTargetKind.LineCross,
                    4 => Ff7AutoPlayTargetKind.LineTouch,
                    5 => Ff7AutoPlayTargetKind.LineEnter,
                    _ => Ff7AutoPlayTargetKind.LineLeave
                };
                AddScript($"line:{line.Slot}:entity:{line.Entity}:script:{script}", line.Entity, script, kind, (start + end) / 2, start: start, end: end);
            }
        }
        if (!process.MapJumpDisabled)
        {
            for (int index = 0; index < session.Field.Triggers.Gateways.Count; index++)
            {
                var gateway = session.Field.Triggers.Gateways[index];
                if (gateway.DestinationField == short.MaxValue)
                {
                    continue;
                }
                Vector3 start = gateway.A;
                Vector3 end = gateway.B;
                events.Add(new($"gateway:{index}", -1, -1, Ff7AutoPlayTargetKind.Gateway, $"{identity}:gateway:{index}:{gateway.DestinationField}:{gateway.DestinationTriangle}", false, (start + end) / 2, GatewayIndex: index, DestinationField: gateway.DestinationField, SegmentStart: start, SegmentEnd: end));
            }
        }
        bool complete = events.All(target => target.AnalysisComplete);
        return new(visit, identity, session.FieldGeneration, events.ToArray(), canAccept, complete, complete ? null : string.Join("; ", events.Where(target => !target.AnalysisComplete).Select(target => $"{target.Id}: {target.SupportReason}")))
        {
            NavigationKey = Ff7AutoplayDriver.ReadNavigationKey(session, visit),
            PersistentReads = persistentReads.OrderBy(read => read.Bank).ThenBy(read => read.Operand).ThenBy(read => read.Word).ToArray(),
            GlobalHostReads = globalHostReads.OrderBy(read => read.Kind).ThenBy(read => read.Parameter).ThenBy(read => read.Selector).ToArray()
        };

        void AddScript(string id, int entity, int script, Ff7AutoPlayTargetKind kind, Vector3 position, int? triangle = null, Vector3? start = null, Vector3? end = null)
        {
            int entry = session.Field.Scripts.Entities[entity].ScriptOffsets[script];
            if (session.Field.Scripts.Data.Span[entry] == 0)
            {
                return;
            }
            if (!analyses.TryGetValue((entity, script), out FieldScriptAnalysis? analysis) || analysis.Fingerprint != session.Machine.ReadScriptAnalysisFingerprint(entity, script, analysis.Reads))
            {
                entryInputs.Remove((entity, script));
                analysis = session.Machine.AnalyzeScript(entity, script);
                if (analysis.Complete)
                {
                    analyses[(entity, script)] = analysis;
                }
            }
            string fingerprint = analysis.Complete ? session.Machine.ReadScriptAnalysisFingerprint(entity, script, analysis.Reads) : analysis.Fingerprint;
            if (analysis.Complete)
            {
                persistentReads.UnionWith(analysis.Reads.Where(read => read.Bank is not (FieldBank.Immediate or FieldBank.MapByte or FieldBank.MapWord)));
                globalHostReads.UnionWith(analysis.HostReads.Where(read => read.Kind is FieldHostReadKind.Gil or FieldHostReadKind.ItemQuantity or FieldHostReadKind.PartyAvailable or FieldHostReadKind.MateriaCapacity));
            }
            Ff7AutoPlayHostConditions external = ReadHostConditions(analysis.HostReads, session.Progress, session.Host.Process, session.Host.ReadNativeMusicPlaying);
            fingerprint = CombineConditionFingerprints(fingerprint, external.Fingerprint);
            if (!entryInputs.TryGetValue((entity, script), out FieldScriptInputDemand? entryInput))
            {
                entryInput = session.Machine.ReadEntryInputDemand(entity, script);
                entryInputs[(entity, script)] = entryInput;
            }
            string? entryContract = null;
            if (entryInput is not null)
            {
                entryContract = $"entry-admission:{EntryAdmissionRevision}:{entryInput.Identity}:{entryInput.Kind}:{entryInput.Mask}:{entryInput.Mapped}";
                fingerprint = CombineConditionFingerprints(fingerprint, entryContract);
            }
            int? destination = analysis.DestinationFields.Length == 1 ? analysis.DestinationFields[0] : null;
            events.Add(new(id, entity, script, kind, fingerprint, analysis.ChangesStory, position, triangle, DestinationField: destination, SegmentStart: start, SegmentEnd: end, AnalysisComplete: analysis.Complete && external.Complete, SupportReason: analysis.Reason ?? external.Reason, MayChangeNavigation: analysis.MayChangeNavigation, EntryInputContract: entryContract));
        }
    }
}
