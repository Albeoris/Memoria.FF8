using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeBattleDefeatReturnBranch ReadNativeDefeatReturnBranch4706B0()
    {
        ushort persistent = Scene.NativeBattleFlagWrites is { Count: > 0 } writes ? writes[^1].PersistentFlags : throw new NotSupportedException("Native4706B0 requires the actual retained saved WORD182 writer.");
        return new(persistent);
    }

    private bool SupportsNativeFieldReturnResult(byte result) => result is 2 or 4 || result == 1 && ReadNativeDefeatReturnBranch4706B0().ResumePreviousScene;

    public NativeBattleAbandonment CaptureNativeBattleAbandonment(BattleEncounterOccurrence encounter, int callbacks)
    {
        NativeBattleDefeatReturnBranch branch = ReadNativeDefeatReturnBranch4706B0();
        long entry = PendingInteractiveBattle?.BattleClockCallsAtEntry ?? Scene.NativeRandomFieldBattle?.BattleClockCallsAtEntry ?? throw new NotSupportedException("Native fresh defeat requires its executed entry clock owner.");
        NativeBattleAbandonment receipt = new(encounter, branch, entry, services.ExamTimer?.ClockCalls ?? throw new NotSupportedException("Native fresh defeat requires its process timer."), callbacks);
        receipt.Validate();
        return receipt;
    }
}
