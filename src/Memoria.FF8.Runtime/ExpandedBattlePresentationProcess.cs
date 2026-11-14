using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Owns the native presentation process for one expanded battle.</summary>
public sealed class ExpandedBattlePresentationProcess
{
    private readonly BattleSession presentation;

    public ExpandedBattlePresentationProcess(BattleSession logicalOwner, BattleDefinition presentationResources)
    {
        ArgumentNullException.ThrowIfNull(logicalOwner);
        ArgumentNullException.ThrowIfNull(presentationResources);
        presentation = BattleSession.CreateHostedBattlePresentation(logicalOwner, presentationResources);
    }

    public NativeBattleGraphicsState Graphics => presentation.NativeGraphics!;
    public NativeBattleEffectMemory Memory => presentation.NativeEffectMemory!;
    public NativeBattleCameraState Camera => presentation.NativeCamera!;
    public NativeBattleStageState Stage => presentation.NativeStage!;
    public NativeExamTimerState? Timer => presentation.NativeExamTimer;
    public bool EntryReady => Graphics.EntryReady;
    public NativeBattlePreparedPresentation? PreparedPresentation => presentation.PreparedPresentation;
    public NativeBattleEffectPresentationStep LastStep { get; private set; } = new([], []);

    internal ulong GlobalRandomCalls => presentation.NativeGlobalRandomChange!.Calls;
    internal NativeBattleModelCpuResources ModelCpuResources => presentation.NativeModelCpuResources!;
    internal NativeBattleModelTextureCache ModelTextureCache => presentation.NativeModelTextureCache!;
    internal long? ActiveActionToken => presentation.HostedActiveActionToken;
    internal long? LastStepActionToken { get; private set; }

    internal long BeginAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        return presentation.BeginHostedAction(header, contacts);
    }

    internal BattleSession.PreparedHostedAction PrepareAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        return presentation.PrepareHostedAction(header, contacts);
    }

    internal void ValidatePreparedAction(BattleSession.PreparedHostedAction prepared)
    {
        presentation.ValidatePreparedHostedAction(prepared);
    }

    internal long PublishAction(BattleSession.PreparedHostedAction prepared)
    {
        return presentation.PublishHostedAction(prepared);
    }

    internal bool IsActionComplete(long token)
    {
        return presentation.IsHostedActionComplete(token);
    }

    internal NativeBattleModelLifecycleServices Advance(BattleSession logicalOwner, NativeExamTimerState? timerObservation = null)
    {
        ArgumentNullException.ThrowIfNull(logicalOwner);
        long? actionToken = ActiveActionToken;
        NativeBattleModelLifecycleServices services = presentation.AdvanceHostedBattlePresentationFrame(logicalOwner, timerObservation ?? logicalOwner.NativeExamTimer);
        presentation.RefreshHostedPreparedPresentation(logicalOwner, services);
        LastStep = presentation.ReadAndClearHostedPresentationFrameOutputs();
        LastStepActionToken = actionToken;
        return services;
    }
}
