using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private NativeWorldSalaryState StageWorldEventBattle(WorldState accepted, NativeWorldSalaryState salary, ushort formation)
    {
        if (accepted.NativeFootRuntime is not { EntityType: 0 or 1 or 2 } || accepted.NativeScriptedRuntime is not null || accepted.NativeTrainRuntime is not null || formation >= 1024)
        {
            throw new NotSupportedException("The world event battle requires an original formation and the implemented mode 3 foot return.");
        }
        RandomEncounterSessionState encounters = salary.RandomEncounters ?? new(false, NativeRandomEncounterState.FromColdProcess(), null);
        if (encounters.Pending is not null)
        {
            throw new InvalidOperationException("A world event battle cannot replace a pending world encounter.");
        }
        // 53FAC0 routes the FF2B formation through the same mode 3 battle entry as other world encounters.
        return salary with { RandomEncounters = encounters with { Pending = new(SceneKind.WorldMap, session.State.FieldId, accepted.Frame, formation, AllocationProgramSha256) } };
    }

    private bool ReadWorldEventBattlePredicate(ushort opcode, ushort operand)
    {
        NativeLastBattle? battle = session.State.LastNativeBattle;
        return opcode switch
        {
            0xFF34 => battle?.Formation == operand,
            0xFF35 => operand == (battle?.Result == 4 ? 1 : 0),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode))
        };
    }
}
