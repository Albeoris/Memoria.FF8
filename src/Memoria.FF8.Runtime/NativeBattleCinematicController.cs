namespace Memoria.FF8.Runtime;

/// <summary>The selected single-task cinematic callback publishes only its own retained filter and shared live registers.</summary>
public static class NativeBattleCinematicController
{
    public static NativeBattleCinematicFrame Advance(NativeBattleCinematicProfile profile, NativeBattleCinematicFrame frame, NativeBattleScriptedCallbackInput input, NativeBattleScriptedCallbackServices services, uint temporaryFrameAddress, Func<NativeBattleEffectMemory> readCurrentMemory, int instructionBudget = 4096, int traversalBudget = 256)
    {
        NativeBattleCinematicEntry.RequireBinding(profile, frame.Effect);
        ArgumentNullException.ThrowIfNull(readCurrentMemory);
        if (frame.Effect.Pools.Length != 1 || frame.Effect.Pools[0] is not { Stride: 0x10 } pool || pool.DescriptorAddress != profile.QueueAddress || pool.BankAddress != profile.BankAddress || pool.Slots.Length != 1)
        {
            throw new InvalidDataException("Native cinematic callback requires its selected single physical task pool.");
        }
        NativeBattleEffectTaskStep advanced = pool.Advance((current, index) =>
        {
            if (current.Slots[index].Callback != profile.Callback)
            {
                throw new NotSupportedException("Native cinematic task has an unsupported callback replacement.");
            }
            NativeBattleCinematicState retained = frame.Memory.Cinematic ?? throw new NotSupportedException("Native cinematic callback filter publication is missing; warm state is not reconstructed.");
            if (retained.Effect != profile.Effect)
            {
                throw new InvalidDataException("Native cinematic callback state belongs to a different selected effect.");
            }
            NativeBattleWaterTemporaryFrame temporary = NativeBattleWater222Controller.CreateTemporaryFrame(temporaryFrameAddress);
            NativeBattleScriptedGraphicsMemory graphics = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic callback requires its actual shared graphics registers.");
            NativeBattleWaterExecutionFrame execution = new(frame.Memory.ScriptedEffects ?? throw new NotSupportedException("Native cinematic callback requires its retained scripted-effect core."), frame.Memory.Arena ?? throw new NotSupportedException("Native cinematic callback requires its current arena."), temporary, frame.Random) { ModuleViewBackup = graphics.ModuleViewBackup };
            execution = execution.WriteTemporaryByte(0x145, retained.Filter145);
            NativeBattleWaterCallbackResult result = NativeBattleCinematicCallback.Run(execution, input, services, instructionBudget, traversalBudget);
            NativeBattleEffectMemory latest = readCurrentMemory() ?? throw new NotSupportedException("Native cinematic callback lost its current external memory owner.");
            NativeBattleScriptedGraphicsMemory latestGraphics = latest.ScriptedGraphics ?? throw new NotSupportedException("Native cinematic callback lost its current shared graphics registers.");
            NativeBattlePrimitiveProcess process = latest.PrimitiveProcess ?? throw new NotSupportedException("Native cinematic callback requires its actual COMMON packet process.");
            latest = latest with
            {
                ScriptedEffects = result.Frame.Core,
                Arena = result.Frame.Arena,
                ScriptedGraphics = latestGraphics with { ModuleViewBackup = result.Frame.ModuleViewBackup },
                PrimitiveProcess = process with { ByteCursor = checked((int)result.CommonCursor) },
                Cinematic = new(profile.Effect, result.Frame.ReadTemporaryByte(0x145))
            };
            frame = frame with { Memory = latest, Random = result.Frame.Random };
            return new(current, checked((int)result.TaskResult));
        });
        return frame with { Effect = frame.Effect with { Pools = [advanced.Pool], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : profile.QueueAddress } };
    }
}
