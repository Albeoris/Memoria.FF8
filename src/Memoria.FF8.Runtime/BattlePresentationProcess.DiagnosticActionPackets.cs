using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private BattleDiagnosticEffectDispatch? diagnosticPacketReadDispatch;
    private NativeBattleOriginalEffectState? diagnosticPacketReadEffect;
    private int diagnosticPacketReadThread;

    private static int? OriginalDiagnosticHeaderIndex(BattleActionHeaderReference? header)
    {
        return header is { } reference && reference.Ordinal < NativeBattleRetainedActionPackets.HeaderCapacity ? reference.Ordinal : null;
    }

    private void ObserveDiagnosticCommittedHostedHeaders(ImmutableArray<BattleActionHeaderReference> headers)
    {
        if (services?.ObservesActionHeaders() != true)
        {
            return;
        }
        foreach (BattleActionHeaderReference header in headers)
        {
            ObserveDiagnosticPreparedHeader(header);
        }
    }

    private static byte? OriginalDiagnosticSource(NativeBattleEffectParameters? parameters)
    {
        return parameters?.OriginalWindow is { } window && (window.KnownMask & 1) != 0 ? window.ReadByte(0) : null;
    }

    private static ushort? OriginalDiagnosticTargets(NativeBattleEffectParameters? parameters)
    {
        return parameters?.OriginalWindow is { } window && (window.KnownMask & 0x0C) == 0x0C
            ? (ushort)(window.ReadByte(2) | window.ReadByte(3) << 8)
            : null;
    }

    internal void PublishDiagnosticEnteredEffect(Action<BattleDiagnosticEffectDispatch> observer, BattleDiagnosticEffectDispatch dispatch, NativeBattleOriginalEffectState effect)
    {
        NativeBattleOriginalEffectState? previous = diagnosticPacketReadEffect;
        diagnosticPacketReadEffect = effect;
        try
        {
            PublishDiagnosticEffectDispatch(observer, dispatch);
        }
        finally
        {
            diagnosticPacketReadEffect = previous;
        }
    }

    private void PublishDiagnosticEffectDispatch(Action<BattleDiagnosticEffectDispatch> observer, BattleDiagnosticEffectDispatch dispatch)
    {
        BattleDiagnosticEffectDispatch? previous = diagnosticPacketReadDispatch;
        int previousThread = diagnosticPacketReadThread;
        diagnosticPacketReadDispatch = dispatch;
        diagnosticPacketReadThread = Environment.CurrentManagedThreadId;
        try
        {
            observer(dispatch);
        }
        finally
        {
            diagnosticPacketReadDispatch = previous;
            diagnosticPacketReadThread = previousThread;
        }
    }

    /// <summary>Copies scalar parameters from the actual entered effect during its synchronous observation, before normal publication.</summary>
    public BattleDiagnosticEffectInvocation ObserveDiagnosticEffectInvocation(BattleDiagnosticEffectDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        _ = RequireDiagnosticActionPacketOwner(dispatch);
        if (diagnosticPacketReadEffect is not { Parameters: { } parameters } effect)
        {
            throw new InvalidOperationException("Diagnostic invocation requires the entered effect and its current header.");
        }
        return new(effect.RequestedEffect, effect.Binding.Effect, OriginalDiagnosticSource(parameters), dispatch.NativeHeaderIndex, parameters.Parameter80, parameters.Parameter81, parameters.Parameter82)
        {
            ActorId = dispatch.ActorId,
            ActorSlot = dispatch.ActorSlot,
            HeaderReference = parameters.Header
        };
    }

    /// <summary>Detaches only the already-published physical action packet domain during its actual synchronous dispatch observation.</summary>
    public NativeBattleRetainedActionPackets ObserveDiagnosticActionPackets(BattleDiagnosticEffectDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        _ = RequireDiagnosticActionPacketOwner(dispatch);
        if (dispatch.NativeHeaderIndex is null)
        {
            throw new InvalidOperationException("Original packet inspection requires the current header within the retained native rows.");
        }
        nativeActionPackets.Validate();
        return nativeActionPackets.ExportRetained().DetachDiagnosticCopy();
    }

    public BattlePartyActorReference ObserveDiagnosticCounterRecipient(BattleDiagnosticEffectDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        _ = RequireDiagnosticActionPacketOwner(dispatch);
        if (diagnosticPacketReadEffect?.Parameters?.TargetActors is not { Count: 1 } targets || targets.Enumerate().Single() is not BattlePartyActorModel party)
        {
            throw new InvalidDataException("Counter retaliation requires its actual single party recipient.");
        }
        return party.Actor;
    }

    internal (long Token, NativeBattleActionHeaderSlot Header, ImmutableArray<BattleModelOwner> Recipients) ReadDiagnosticTimedInputPackets(BattleDiagnosticEffectDispatch dispatch)
    {
        NativeBattleActionHeaderSlot header = RequireDiagnosticActionPacketOwner(dispatch);
        if (hostedPresentationInvocation is not { } invocation || invocation.Header != dispatch.HeaderReference)
        {
            throw new InvalidOperationException("Timed input diagnostics require the current presentation token and its committed header.");
        }
        ImmutableArray<BattleModelOwner> recipients = nativeActionPackets.ReadHeaderContacts(invocation.Header).Select(contact => contact.Primary.Recipient).ToImmutableArray();
        return (invocation.Token, header, recipients);
    }

    private NativeBattleActionHeaderSlot RequireDiagnosticActionPacketOwner(BattleDiagnosticEffectDispatch dispatch)
    {
        if (!ReferenceEquals(dispatch, diagnosticPacketReadDispatch) || diagnosticPacketReadThread != Environment.CurrentManagedThreadId ||
            dispatch.Serial <= 0 || dispatch.HeaderReference is not { } reference ||
            dispatch.NativeHeaderGeneration is not long generation || generation <= 0 || diagnosticHeaderGenerations is not { } generations ||
            !generations.TryGetValue(reference.Ordinal, out long actualGeneration) || actualGeneration != generation ||
            nativeActionPackets.ActiveHeader != reference)
        {
            throw new InvalidOperationException("Diagnostic action packets require the same-thread entered effect and its actual header generation.");
        }
        NativeBattleActionHeaderSlot source = nativeActionPackets.ReadHeader(reference);
        if (diagnosticPacketReadEffect is not { Parameters: { } parameters } effect ||
            dispatch.ActorId != ReadGraphicsPresentationId(source.ReflectedCaster ?? source.Source) ||
            parameters.Header != reference || OriginalDiagnosticSource(parameters) != dispatch.NativeActorIndex ||
            dispatch.NativeCommand != source.Command || dispatch.ActionId != source.Action ||
            dispatch.RequestedEffect != effect.RequestedEffect || dispatch.ExecutedEffect != effect.Binding.Effect ||
            OriginalDiagnosticTargets(parameters) != dispatch.NativeTargetMask)
        {
            throw new InvalidDataException("Diagnostic action packet payload differs from its entered effect.");
        }
        return source;
    }

    internal NativeBattleRetainedActionPackets ObserveDiagnosticDuelClosingPackets(BattleDiagnosticEffectDispatch dispatch, ushort selector)
    {
        NativeBattleActionHeaderSlot source = RequireDiagnosticActionPacketOwner(dispatch);
        if (dispatch.NativeCommand != 241 || dispatch.ActionId is not (65530 or 65532) || source.Effect != selector ||
            dispatch.RequestedEffect != 274 || dispatch.ExecutedEffect != 274 ||
            diagnosticPacketReadEffect is not { Parameters: { Parameter80: 8 } parameters } ||
            parameters.Parameter82 != (dispatch.ActionId == 65530 ? selector - 5 : 0) || NativeEffectMemory?.Entry21DFEC4?.Effect != 274)
        {
            throw new InvalidDataException("Duel closing packets require the native 241 retained-effect control owner.");
        }
        return nativeActionPackets.ExportRetained().DetachDiagnosticCopy();
    }
}
