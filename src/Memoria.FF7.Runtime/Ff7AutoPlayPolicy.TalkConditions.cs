using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayTalkConditionDomain(string Condition, string VisitGuard, string Context, FieldVariableRead[] Reads, FieldHostRead[] HostReads, Dictionary<int, string> Conditions, int Entity = -1, string? Geometry = null, bool Complete = false);

public sealed partial class Ff7AutoPlayPolicy
{
    private sealed record TalkContactPlan(string Key, string Context, Vector3 Point, int Triangle, string Condition);
    private TalkContactPlan? talkContactPlan;
    private readonly Dictionary<string, (string Context, HashSet<(Vector3 Point, int Triangle)> Points)> failedTalkContactAttempts = new(StringComparer.Ordinal);

    private bool RotateFailedTalkContact(Ff7AutoPlayActiveEvent active)
    {
        if (active is not { Kind: Ff7AutoPlayTargetKind.ActorTalk, Accepted: false, NavigationKey: not null } || active.Generation != session.FieldGeneration || talkContactPlan is not { } contact || contact.Key != TalkDomainKey(active.NavigationKey, active.EventId))
        {
            return false;
        }
        if (!failedTalkContactAttempts.TryGetValue(contact.Key, out var attempts) || attempts.Context != contact.Context)
        {
            attempts = (contact.Context, []);
            failedTalkContactAttempts[contact.Key] = attempts;
        }
        attempts.Points.Add((contact.Point, contact.Triangle));
        talkContactPlan = null;
        return true;
    }

    private string ReadTalkContactMovementIdentity(Ff7AutoPlayActiveEvent active)
    {
        return talkContactPlan is { } contact && active.NavigationKey is not null && contact.Key == TalkDomainKey(active.NavigationKey, active.EventId) ? $"{contact.Context}:{contact.Point}:{contact.Triangle}" : string.Empty;
    }

    internal static bool MayContainTalkContact(PcWalkmeshTriangle triangle, int targetX, int targetY, int targetZ, int range)
    {
        if (range <= 0)
        {
            return false;
        }
        Vector3[] vertices = [triangle.A, triangle.B, triangle.C];
        // Native floor traversal tests truncated center coordinates; talking uses floor coordinates.
        double minX = vertices.Min(point => point.X) - 1;
        double maxX = vertices.Max(point => point.X);
        double minY = vertices.Min(point => point.Y) - 1;
        double maxY = vertices.Max(point => point.Y);
        double dx = Math.Max(0, Math.Max(minX - targetX, targetX - maxX));
        double dy = Math.Max(0, Math.Max(minY - targetY, targetY - maxY));
        double farX = Math.Max(Math.Abs(minX - targetX), Math.Abs(maxX - targetX));
        double farY = Math.Max(Math.Abs(minY - targetY), Math.Abs(maxY - targetY));
        if (farX * farX + farY * farY > int.MaxValue)
        {
            return true;
        }
        return dx * dx + dy * dy < (double)range * range && vertices.Max(point => point.Z) >= targetZ - 257 && vertices.Min(point => point.Z) <= targetZ + 257;
    }

    private string ReadTalkConditionGeometry(Ff7Actor actor, Ff7Actor player)
    {
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { actor.Entity, actor.Slot, actor.Model, actor.EntityBound, actor.PlayerControlled, actor.TalkEnabled, actor.X4096, actor.Y4096, actor.Z4096, actor.Triangle, actor.TalkRadius, actor.CollisionRadius, Player = new { player.Entity, player.Model, player.EntityBound, player.CollisionRadius } })));
    }

    private string ReadTalkConditionContext(IReadOnlyList<FieldVariableRead> reads, IReadOnlyList<FieldHostRead> hosts)
    {
        Ff7AutoPlayHostConditions external = Ff7AutoPlayDiscovery.ReadHostConditions(hosts, session.Progress, session.Host.Process, session.Host.ReadNativeMusicPlaying);
        if (!external.Complete)
        {
            throw new NotSupportedException(external.Reason);
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Variables = reads.Select(read => new { Read = read, Value = read.Word ? session.Machine.Variables.ReadWord(read.Bank, read.Operand) : session.Machine.Variables.ReadByte(read.Bank, checked((byte)read.Operand)) }),
            External = external.Fingerprint
        })));
    }

    private Ff7AutoPlayTalkConditionDomain? ObserveTalkConditionDomain(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target)
    {
        if (target.Kind != Ff7AutoPlayTargetKind.ActorTalk || !target.AnalysisComplete || target.EntryInputDemand is not null || !observation.CanAcceptInput)
        {
            return null;
        }
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Ff7Actor? actor = session.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == target.Entity && !actor.PlayerControlled && actor.TalkEnabled);
        if (player is null || actor is null)
        {
            return null;
        }
        FieldScriptEntryDependencies? dependencies = discovery.ReadTalkContactDependencies(target);
        if (dependencies is not { Complete: true } || dependencies.Reads.Any(read => read.Bank is FieldBank.MapByte or FieldBank.MapWord) || !SupportsTalkContactProjection(dependencies.HostReads, player.Entity))
        {
            return null;
        }
        string variables = session.Machine.ReadScriptAnalysisFingerprint(target.Entity, target.Script, dependencies.Reads);
        string Project(int triangle)
        {
            Ff7FieldProcess projected = session.Host.Process with { Actors = session.Host.Process.Actors.Select(item => item.Slot == player.Slot ? item with { Triangle = triangle } : item).ToArray() };
            Ff7AutoPlayHostConditions external = Ff7AutoPlayDiscovery.ReadHostConditions(dependencies.HostReads, session.Progress, projected, session.Host.ReadNativeMusicPlaying);
            if (!external.Complete)
            {
                throw new NotSupportedException(external.Reason);
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(variables + "|" + external.Fingerprint)));
        }
        if (Project(player.Triangle) != target.ConditionFingerprint || !State.VisitGuards.TryGetValue(observation.VisitId, out Ff7AutoPlayVisitGuard? guard))
        {
            return null;
        }
        Dictionary<int, string> conditions = session.Field.Walkmesh.Where(triangle => MayContainTalkContact(triangle, actor.X4096 >> 12, actor.Y4096 >> 12, actor.Z4096 >> 12, actor.TalkRadius + player.CollisionRadius)).ToDictionary(triangle => triangle.Index, triangle => Project(triangle.Index));
        if (session.GetTalkTarget()?.Entity == target.Entity && !conditions.ContainsKey(player.Triangle))
        {
            return null;
        }
        FieldHostRead[] globalHosts = dependencies.HostReads.Where(read => read.Kind != FieldHostReadKind.Actor).ToArray();
        Ff7AutoPlayTalkConditionDomain result = new(target.ConditionFingerprint, guard.Fingerprint, ReadTalkConditionContext(dependencies.Reads, globalHosts), dependencies.Reads, globalHosts, conditions, actor.Entity, ReadTalkConditionGeometry(actor, player));
        State.TalkConditionDomains ??= new(StringComparer.Ordinal);
        State.TalkConditionDomains[TalkDomainKey(observation.NavigationKey, target.Id)] = result;
        return result;
    }

    internal static bool SupportsTalkContactProjection(IReadOnlyList<FieldHostRead> reads, int player)
    {
        return reads.Any(read => read.Kind == FieldHostReadKind.Actor) && !reads.Any(read => read.Kind == FieldHostReadKind.Actor && (read.Parameter != player || read.Selector != (int)FieldActorQueryKind.Triangle)) && !reads.Any(read => read.Kind is not (FieldHostReadKind.Actor or FieldHostReadKind.Gil or FieldHostReadKind.ItemQuantity or FieldHostReadKind.PartyAvailable or FieldHostReadKind.MateriaCapacity or FieldHostReadKind.MusicPlaying));
    }

    internal static bool HasConsumedTalkContactDomain(Ff7AutoPlayTalkConditionDomain? domain, string? condition, string guard, string context, bool needsRefresh, AutoPlayConditionCoverage? coverage)
    {
        return domain is { Complete: true, Conditions.Count: > 0 } && domain.Condition == condition && domain.VisitGuard == guard && domain.Context == context && !needsRefresh && coverage is not null && domain.Conditions.Values.All(coverage.HasExecutedEntry);
    }

    private static string TalkDomainKey(string navigation, string target) => navigation + "|" + target;

    private AutoPlayEventAvailability? ReadTalkConditionAvailability(AutoPlayNavigationVisit navigation, AutoPlayEvent observed, Ff7AutoPlayObservation observation)
    {
        string key = TalkDomainKey(navigation.Key, observed.Id);
        if (navigation.Key == observation.NavigationKey && observation.Events.SingleOrDefault(target => target.Id == observed.Id) is { } target)
        {
            if (ObserveTalkConditionDomain(observation, target) is null)
            {
                State.TalkConditionDomains?.Remove(key);
                return null;
            }
        }
        if (State.TalkConditionDomains is null || !State.TalkConditionDomains.TryGetValue(key, out Ff7AutoPlayTalkConditionDomain? domain) || domain is null || domain.Entity < 0 || domain.Geometry is not { Length: 64 } geometry || !geometry.All(char.IsAsciiHexDigit) || domain.Condition != observed.ConditionSignature || domain.Conditions is not { Count: > 0 } || domain.Reads is null || domain.HostReads is null || !State.VisitGuards.TryGetValue(navigation.VisitKey, out Ff7AutoPlayVisitGuard? guard) || guard.Fingerprint != domain.VisitGuard || NeedsVisitRefresh(new(navigation.Key, navigation.VisitKey)) || ReadTalkConditionContext(domain.Reads, domain.HostReads) != domain.Context)
        {
            return null;
        }
        if (navigation.VisitKey == observation.VisitId)
        {
            Ff7Actor? currentActor = session.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == domain.Entity);
            Ff7Actor? currentPlayer = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
            if (currentActor is null || currentPlayer is null || domain.Geometry != ReadTalkConditionGeometry(currentActor, currentPlayer))
            {
                return null;
            }
        }
        if (navigation.VisitKey != observation.VisitId && domain.HostReads.Any(read => read.Kind == FieldHostReadKind.MusicPlaying))
        {
            return null;
        }
        var coverage = State.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == navigation.VisitKey)?.Events.SingleOrDefault(item => item.Event.Id == observed.Id);
        return HasConsumedTalkContactDomain(domain, observed.ConditionSignature, guard.Fingerprint, domain.Context, false, coverage?.Conditions) ? new(true, new(StringComparer.Ordinal) { observed.Id }, "consumed-talk-contact-domain:" + domain.Context) : null;
    }

    private TalkContactPlan? ReadFreshTalkContact(Ff7AutoplayDriver driver, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target)
    {
        Ff7AutoPlayTalkConditionDomain? domain = ObserveTalkConditionDomain(observation, target);
        var coverage = State.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == target.Id);
        if (domain is null || coverage?.Conditions is not { } executed)
        {
            return null;
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Actor actor = session.Host.GetActor(target.Entity);
        var contacts = driver.DiscoverTalkContacts(actor, player).Contacts.Where(contact => domain.Conditions.TryGetValue(contact.Triangle, out string? condition) && !executed.HasExecutedEntry(condition)).ToArray();
        string key = TalkDomainKey(observation.NavigationKey, target.Id);
        string context = domain.Context + "|" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Geometry = domain.Geometry,
            observation.NavigationKey,
            session.Host.Process.MapJumpDisabled,
            Boundaries = driver.CaptureSpatialBoundaryTriangles(),
            Blockers = session.Host.Process.Actors.Where(item => !item.PlayerControlled && item.CollisionEnabled).Select(item => new { item.Slot, item.Entity, item.Model, item.EntityBound, item.X4096, item.Y4096, item.Z4096, item.Triangle, item.CollisionRadius }),
            Lines = session.Host.Process.Lines.Select(line => new { line.Slot, line.Entity, line.X1, line.Y1, line.Z1, line.X2, line.Y2, line.Z2, line.Enabled, line.SlipDisabled })
        })));
        if (!failedTalkContactAttempts.TryGetValue(key, out var attempts) || attempts.Context != context)
        {
            attempts = (context, []);
            failedTalkContactAttempts[key] = attempts;
        }
        HashSet<(Vector3 Point, int Triangle)> failedTalkContacts = attempts.Points;
        if (contacts.Length != 0 && contacts.All(failedTalkContacts.Contains))
        {
            // A failed witness remains retryable; completing one fair round proves no negative.
            failedTalkContacts.Clear();
        }
        if (talkContactPlan is { } retained && retained.Key == key && retained.Context == context && contacts.Any(contact => contact.Point == retained.Point && contact.Triangle == retained.Triangle) && !failedTalkContacts.Contains((retained.Point, retained.Triangle)))
        {
            return retained;
        }
        Vector3 position = new(player.X4096 / 4096f, player.Y4096 / 4096f, player.Z4096 / 4096f);
        var candidate = contacts.Where(contact => !failedTalkContacts.Contains(contact)).OrderBy(contact => failedTalkContacts.Count(failed => failed.Triangle == contact.Triangle)).ThenBy(contact => Vector3.DistanceSquared(position, contact.Point)).ThenBy(contact => contact.Triangle).Cast<(Vector3 Point, int Triangle)?>().FirstOrDefault();
        talkContactPlan = candidate is { } fresh ? new(key, context, fresh.Point, fresh.Triangle, domain.Conditions[fresh.Triangle]) : null;
        return talkContactPlan;
    }

    private Ff7AutoplayInput ChooseConditionAwareTalkInput(Ff7AutoplayDriver driver, Ff7AutoPlayTarget target, bool run)
    {
        if (ordinaryObservation is not { } observation || ObserveTalkConditionDomain(observation, target) is null)
        {
            return driver.TalkTo(target.Entity, run);
        }
        var currentCoverage = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id);
        if (session.GetTalkTarget()?.Entity == target.Entity && currentCoverage.Conditions?.HasExecutedEntry(target.ConditionFingerprint) != true)
        {
            return driver.TalkTo(target.Entity, run);
        }
        if (ReadFreshTalkContact(driver, observation, target) is not { } contact)
        {
            driver.DeferPlanning();
            return default;
        }
        FieldScriptEntryDependencies dependencies = discovery.ReadTalkContactDependencies(target)!;
        string variables = session.Machine.ReadScriptAnalysisFingerprint(target.Entity, target.Script, dependencies.Reads);
        var executed = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id).Conditions!;
        bool FreshContact(Ff7Actor projected)
        {
            Ff7FieldProcess process = session.Host.Process with { Actors = session.Host.Process.Actors.Select(item => item.Slot == projected.Slot ? projected : item).ToArray() };
            Ff7AutoPlayHostConditions external = Ff7AutoPlayDiscovery.ReadHostConditions(dependencies.HostReads, session.Progress, process, session.Host.ReadNativeMusicPlaying);
            string condition = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(variables + "|" + external.Fingerprint)));
            return external.Complete && !executed.HasExecutedEntry(condition);
        }
        try
        {
            return driver.TalkToContact(target.Entity, contact.Point, contact.Triangle, run, contact.Context, FreshContact);
        }
        catch (Ff7AutoPlayNavigationUnavailableException)
        {
            if (driver.PreparingNavigationMode)
            {
                throw;
            }
            if (State.Active is { } active)
            {
                RotateFailedTalkContact(active);
            }
            driver.DeferPlanning();
            return default;
        }
    }
}
