namespace Memoria.FF8.Runtime;

/// <summary>The retained single-task bridge for the selected Ifrit cinematic callback.</summary>
public static class NativeBattleIfrit201Controller
{
    public const uint MainDescriptor = 0x2796E18;
    public const uint MainBank = 0x2796E28;
    public const uint Callback = 0xB25DF0;

    public static NativeBattleIfrit201Frame AdvanceB25DF0(NativeBattleIfrit201Frame frame, NativeBattleScriptedCallbackInput input, NativeBattleScriptedCallbackServices services, uint temporaryFrameAddress, Func<NativeBattleEffectMemory> readCurrentMemory, int instructionBudget = 4096, int traversalBudget = 256)
    {
        RequireBinding(frame.Effect);
        ArgumentNullException.ThrowIfNull(readCurrentMemory);
        if (frame.Effect.Pools.Length != 1 || frame.Effect.Pools[0] is not { DescriptorAddress: MainDescriptor, BankAddress: MainBank, Stride: 0x10 } pool || pool.Slots.Length != 1)
        {
            throw new InvalidDataException("Native Ifrit callback requires its actual single physical task pool.");
        }
        NativeBattleEffectTaskStep advanced = pool.Advance((current, index) =>
        {
            if (current.Slots[index].Callback != Callback)
            {
                throw new NotSupportedException("Native Ifrit task has an unsupported callback replacement.");
            }
            NativeBattleIfrit201State retained = frame.Memory.Ifrit201 ?? throw new NotSupportedException("Native Ifrit callback filter publication is missing; warm state is not reconstructed.");
            NativeBattleWaterTemporaryFrame temporary = NativeBattleWater222Controller.CreateTemporaryFrame(temporaryFrameAddress);
            NativeBattleScriptedGraphicsMemory graphics = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Ifrit callback requires its actual shared graphics registers.");
            NativeBattleWaterExecutionFrame execution = new(frame.Memory.ScriptedEffects ?? throw new NotSupportedException("Native Ifrit callback requires its retained scripted-effect core."), frame.Memory.Arena ?? throw new NotSupportedException("Native Ifrit callback requires its current arena."), temporary, frame.Random) { ModuleViewBackup = graphics.ModuleViewBackup };
            execution = execution.WriteTemporaryByte(0x145, retained.Filter145);
            NativeBattleWaterCallbackResult result = NativeBattleIfritCallback.RunB25DF0(execution, input, services, instructionBudget, traversalBudget);
            NativeBattleEffectMemory latest = readCurrentMemory() ?? throw new NotSupportedException("Native Ifrit callback lost its current external memory owner.");
            NativeBattleScriptedGraphicsMemory latestGraphics = latest.ScriptedGraphics ?? throw new NotSupportedException("Native Ifrit callback lost its current shared graphics registers.");
            NativeBattlePrimitiveProcess process = latest.PrimitiveProcess ?? throw new NotSupportedException("Native Ifrit callback requires its actual COMMON packet process.");
            latest = latest with
            {
                ScriptedEffects = result.Frame.Core,
                Arena = result.Frame.Arena,
                ScriptedGraphics = latestGraphics with { ModuleViewBackup = result.Frame.ModuleViewBackup },
                PrimitiveProcess = process with { ByteCursor = checked((int)result.CommonCursor) },
                Ifrit201 = new(result.Frame.ReadTemporaryByte(0x145))
            };
            frame = frame with { Memory = latest, Random = result.Frame.Random };
            return new(current, checked((int)result.TaskResult));
        });
        return frame with { Effect = frame.Effect with { Pools = [advanced.Pool], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : MainDescriptor } };
    }

    private static void RequireBinding(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding is not { Effect: 201, Entry: 0xB25780, Initializer: 0xB25750 })
        {
            throw new InvalidDataException("Native Ifrit controller requires its actual selected entry and initializer.");
        }
    }
}
