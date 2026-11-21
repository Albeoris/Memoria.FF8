using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private bool IsExcavationPreferredEntry(AutoPlayVisit visit, AutoPlayEvent target)
    {
        if (session.World is not null || ordinaryObservation is not { } observation || visit.Key != observation.VisitId || !observation.Events.Any(current => current.Id == target.Id && current.ConditionFingerprint == target.ConditionSignature && current.AnalysisComplete))
        {
            return false;
        }
        if (Ff7AutoPlayScriptedInput.IsExcavationProgram(observation.ProgramIdentity))
        {
            return target.Id.StartsWith(Ff7AutoPlayDiscovery.ExcavationDigEventPrefix, StringComparison.Ordinal);
        }
        return observation.ProgramIdentity == Ff7AutoPlayDiscovery.ExcavationRewardProgram && target.Id == "actor:13:script:1" && session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xEA) == 1 && session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xEB) == 1 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE7) & 8) == 0;
    }
}
