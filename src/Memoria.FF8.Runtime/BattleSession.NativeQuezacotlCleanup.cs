using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState PrepareNativeQuezacotlCleanup355(NativeBattleGraphicsState graphics)
    {
        NativeBattleQuezacotlWorkspaceWrite restored = NativeBattleQuezacotlCleanup.RestoreAndUnlink355(NativeEffectMemory ?? throw new NotSupportedException("Native355 cleanup requires its retained memory."), graphics.OriginalEffect ?? throw new InvalidDataException("Native355 cleanup lost its module."), current =>
        {
            NativeEffectMemory = current;
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(0x20DFAB8 + 0x2E50, 0x25217A8, WriteNativeAuxiliaryCompletionByte);
            graphics = graphics with { Auxiliary = auxiliary };
            NativeStage = NativeBattleQuezacotlWorkspace.SetStageVisibility6C6500(NativeStage ?? throw new NotSupportedException("Native355 cleanup requires its four retained stage groups."), 1);
            return NativeEffectMemory ?? throw new InvalidDataException("Native355 scheduling lost its completion memory.");
        });
        NativeEffectMemory = restored.Memory;
        return graphics with { OriginalEffect = restored.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeQuezacotlAfterChild6C3760(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native6C3760 wait lost its module.");
        graphics = FinishNativeQuezacotlSecondaryLists(graphics, NativeEffectMemory ?? throw new NotSupportedException("Native6C3760 wait requires its process memory."), effect, 355, new());
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained stage groups.");
        short intensity = checked((short)graphics.OriginalEffect!.Quezacotl!.Intensity17A4);
        NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = intensity, ColorFlags = 0 }).ToImmutableArray() };
        return graphics;
    }

    private NativeBattleGraphicsState FinishNativeQuezacotlSecondaryLists(NativeBattleGraphicsState graphics, NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, ushort phase, NativeBattleGteDepthTrace depthTrace)
    {
        NativeEffectMemory = memory;
        uint? pathOutlineDirection = null;
        if (phase >= 87)
        {
            (graphics, pathOutlineDirection) = AdvanceNativeQuezacotlRibbon(graphics with { OriginalEffect = effect }, depthTrace);
            memory = NativeEffectMemory!;
            effect = graphics.OriginalEffect!;
        }
        if (phase >= 88)
        {
            (graphics, uint? particleDirection) = AdvanceNativeQuezacotlParticles(graphics with { OriginalEffect = effect }, depthTrace);
            pathOutlineDirection = particleDirection ?? pathOutlineDirection;
            memory = NativeEffectMemory!;
            effect = graphics.OriginalEffect!;
        }
        if (phase >= 257)
        {
            graphics = AdvanceNativeQuezacotlPaths(graphics with { OriginalEffect = effect }, depthTrace, pathOutlineDirection);
            memory = NativeEffectMemory!;
            effect = graphics.OriginalEffect!;
        }
        if (phase >= 303)
        {
            graphics = AdvanceNativeQuezacotlFragments(graphics with { OriginalEffect = effect }, depthTrace);
            memory = NativeEffectMemory!;
            effect = graphics.OriginalEffect!;
        }
        effect = NativeBattleQuezacotlChild.FinishMain6C3760(memory, effect, phase >= 87, phase >= 88, phase >= 257, phase >= 303);
        NativeEffectMemory = memory;
        return graphics with { OriginalEffect = effect };
    }
}
