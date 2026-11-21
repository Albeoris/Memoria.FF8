using Memoria.FF7.Events;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayDiscovery
{
    private FieldSpatialObservation? spatialObservation;
    private string? spatialSource;

    private void AddSpatialTargets(List<Ff7AutoPlayTarget> targets, HashSet<FieldVariableRead> persistentReads, HashSet<FieldHostRead> globalHostReads)
    {
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || !session.Machine.HasCompletedInitialCycle)
        {
            return;
        }
        FieldSpatialRandomDomain randomDomain = session.ReadSpatialRandomDomain();
        string adviceSource = session.ReadSpatialAdviceFingerprint();
        string Source(FieldVariableRead[] reads) => $"{session.FieldGeneration}:{player.Entity}:{session.Machine.ReadScriptAnalysisAllocationFingerprint()}:{session.Machine.ReadSpatialExecutionFingerprint()}:{session.Machine.ReadScriptAnalysisFingerprint(player.Entity, 0, reads)}:{randomDomain.Fingerprint}:{adviceSource}";
        if (spatialObservation is null || spatialSource != Source(spatialObservation.Reads))
        {
            FieldSpatialSample[] samples = session.Field.Walkmesh.Select(triangle =>
            {
                var point = (triangle.A + triangle.B + triangle.C) / 3;
                return new FieldSpatialSample(triangle.Index, new(checked((short)point.X), checked((short)point.Y), checked((short)point.Z), checked((short)triangle.Index)));
            }).ToArray();
            spatialObservation = session.Machine.ReadCurrentSpatialDemands(player.Entity, samples, randomDomain: randomDomain, adviceContext: session.ReadSpatialAdviceContext(samples));
            spatialSource = Source(spatialObservation.Reads);
        }
        persistentReads.UnionWith(spatialObservation.Reads.Where(read => read.Bank is not (FieldBank.Immediate or FieldBank.MapByte or FieldBank.MapWord)));
        byte[] blocked = session.Host.Process.BlockedTriangles;
        IReadOnlySet<int>? reachable = (uint)player.Triangle < session.Field.Walkmesh.Count && blocked.Length >= (session.Field.Walkmesh.Count + 7) / 8 ? Ff7AutoplayDriver.ReachableTriangles(session.Field.Walkmesh, blocked, player.Triangle) : null;
        foreach (FieldSpatialDemand demand in spatialObservation.Demands)
        {
            if (!CanOfferSpatialTriangle(demand.SampleId, demand.TriangleOnly, reachable))
            {
                continue;
            }
            var triangle = session.Field.Walkmesh[demand.SampleId];
            Ff7AutoPlayHostConditions external = ReadHostConditions(demand.HostReads, session.Progress, session.Host.Process, session.Host.ReadNativeMusicPlaying);
            globalHostReads.UnionWith(demand.HostReads.Where(read => read.Kind is FieldHostReadKind.Gil or FieldHostReadKind.ItemQuantity or FieldHostReadKind.PartyAvailable or FieldHostReadKind.MateriaCapacity or FieldHostReadKind.MainPartySlot));
            int? destination = demand.DestinationFields.Length == 1 ? demand.DestinationFields[0] : null;
            string owner = demand.DirectEffectOwner is not null ? "effect:" : demand.TargetScript == -1 ? "direct:" : string.Empty;
            bool effectComplete = demand.DirectEffectOwner is not { ExitProgramCounters.Length: 0 };
            string inputContract = demand.InputGuards.Length == 0 ? string.Empty : $":input:{SpatialInputGuardSignature(demand.InputGuards)}";
            Ff7AutoPlayTarget target = new($"spatial:{demand.Entity}:{demand.ProgramCounter:X4}:{owner}triangle:{demand.SampleId}{inputContract}", demand.Entity, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, CombineConditionFingerprints(demand.Fingerprint, external.Fingerprint), demand.ChangesStory, (triangle.A + triangle.B + triangle.C) / 3, Triangle: triangle.Index, DestinationField: destination, AnalysisComplete: external.Complete && effectComplete, SupportReason: effectComplete ? external.Reason : "The direct native spatial effect has no proved completion instruction.", MayChangeNavigation: demand.MayChangeNavigation, ChangesField: demand.DestinationFields.Length > 0) { SpatialOwner = demand.TargetScript < 0 ? null : new(demand.ProgramCounter, demand.TargetEntity, demand.TargetScript, demand.RequestPriority, demand.RequestMode, demand.SourcePriority), SpatialDestinations = demand.DestinationFields, TriangleOnly = demand.TriangleOnly, SpatialInputGuards = demand.InputGuards, SpatialSourceProgramCounter = demand.ProgramCounter, DirectEffectOwner = demand.DirectEffectOwner };
            target = target with { PlanningDestinationFields = demand.PlanningDestinationFields };
            Ff7AutoPlayTarget? duplicate = targets.SingleOrDefault(existing => existing.Id == target.Id);
            if (duplicate is null)
            {
                targets.Add(target);
            }
            else if (duplicate.ConditionFingerprint != target.ConditionFingerprint || duplicate.SpatialOwner != target.SpatialOwner || !DirectEffectOwnersEqual(duplicate.DirectEffectOwner, target.DirectEffectOwner) || duplicate.TriangleOnly != target.TriangleOnly || duplicate.AnalysisComplete != target.AnalysisComplete || duplicate.ChangesStory != target.ChangesStory || duplicate.MayChangeNavigation != target.MayChangeNavigation || !(duplicate.SpatialDestinations ?? []).SequenceEqual(target.SpatialDestinations ?? []))
            {
                throw new NotSupportedException("Distinct native spatial paths share an unresolved autoplay target identity.");
            }
        }
    }

    private static bool DirectEffectOwnersEqual(FieldSpatialEffectOwner? left, FieldSpatialEffectOwner? right)
    {
        return left is null && right is null || left is not null && right is not null && left.Entity == right.Entity && left.Priority == right.Priority && left.ProgramCounter == right.ProgramCounter && left.Command == right.Command && left.ExitProgramCounters.SequenceEqual(right.ExitProgramCounters);
    }

    internal static bool CanOfferSpatialTriangle(int triangle, bool triangleOnly, IReadOnlySet<int>? reachable)
    {
        return !triangleOnly || reachable is null || triangle < 0 || reachable.Contains(triangle);
    }

    internal static string SpatialInputGuardSignature(IReadOnlyList<FieldSpatialInputGuard> guards)
    {
        string facts = string.Join('|', guards.Select(guard => $"{guard.Demand.Entity}:{guard.Demand.Priority}:{guard.Demand.ProgramCounter}:{guard.Demand.Kind}:{guard.Demand.Mask}:{guard.Demand.Mapped}:{guard.Matched}").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts)));
    }

}
