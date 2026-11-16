using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayWorldModelScriptObservation(string Id, int Model, int Event, Ff7WorldEntity Target, WorldScriptAnalysis Analysis, string PredicateFingerprint, string GeometryFingerprint, bool Complete, string? Reason);

public sealed partial class Ff7WorldRuntime
{
    public Ff7AutoPlayWorldModelScriptObservation[] ReadAutoPlayModelFrontiers(int instructionBudget = 2048)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(instructionBudget, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(instructionBudget, 65536);
        List<Ff7AutoPlayWorldModelScriptObservation> result = [];
        int controlledOwner = ControlledModel;
        Ff7WorldEntity[] hostEntities = entities.Values.ToArray();
        Ff7WorldEntity[] hostFreeEntities = freeEntities.ToArray();
        int? ReadHost(WorldHostDependency dependency) => ProjectAutoPlayHostDependency(dependency, controlledOwner, hostEntities, hostFreeEntities);
        foreach (Ff7WorldEntity source in ReadAutoPlayModelContactSources())
        {
            foreach (int eventId in new[] { 3, 4 })
            {
                WorldScriptAnalysis analysis = Machine.AnalyzeModelDispatch(source.Model, eventId, instructionBudget, currentEntryOnly: true, readHost: ReadHost);
                string predicate = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Predicate = Machine.ReadPredicateFingerprint(analysis.Reads), analysis.HostValues })));
                string geometry = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { source.Model, source.X, source.Z, CollisionDisabled = source.Flags & 128 })));
                result.Add(new($"model:{source.Model}:event:{eventId}", source.Model, eventId, source, analysis, predicate, geometry, analysis.Complete, analysis.Reason));
            }
        }
        return result.ToArray();
    }
}
