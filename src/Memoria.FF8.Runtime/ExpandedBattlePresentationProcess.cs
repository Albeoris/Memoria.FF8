using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Owns the native presentation process for one expanded battle.</summary>
public sealed class ExpandedBattlePresentationProcess
{
    private readonly BattleSession logicalOwner;
    private readonly BattleSession presentation;
    private bool advancing;

    public ExpandedBattlePresentationProcess(BattleSession logicalOwner, BattleDefinition presentationResources)
    {
        ArgumentNullException.ThrowIfNull(logicalOwner);
        ArgumentNullException.ThrowIfNull(presentationResources);
        this.logicalOwner = logicalOwner;
        presentation = BattleSession.CreateHostedBattlePresentation(logicalOwner, presentationResources);
        presentation.ValidateHostedPresentationRandomOwner(logicalOwner);
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
    internal NativeGlobalRandomChange? GlobalRandomChange => presentation.NativeGlobalRandomChange;
    internal NativeCrtRandomState? CurrentCrtRandom => presentation.CurrentCrtRandom;
    internal NativeBattleModelCpuResources ModelCpuResources => presentation.NativeModelCpuResources!;
    internal NativeBattleModelTextureCache ModelTextureCache => presentation.NativeModelTextureCache!;
    internal long? ActiveActionToken => presentation.HostedActiveActionToken;
    internal long? LastStepActionToken { get; private set; }
    internal NativeBattleModelLifecycleServices? LastFrameServices { get; private set; }

    internal void RequireLogicalOwner(BattleSession owner)
    {
        if (!ReferenceEquals(logicalOwner, owner))
        {
            throw new InvalidOperationException("Hosted presentation requires its original logical battle owner.");
        }
    }

    internal long AccumulateElapsed(TimeSpan elapsed)
    {
        RequireIdle();
        return presentation.AccumulateHostedPresentationElapsed(elapsed);
    }

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

    internal NativeBattleModelLifecycleServices Advance(BattleSession logicalOwner, NativeExamTimerState? timerObservation = null, Action? beforeCore = null)
    {
        ArgumentNullException.ThrowIfNull(logicalOwner);
        RequireLogicalOwner(logicalOwner);
        RequireIdle();

        advancing = true;
        try
        {
            presentation.ImportHostedPresentationRandom(logicalOwner);
            try
            {
                long? actionToken = ActiveActionToken;
                NativeBattleModelLifecycleServices services = presentation.AdvanceHostedBattlePresentationFrame(logicalOwner, timerObservation ?? logicalOwner.NativeExamTimer, beforeCore is null ? null : DispatchBeforeCore);
                presentation.RefreshHostedPreparedPresentation(logicalOwner, services);
                LastStep = presentation.ReadAndClearHostedPresentationFrameOutputs();
                LastStepActionToken = actionToken;
                LastFrameServices = services;
                return services;

                void DispatchBeforeCore()
                {
                    presentation.ExportHostedPresentationRandom(logicalOwner);
                    try
                    {
                        beforeCore();
                        actionToken = ActiveActionToken;
                    }
                    finally
                    {
                        presentation.CopyHostedPresentationRandomFrom(logicalOwner);
                    }
                }
            }
            finally
            {
                presentation.ExportHostedPresentationRandom(logicalOwner);
            }
        }
        finally
        {
            advancing = false;
        }
    }

    private void RequireIdle()
    {
        if (advancing)
        {
            throw new InvalidOperationException("A hosted presentation frame is already advancing.");
        }
    }
}
