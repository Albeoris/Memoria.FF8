using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The single physical task bridge around the finite scripted-effect initializer and callback.</summary>
public static class NativeBattleWater222Controller
{
    public const uint MainDescriptor = 0x2796BA8;
    public const uint MainBank = 0x2796BB8;
    public const uint Callback = 0xA9B870;

    public static NativeBattleWater222Frame InitializeA9B200(NativeBattleWater222Frame frame, NativeBattleWater222Resources resources, NativeBattleWaterInitializationInputs inputs)
    {
        RequireBinding(frame.Effect);
        NativeBattleWaterInitializationResult initialized = NativeBattleWaterInitialization.InitializeA9B200(frame.Memory.ScriptedEffects ?? throw new NotSupportedException("Native Water entry requires its current scripted-effect core."), frame.Memory.Arena ?? throw new NotSupportedException("Native Water entry requires its actual initialized resource arena."), resources.Profile, inputs);
        NativeBattleWaterInitialPoolRequest request = initialized.PoolRequest;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(request.DescriptorAddress, request.BankAddress, checked((ushort)request.Stride), checked((ushort)request.Capacity));
        (pool, _) = pool.Append(request.Callback);
        NativeBattleWaterExecutionFrame execution = new(initialized.Core, initialized.Arena, initialized.TemporaryFrame, frame.Random);
        NativeBattleEffectMemory memory = frame.Memory with { ScriptedEffects = initialized.Core, Arena = initialized.Arena, Water222 = new(execution.ReadTemporaryByte(0x145)) };
        return new(memory, frame.Effect with { Pools = [pool], ReturnedDescriptor = request.DescriptorAddress }, frame.Random);
    }

    public static NativeBattleWater222Frame AdvanceA9B870(NativeBattleWater222Frame frame, NativeBattleWaterCallbackInput input, NativeBattleWaterCallbackServices services, uint temporaryFrameAddress, Func<NativeBattleEffectMemory> readCurrentMemory, int instructionBudget = 4096, int traversalBudget = 256)
    {
        RequireBinding(frame.Effect);
        ArgumentNullException.ThrowIfNull(readCurrentMemory);
        if (frame.Effect.Pools.Length != 1 || frame.Effect.Pools[0] is not { DescriptorAddress: MainDescriptor, BankAddress: MainBank, Stride: 0x10 } pool || pool.Slots.Length != 1)
        {
            throw new InvalidDataException("Native Water callback requires its actual single physical task pool.");
        }
        NativeBattleEffectTaskStep advanced = pool.Advance((current, index) =>
        {
            if (current.Slots[index].Callback != Callback)
            {
                throw new NotSupportedException("Native Water task has an unsupported callback replacement.");
            }
            NativeBattleWater222State retained = frame.Memory.Water222 ?? throw new NotSupportedException("Native Water callback filter publication is missing; warm state is not reconstructed.");
            NativeBattleWaterTemporaryFrame temporary = CreateTemporaryFrame(temporaryFrameAddress);
            NativeBattleScriptedGraphicsMemory graphics = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Water callback requires its actual shared graphics registers.");
            NativeBattleWaterExecutionFrame execution = new(frame.Memory.ScriptedEffects ?? throw new NotSupportedException("Native Water callback requires its retained scripted-effect core."), frame.Memory.Arena ?? throw new NotSupportedException("Native Water callback requires its current arena."), temporary, frame.Random) { ModuleViewBackup = graphics.ModuleViewBackup };
            execution = execution.WriteTemporaryByte(0x145, retained.Filter145);
            NativeBattleWaterCallbackResult result = NativeBattleWaterCallback.RunA9B870(execution, input, services, instructionBudget, traversalBudget);
            NativeBattleEffectMemory latest = readCurrentMemory() ?? throw new NotSupportedException("Native Water callback lost its current external memory owner.");
            NativeBattleScriptedGraphicsMemory latestGraphics = latest.ScriptedGraphics ?? throw new NotSupportedException("Native Water callback lost its current shared graphics registers.");
            NativeBattlePrimitiveProcess process = latest.PrimitiveProcess ?? throw new NotSupportedException("Native Water callback requires its actual COMMON packet process.");
            latest = latest with
            {
                ScriptedEffects = result.Frame.Core,
                Arena = result.Frame.Arena,
                ScriptedGraphics = latestGraphics with { ModuleViewBackup = result.Frame.ModuleViewBackup },
                PrimitiveProcess = process with { ByteCursor = checked((int)result.CommonCursor) },
                Water222 = new(result.Frame.ReadTemporaryByte(0x145))
            };
            frame = frame with { Memory = latest, Random = result.Frame.Random };
            return new(current, checked((int)result.TaskResult));
        });
        return frame with { Effect = frame.Effect with { Pools = [advanced.Pool], ReturnedDescriptor = advanced.Survivors == 0 ? 0u : MainDescriptor } };
    }

    public static NativeBattleWaterTemporaryFrame CreateTemporaryFrame(uint address)
    {
        if (address == 0 || address > uint.MaxValue - 0x180)
        {
            throw new InvalidDataException("Native Water callback requires its actual bounded transient frame address.");
        }
        return new(address, new byte[0x180].ToImmutableArray(), new bool[0x180].ToImmutableArray());
    }

    private static void RequireBinding(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding is not { Effect: 222, Entry: 0xA9B200, Initializer: 0xA9B1D0 })
        {
            throw new InvalidDataException("Native Water controller requires its actual selected entry and initializer.");
        }
    }
}
