namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticEffectInvocation(ushort RequestedEffect, ushort ExecutedEffect, byte ActorIndex, int HeaderIndex, byte? Parameter80, byte? Parameter81, byte? Parameter82);

public sealed partial class BattleSession
{
    private BattleDiagnosticEffectDispatch? diagnosticPacketReadDispatch;
    private NativeBattleOriginalEffectState? diagnosticPacketReadEffect;
    private int diagnosticPacketReadThread;

    private void PublishDiagnosticEnteredEffect(Action<BattleDiagnosticEffectDispatch> observer, BattleDiagnosticEffectDispatch dispatch, NativeBattleOriginalEffectState effect)
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
            ObserveDiagnosticInvocationDispatch(dispatch);
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
        if (!ReferenceEquals(dispatch, diagnosticPacketReadDispatch) || diagnosticPacketReadThread != Environment.CurrentManagedThreadId || dispatch.Serial <= 0 || dispatch.Serial != diagnosticDispatchSerial || NativeHostCallbacks != dispatch.Callback || diagnosticPacketReadEffect is not { Parameters: { } parameters } effect || effect.RequestedEffect != dispatch.RequestedEffect || effect.Binding.Effect != dispatch.ExecutedEffect || parameters.Actor78 != dispatch.NativeActorIndex || parameters.HeaderIndex7C != dispatch.NativeHeaderIndex || dispatch.NativeHeaderIndex is not int header || header is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || dispatch.NativeHeaderGeneration is not long generation || generation <= 0 || diagnosticHeaderGenerations is not { } generations || !generations.TryGetValue(header, out long actualGeneration) || actualGeneration != generation || nativeActionPackets.ActiveHeaderIndex != header)
        {
            throw new InvalidOperationException("Diagnostic invocation parameters require the actual same-thread entered-effect dispatch and header generation.");
        }
        return new(effect.RequestedEffect, effect.Binding.Effect, parameters.Actor78, parameters.HeaderIndex7C, parameters.Parameter80, parameters.Parameter81, parameters.Parameter82);
    }

    /// <summary>Detaches only the already-published physical action packet domain during its actual synchronous dispatch observation.</summary>
    public NativeBattleActionPacketsState ObserveDiagnosticActionPackets(BattleDiagnosticEffectDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        if (!ReferenceEquals(dispatch, diagnosticPacketReadDispatch) || diagnosticPacketReadThread != Environment.CurrentManagedThreadId || dispatch.Serial <= 0 || dispatch.Serial != diagnosticDispatchSerial || NativeHostCallbacks != dispatch.Callback || dispatch.NativeHeaderIndex is not int header || header is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || dispatch.NativeHeaderGeneration is not long generation || generation <= 0 || diagnosticHeaderGenerations is not { } generations || !generations.TryGetValue(header, out long actualGeneration) || actualGeneration != generation || nativeActionPackets.ActiveHeaderIndex != header)
        {
            throw new InvalidOperationException("Diagnostic action packets require the exact current synchronous dispatch/header generation owner.");
        }
        nativeActionPackets.Validate();
        NativeBattleActionHeaderSlot source = nativeActionPackets.Headers[header];
        ushort Word(int offset) => (ushort)(source.ReadByte(offset) | source.ReadByte(offset + 1) << 8);
        if (dispatch.ActorId is null || !byId.TryGetValue(dispatch.ActorId, out BattleCombatant? actor) || actor.NativeIndex != source.ReadByte(0) || dispatch.NativeActorIndex != source.ReadByte(0) || dispatch.NativeCommand != source.ReadByte(1) || dispatch.ActionId != Word(4) || dispatch.RequestedEffect != Word(6) || dispatch.ExecutedEffect != Word(6) || NativeBattleActionGraphics.Normalize(nativeActionPackets, header).TargetMask != dispatch.NativeTargetMask)
        {
            throw new InvalidDataException("Diagnostic action packet payload differs from its current dispatch source fields.");
        }
        return nativeActionPackets.DetachDiagnosticCopy();
    }

    /// <summary>Observes the original Duel timeout control's zero-magic header while its retained274 entry executes selector8.</summary>
    public NativeBattleActionPacketsState ObserveDiagnosticDuelClosingPackets(BattleDiagnosticEffectDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        if (!ReferenceEquals(dispatch, diagnosticPacketReadDispatch) || diagnosticPacketReadThread != Environment.CurrentManagedThreadId || dispatch.Serial <= 0 || dispatch.Serial != diagnosticDispatchSerial || NativeHostCallbacks != dispatch.Callback || dispatch.NativeHeaderIndex is not int header || header is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || dispatch.NativeHeaderGeneration is not long generation || generation <= 0 || diagnosticHeaderGenerations is not { } generations || !generations.TryGetValue(header, out long actualGeneration) || actualGeneration != generation || nativeActionPackets.ActiveHeaderIndex != header || dispatch.ActorId is null || !byId.TryGetValue(dispatch.ActorId, out BattleCombatant? actor) || actor.ModelId != 1 || actor.NativeLimit is not { CharacterId: 1, Command: 11 } || nativeDuelProcesses.Count != 1 || !nativeDuelProcesses.TryGetValue(actor.Id, out BattleNativeDuelState? duel) || duel is not { Phase: BattleNativeDuelPhase.Finisher, Selection1D28DEF: 254, RemainingTimer: <= 0 } || dispatch.NativeCommand != 241 || dispatch.ActionId != 65532 || dispatch.RequestedEffect != 274 || dispatch.ExecutedEffect != 274 || diagnosticPacketReadEffect is not { RequestedEffect: 274, Binding.Effect: 274, Parameters: { Parameter80: 8, Parameter82: 0 } parameters } || parameters.Actor78 != actor.NativeIndex || parameters.HeaderIndex7C != header || NativeEffectMemory?.Entry21DFEC4?.Effect != 274)
        {
            throw new InvalidDataException("Duel closing packets require the actual synchronous timed-out native241/FFFC retained274 control owner. " + DescribeDiagnosticDuelClosingGuard(dispatch));
        }
        nativeActionPackets.Validate();
        NativeBattleActionHeaderSlot source = nativeActionPackets.Headers[header];
        ushort Word(int offset) => (ushort)(source.ReadByte(offset) | source.ReadByte(offset + 1) << 8);
        if (source.ReadByte(0) != actor.NativeIndex || dispatch.NativeActorIndex != actor.NativeIndex || source.ReadByte(1) != 241 || Word(4) != 65532 || Word(6) != 0 || NativeBattleActionGraphics.Normalize(nativeActionPackets, header).TargetMask != dispatch.NativeTargetMask)
        {
            throw new InvalidDataException("Duel timeout control changed its actual actor, zero-magic header or native target normalization.");
        }
        return nativeActionPackets.DetachDiagnosticCopy();
    }

    private string DescribeDiagnosticDuelClosingGuard(BattleDiagnosticEffectDispatch dispatch)
    {
        BattleCombatant? actor = dispatch.ActorId is { } id && byId.TryGetValue(id, out BattleCombatant? found) ? found : null;
        BattleNativeDuelState? duel = actor is not null && nativeDuelProcesses.TryGetValue(actor.Id, out BattleNativeDuelState? state) ? state : null;
        long? generation = dispatch.NativeHeaderIndex is int selected && diagnosticHeaderGenerations is { } generations && generations.TryGetValue(selected, out long actual) ? actual : null;
        NativeBattleActionHeaderSlot? header = dispatch.NativeHeaderIndex is int index && index >= 0 && index < nativeActionPackets.Headers.Length ? nativeActionPackets.Headers[index] : null;
        byte? HeaderByte(int offset) => header is not null && !header.Payload.IsDefault && header.Payload.Length > offset && (header.UnknownByteMask & (1u << offset)) == 0 ? header.Payload[offset] : null;
        int? HeaderWord(int offset) => HeaderByte(offset) is byte low && HeaderByte(offset + 1) is byte high ? low | high << 8 : null;
        NativeBattleOriginalEffectState? effect = NativeGraphics?.OriginalEffect;
        object observation = new
        {
            SameDispatch = ReferenceEquals(dispatch, diagnosticPacketReadDispatch),
            SameThread = diagnosticPacketReadThread == Environment.CurrentManagedThreadId,
            CurrentSerial = diagnosticDispatchSerial,
            dispatch.Serial,
            Callback = NativeHostCallbacks,
            DispatchCallback = dispatch.Callback,
            dispatch.NativeCommand,
            dispatch.ActionId,
            dispatch.RequestedEffect,
            dispatch.ExecutedEffect,
            dispatch.NativeHeaderIndex,
            dispatch.NativeHeaderGeneration,
            ActualGeneration = generation,
            nativeActionPackets.ActiveHeaderIndex,
            ActorModel = actor?.ModelId,
            ActorIndex = actor?.NativeIndex,
            LimitCharacter = actor?.NativeLimit?.CharacterId,
            LimitCommand = actor?.NativeLimit?.Command,
            DuelOwners = nativeDuelProcesses.Count,
            Duel = duel is null ? null : new { Phase = duel.Phase.ToString(), duel.Selection1D28DEF, duel.RemainingTimer, duel.InitialTimer, duel.TargetMask },
            PublishedEffect = effect is null ? null : new { effect.RequestedEffect, effect.Binding.Effect, Actor = effect.Parameters?.Actor78, Header = effect.Parameters?.HeaderIndex7C, Selector80 = effect.Parameters?.Parameter80, Ready81 = effect.Parameters?.Parameter81, Variant82 = effect.Parameters?.Parameter82 },
            EnteredSelector80 = diagnosticPacketReadEffect?.Parameters?.Parameter80,
            EnteredVariant82 = diagnosticPacketReadEffect?.Parameters?.Parameter82,
            MemoryParameter80 = NativeEffectMemory?.Parameter80,
            MemoryParameter82 = NativeEffectMemory?.Parameter82,
            RetainedEntry = NativeEffectMemory?.Entry21DFEC4 is { } retained ? new { retained.Effect, retained.Entry, retained.Initializer } : null,
            Header = header is null ? null : new { header.UnknownByteMask, Actor = HeaderByte(0), Command = HeaderByte(1), Action = HeaderWord(4), Magic = HeaderWord(6) }
        };
        return System.Text.Json.JsonSerializer.Serialize(observation);
    }
}
