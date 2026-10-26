using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class DiagnosticBattleScanInput
{
    private long? sampledCallback;
    private ushort mappedButtons;

    internal ushort Advance(NativeBattleOriginalEffectState? effect, bool enabled, long callback)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(callback);
        if (!enabled || effect is not { RequestedEffect: 40, Binding.Effect: 40, Binding.Entry: 0x84D030, ReturnedDescriptor: 0x269ABA8 })
        {
            sampledCallback = null;
            mappedButtons = 0;
            return 0;
        }
        if (sampledCallback == callback)
        {
            return mappedButtons;
        }
        sampledCallback = callback;
        if (mappedButtons != 0)
        {
            mappedButtons = 0;
            return mappedButtons;
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            for (int index = 0; index < pool.Slots.Length; index++)
            {
                NativeBattleModulePoolTask viewer = pool.Slots[index];
                if ((viewer.Flags & 1) == 0 || viewer.Callback != 0x84F860)
                {
                    continue;
                }
                int phase = unchecked((sbyte)viewer.ReadByte(0x29));
                uint address = checked(pool.BankAddress + (uint)(index * pool.Stride));
                bool ready = phase == 2 || phase == 1 && effect.Pools.SelectMany(candidate => candidate.Slots).Any(result => (result.Flags & 1) != 0 && result.Callback == 0x84FAF0 && result.ReadByte(0x29) == 2 && result.ReadDword(0x18) == address);
                if (ready)
                {
                    mappedButtons = 0x40;
                    return mappedButtons;
                }
            }
        }
        return 0;
    }
}

internal sealed partial class RecreateGame
{
    private readonly DiagnosticBattleScanInput diagnosticScanInput = new();

    private ushort ReadDiagnosticScanMappedButtons(BattleSession active, ushort actualButtons)
    {
        bool enabled = ReferenceEquals(active, diagnosticDriverBattle) && diagnosticDriver is { Finished: false, Stage: DiagnosticDriverStage.Drain } && diagnosticDriverRecipe?.ExpectedEffect == 40 && active.NativeEffectMemory?.Scan is { InputInitialized: true, Text.Initialized: true };
        long callback = enabled ? active.NativeHostCallbacks ?? throw new NotSupportedException("Diagnostic Scan input requires its current native callback publication.") : 0;
        return (ushort)(actualButtons | diagnosticScanInput.Advance(active.NativeGraphics?.OriginalEffect, enabled, callback));
    }
}
