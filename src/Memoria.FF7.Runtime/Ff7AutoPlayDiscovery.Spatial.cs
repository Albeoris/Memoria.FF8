using Memoria.FF7.Events;

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
        string Source(FieldVariableRead[] reads) => $"{session.FieldGeneration}:{player.Entity}:{session.Machine.ReadScriptAnalysisAllocationFingerprint()}:{session.Machine.ReadScriptAnalysisFingerprint(player.Entity, 0, reads)}";
        if (spatialObservation is null || spatialSource != Source(spatialObservation.Reads))
        {
            FieldSpatialSample[] samples = session.Field.Walkmesh.Select(triangle =>
            {
                var point = (triangle.A + triangle.B + triangle.C) / 3;
                return new FieldSpatialSample(triangle.Index, new(checked((short)point.X), checked((short)point.Y), checked((short)point.Z), checked((short)triangle.Index)));
            }).ToArray();
            spatialObservation = session.Machine.ReadCurrentSpatialDemands(player.Entity, samples);
            spatialSource = Source(spatialObservation.Reads);
        }
        persistentReads.UnionWith(spatialObservation.Reads.Where(read => read.Bank is not (FieldBank.Immediate or FieldBank.MapByte or FieldBank.MapWord)));
        foreach (FieldSpatialDemand demand in spatialObservation.Demands)
        {
            var triangle = session.Field.Walkmesh[demand.SampleId];
            Ff7AutoPlayHostConditions external = ReadHostConditions(demand.HostReads, session.Progress, session.Host.Process, session.Host.ReadNativeMusicPlaying);
            globalHostReads.UnionWith(demand.HostReads.Where(read => read.Kind is FieldHostReadKind.Gil or FieldHostReadKind.ItemQuantity or FieldHostReadKind.PartyAvailable or FieldHostReadKind.MateriaCapacity));
            int? destination = demand.DestinationFields.Length == 1 ? demand.DestinationFields[0] : null;
            targets.Add(new($"spatial:{demand.Entity}:{demand.ProgramCounter:X4}:triangle:{demand.SampleId}", demand.Entity, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, CombineConditionFingerprints(demand.Fingerprint, external.Fingerprint), demand.ChangesStory, (triangle.A + triangle.B + triangle.C) / 3, Triangle: triangle.Index, DestinationField: destination, AnalysisComplete: external.Complete, SupportReason: external.Reason, ChangesField: true) { SpatialDestinations = demand.DestinationFields, TriangleOnly = demand.TriangleOnly });
        }
    }
}
