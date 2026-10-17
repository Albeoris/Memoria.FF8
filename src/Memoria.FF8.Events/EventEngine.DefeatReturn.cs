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

    public void RequireNativeBattleAbandonment(NativeBattleAbandonment receipt)
    {
        receipt.Validate();
        if (receipt.Branch != ReadNativeDefeatReturnBranch4706B0() || receipt.ExitClockCalls != services.ExamTimer?.ClockCalls)
        {
            throw new InvalidDataException("Abandonment no longer matches the actual native defeat branch or process clock.");
        }
        _ = ReadLatestNativeBattleResult(Scene, abandonedReplacement: receipt);
    }

    public void CommitNativeBattleAbandonment(NativeBattleAbandonment receipt)
    {
        receipt.Validate();
        if (receipt.Branch != ReadNativeDefeatReturnBranch4706B0() || receipt.ExitClockCalls != services.ExamTimer?.ClockCalls)
        {
            throw new InvalidDataException("Abandonment no longer matches the actual native defeat branch or process clock.");
        }
        if (receipt.Encounter.ScriptedOwner is { } owner)
        {
            NativeBattleCompletion request = PendingInteractiveBattle ?? throw new InvalidOperationException("Abandonment requires its suspended BATTLE request.");
            if (request.FieldId != owner.FieldId || request.ProgramHash != owner.ProgramHash || request.ObjectId != owner.ObjectId || request.ScriptId != owner.ScriptId || request.Operation != owner.Operation || request.Frame != receipt.Encounter.SourceFrame || request.BattleClockCallsAtEntry != receipt.EntryClockCalls)
            {
                throw new InvalidDataException("Abandonment changed the issuing BATTLE operation.");
            }
            int index = Scene.NativeBattleCompletions!.IndexOf(request);
            var wait = receipt.ScriptWait ?? throw new InvalidDataException("Scripted abandonment requires its original typed wait.");
            Fiber fiber = fibers.Single(item => item.Sequence == wait.FiberSequence);
            if (Scene.NativeBattleWaits?.GetValueOrDefault(fiber.Sequence, -1) != index || fiber.Complete || fiber.WaitReason != WaitReason.MinimalBattle || fiber.Position != wait.NextOperation || fiber.WaitUntil != wait.WaitUntil)
            {
                throw new InvalidDataException("Abandoned BATTLE no longer owns its actual live typed wait.");
            }
            Scene.NativeBattleCompletions[index] = request with { Abandonment = receipt, EmulatedCombatCallbacks = receipt.CombatCallbacks };
        }
        else if (receipt.Encounter.SourceScene == SceneKind.Field)
        {
            NativeRandomFieldBattleOwner random = Scene.NativeRandomFieldBattle ?? throw new InvalidOperationException("Abandonment requires its suspended random encounter.");
            if (random.Abandonment is not null || random.Result is not null || random.BattleClockCallsAtEntry != receipt.EntryClockCalls || random.Callbacks != receipt.CombatCallbacks)
            {
                throw new InvalidDataException("Abandonment changed the random encounter callback owner.");
            }
            Scene.NativeRandomFieldBattle = random with { Abandonment = receipt };
        }
        Scene.NativeAbandonedBattleOutcome = receipt;
        Scene.NativeBattleResult = ReadLatestNativeBattleResult(Scene);
    }

    public NativeBattleAbandonment CaptureNativeBattleAbandonment(BattleEncounterOccurrence encounter, int callbacks)
    {
        NativeBattleDefeatReturnBranch branch = ReadNativeDefeatReturnBranch4706B0();
        long entry = PendingInteractiveBattle?.BattleClockCallsAtEntry ?? Scene.NativeRandomFieldBattle?.BattleClockCallsAtEntry ?? throw new NotSupportedException("Native fresh defeat requires its executed entry clock owner.");
        NativeBattleAbandonment receipt = new(encounter, branch, entry, services.ExamTimer?.ClockCalls ?? throw new NotSupportedException("Native fresh defeat requires its process timer."), callbacks);
        if (PendingInteractiveBattle is { } request)
        {
            int index = Scene.NativeBattleCompletions!.IndexOf(request);
            long sequence = Scene.NativeBattleWaits!.Single(item => item.Value == index).Key;
            Fiber fiber = fibers.Single(item => item.Sequence == sequence);
            if (fiber.Complete || fiber.WaitReason != WaitReason.MinimalBattle)
            {
                throw new InvalidDataException("Abandonment requires the original suspended BATTLE fiber.");
            }
            receipt = receipt with { ScriptWait = new(sequence, fiber.Position, fiber.WaitUntil) };
        }
        receipt.Validate();
        _ = ReadLatestNativeBattleResult(Scene, abandonedReplacement: receipt);
        return receipt;
    }
}
