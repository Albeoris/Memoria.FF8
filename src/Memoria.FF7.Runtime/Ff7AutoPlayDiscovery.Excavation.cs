using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayDiscovery
{
    internal const string ExcavationDigEventPrefix = "spatial:0:06CD:excavation-harp:triangle:";
    internal const string ExcavationRewardProgram = "ED04C8C40A6AA8EB9AF8681DC31E804D85176B47540FAAA84FA9D3B087C91E32";

    internal static bool IsExcavationRewardContext(string program, int entity, int script, Ff7AutoPlayTargetKind kind, FieldVariables variables)
    {
        return program == ExcavationRewardProgram && entity == 13 && script == 1 && kind == Ff7AutoPlayTargetKind.ActorTalk && variables.ReadByte(FieldBank.Persistent0Byte, 0xEA) == 1 && variables.ReadByte(FieldBank.Persistent0Byte, 0xEB) == 1 && (variables.ReadByte(FieldBank.Persistent0Byte, 0xE7) & 8) == 0;
    }

    private Ff7AutoPlayTarget? ReadExcavationRewardTarget(string id, int entity, int script, Ff7AutoPlayTargetKind kind, System.Numerics.Vector3 position, int? triangle, HashSet<FieldVariableRead> persistentReads)
    {
        if (!IsExcavationRewardContext(session.Machine.ProgramIdentity, entity, script, kind, session.Machine.Variables))
        {
            return null;
        }
        FieldScriptAnalysis award = session.Machine.AnalyzeScriptAtProgramCounter(13, 0x0B05, exitBoundaries: new HashSet<int> { 0x0B10 });
        FieldScriptAnalysis footer = session.Machine.AnalyzeScriptAtProgramCounter(13, 0x0E57, acquiredControls: new HashSet<FieldControlKind> { FieldControlKind.PlayerLocked, FieldControlKind.MenuDisabled });
        if (!award.Complete || !footer.Complete || award.HostReads.Concat(footer.HostReads).Any(read => read.Kind == FieldHostReadKind.Random))
        {
            return null;
        }
        FieldVariableRead[] guards = [new(FieldBank.Persistent0Byte, 0xEA, false), new(FieldBank.Persistent0Byte, 0xEB, false), new(FieldBank.Persistent0Byte, 0xE7, false)];
        persistentReads.UnionWith(guards);
        string condition = CombineConditionFingerprints(session.Machine.ReadScriptAnalysisFingerprint(entity, script, guards), $"excavation-harp-award:{award.Fingerprint}:{footer.Fingerprint}");
        return new(id, entity, script, kind, condition, false, position, triangle);
    }

    private void AddExcavationDigTarget(List<Ff7AutoPlayTarget> targets, HashSet<FieldVariableRead> persistentReads)
    {
        if (!Ff7AutoPlayScriptedInput.IsExcavationProgram(session.Machine.ProgramIdentity))
        {
            return;
        }
        FieldVmSnapshot vm = session.Machine.Capture();
        if (Ff7AutoPlayScriptedInput.ReadExcavationDigTriangle(vm, session.Host.Process) is not int triangle || (uint)triangle >= session.Field.Walkmesh.Count)
        {
            return;
        }
        FieldScriptInputDemand? input = session.Machine.ReadCurrentInputDemands().SingleOrDefault(demand => demand.Entity == 0 && demand.Priority == 7 && demand.ProgramCounter == 0x06C9 && demand.Kind == FieldScriptInputKind.Pressed && demand.Mask == 0x80 && !demand.Mapped && demand.AwaitingInput);
        if (input is null)
        {
            return;
        }
        FieldVariableRead[] reads = [new(FieldBank.MapByte, 0x0C, false), new(FieldBank.MapByte, 0x2B, false), new(FieldBank.Persistent0Byte, 0xEB, false), new(FieldBank.Persistent0Byte, 0xEA, false), new(FieldBank.Persistent0Byte, 0xE7, false), .. Enumerable.Range(0, 7).Select(index => new FieldVariableRead(FieldBank.MapWord, (ushort)(0x10 + index * 2), true))];
        persistentReads.UnionWith(reads.Where(read => read.Bank == FieldBank.Persistent0Byte));
        var face = session.Field.Walkmesh[triangle];
        string condition = CombineConditionFingerprints(session.Machine.ReadScriptAnalysisFingerprint(0, 0, reads), $"excavation-harp:{triangle}:{input.Identity}");
        targets.Add(new(ExcavationDigEventPrefix + triangle, 0, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, condition, false, (face.A + face.B + face.C) / 3, Triangle: triangle, DestinationField: 617, MayChangeNavigation: true, ChangesField: true)
        {
            TriangleOnly = true,
            SpatialInputGuards = [new(input, true)],
            SpatialSourceProgramCounter = 0x06CD,
            SpatialOwner = new(0x06CD, 2, 3, 6, 3, 7),
            SpatialDestinations = [617],
            PlanningDestinationFields = [617]
        });
    }
}
