namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticEffectInvocation(ushort RequestedEffect, ushort ExecutedEffect, byte ActorIndex, int HeaderIndex, byte? Parameter80, byte? Parameter81, byte? Parameter82);

public sealed partial class BattleSession
{
    private BattleDiagnosticEffectDispatch? diagnosticPacketReadDispatch;
    private NativeBattleOriginalEffectState? diagnosticPacketReadEffect;
    private int diagnosticPacketReadThread;

    private static int? OriginalDiagnosticHeaderIndex(BattleActionHeaderReference? header)
    {
        return header is { } reference && reference.Ordinal < NativeBattleRetainedActionPackets.HeaderCapacity ? reference.Ordinal : null;
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
        if (!ReferenceEquals(dispatch, diagnosticPacketReadDispatch) || diagnosticPacketReadThread != Environment.CurrentManagedThreadId || dispatch.Serial <= 0 || dispatch.Serial != diagnosticDispatchSerial || NativeHostCallbacks != dispatch.Callback || diagnosticPacketReadEffect is not { Parameters: { } parameters } effect || effect.RequestedEffect != dispatch.RequestedEffect || effect.Binding.Effect != dispatch.ExecutedEffect || OriginalDiagnosticSource(parameters) != dispatch.NativeActorIndex || OriginalDiagnosticHeaderIndex(parameters.Header) != dispatch.NativeHeaderIndex || dispatch.NativeHeaderIndex is not int header || header is < 0 or >= NativeBattleRetainedActionPackets.HeaderCapacity || dispatch.NativeHeaderGeneration is not long generation || generation <= 0 || diagnosticHeaderGenerations is not { } generations || !generations.TryGetValue(header, out long actualGeneration) || actualGeneration != generation || nativeActionPackets.ActiveHeader != new BattleActionHeaderReference(header))
        {
            throw new InvalidOperationException("Diagnostic invocation parameters require the actual same-thread entered-effect dispatch and header generation.");
        }
        return new(effect.RequestedEffect, effect.Binding.Effect, OriginalDiagnosticSource(parameters) ?? throw new NotSupportedException("Diagnostic invocation requires an original source alias."), header, parameters.Parameter80, parameters.Parameter81, parameters.Parameter82);
    }

    /// <summary>Detaches only the already-published physical action packet domain during its actual synchronous dispatch observation.</summary>
    public NativeBattleRetainedActionPackets ObserveDiagnosticActionPackets(BattleDiagnosticEffectDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        if (!ReferenceEquals(dispatch, diagnosticPacketReadDispatch) || diagnosticPacketReadThread != Environment.CurrentManagedThreadId || dispatch.Serial <= 0 || dispatch.Serial != diagnosticDispatchSerial || NativeHostCallbacks != dispatch.Callback || dispatch.NativeHeaderIndex is not int header || header is < 0 or >= NativeBattleRetainedActionPackets.HeaderCapacity || dispatch.NativeHeaderGeneration is not long generation || generation <= 0 || diagnosticHeaderGenerations is not { } generations || !generations.TryGetValue(header, out long actualGeneration) || actualGeneration != generation || nativeActionPackets.ActiveHeader != new BattleActionHeaderReference(header))
        {
            throw new InvalidOperationException("Diagnostic action packets require the exact current synchronous dispatch/header generation owner.");
        }
        nativeActionPackets.Validate();
        BattleActionHeaderReference reference = new(header);
        NativeBattleActionHeaderSlot source = nativeActionPackets.ReadHeader(reference);
        if (dispatch.ActorId is null || !byId.TryGetValue(dispatch.ActorId, out BattleCombatant? actor) || CreatePresentationModelOwner(actor) != source.Source || dispatch.NativeActorIndex != actor.NativeActorIndex || dispatch.NativeCommand != source.Command || dispatch.ActionId != source.Action || dispatch.RequestedEffect != source.Effect || dispatch.ExecutedEffect != source.Effect || nativeActionPackets.ModelAliases.EncodeTargetMask(NativeBattleActionGraphics.Normalize(nativeActionPackets, reference).TargetActors.Resolve(nativeActionPackets.ModelAliases)) != dispatch.NativeTargetMask)
        {
            throw new InvalidDataException("Diagnostic action packet payload differs from its current dispatch source fields.");
        }
        return nativeActionPackets.ExportRetained().DetachDiagnosticCopy();
    }

    /// <summary>Observes the original Duel timeout control's zero-magic header while its retained274 entry executes selector8.</summary>
    public NativeBattleRetainedActionPackets ObserveDiagnosticDuelClosingPackets(BattleDiagnosticEffectDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        if (!ReferenceEquals(dispatch, diagnosticPacketReadDispatch) || diagnosticPacketReadThread != Environment.CurrentManagedThreadId || dispatch.Serial <= 0 || dispatch.Serial != diagnosticDispatchSerial || NativeHostCallbacks != dispatch.Callback || dispatch.NativeHeaderIndex is not int header || header is < 0 or >= NativeBattleRetainedActionPackets.HeaderCapacity || dispatch.NativeHeaderGeneration is not long generation || generation <= 0 || diagnosticHeaderGenerations is not { } generations || !generations.TryGetValue(header, out long actualGeneration) || actualGeneration != generation || nativeActionPackets.ActiveHeader != new BattleActionHeaderReference(header) || dispatch.ActorId is null || !byId.TryGetValue(dispatch.ActorId, out BattleCombatant? actor) || actor.ModelId != 1 || actor.NativeLimit is not { CharacterId: 1, Command: 11 } || nativeDuelProcesses.Count != 1 || !nativeDuelProcesses.TryGetValue(actor.Id, out BattleNativeDuelState? duel) || duel is not { Phase: BattleNativeDuelPhase.Finisher, Selection1D28DEF: 254, RemainingTimer: <= 0 } || dispatch.NativeCommand != 241 || dispatch.ActionId != 65532 || dispatch.RequestedEffect != 274 || dispatch.ExecutedEffect != 274 || diagnosticPacketReadEffect is not { RequestedEffect: 274, Binding.Effect: 274, Parameters: { Parameter80: 8, Parameter82: 0 } parameters } || parameters.Source != CreatePresentationModelOwner(actor) || parameters.Header != new BattleActionHeaderReference(header) || NativeEffectMemory?.Entry21DFEC4?.Effect != 274)
        {
            throw new InvalidDataException("Duel closing packets require the actual synchronous timed-out native241/FFFC retained274 control owner. " + DescribeDiagnosticDuelClosingGuard(dispatch));
        }
        nativeActionPackets.Validate();
        BattleActionHeaderReference reference = new(header);
        NativeBattleActionHeaderSlot source = nativeActionPackets.ReadHeader(reference);
        if (source.Source != CreatePresentationModelOwner(actor) || dispatch.NativeActorIndex != actor.NativeActorIndex || source.Command != 241 || source.Action != 65532 || source.Effect != 0 || nativeActionPackets.ModelAliases.EncodeTargetMask(NativeBattleActionGraphics.Normalize(nativeActionPackets, reference).TargetActors.Resolve(nativeActionPackets.ModelAliases)) != dispatch.NativeTargetMask)
        {
            throw new InvalidDataException("Duel timeout control changed its actual actor, zero-magic header or native target normalization.");
        }
        return nativeActionPackets.ExportRetained().DetachDiagnosticCopy();
    }

    private string DescribeDiagnosticDuelClosingGuard(BattleDiagnosticEffectDispatch dispatch)
    {
        BattleCombatant? actor = dispatch.ActorId is { } id && byId.TryGetValue(id, out BattleCombatant? found) ? found : null;
        BattleNativeDuelState? duel = actor is not null && nativeDuelProcesses.TryGetValue(actor.Id, out BattleNativeDuelState? state) ? state : null;
        long? generation = dispatch.NativeHeaderIndex is int selected && diagnosticHeaderGenerations is { } generations && generations.TryGetValue(selected, out long actual) ? actual : null;
        NativeBattleRetainedActionHeaderSlot? header = dispatch.NativeHeaderIndex is int index && index >= 0 && index < NativeBattleRetainedActionPackets.HeaderCapacity ? nativeActionPackets.ExportRetained().Headers[index] : null;
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
            ActiveHeaderIndex = OriginalDiagnosticHeaderIndex(nativeActionPackets.ActiveHeader),
            ActorModel = actor?.ModelId,
            ActorIndex = actor?.NativeActorIndex,
            LimitCharacter = actor?.NativeLimit?.CharacterId,
            LimitCommand = actor?.NativeLimit?.Command,
            DuelOwners = nativeDuelProcesses.Count,
            Duel = duel is null ? null : new { Phase = duel.Phase.ToString(), duel.Selection1D28DEF, duel.RemainingTimer, duel.InitialTimer, TargetMask = NativeBattleTargetRequestAdapter.ProjectOriginalWord(duel.TargetRequest, ReadOriginalTargetSlot) },
            PublishedEffect = effect is null ? null : new { effect.RequestedEffect, effect.Binding.Effect, Actor = effect.Parameters?.Source, Header = effect.Parameters?.Header, Selector80 = effect.Parameters?.Parameter80, Ready81 = effect.Parameters?.Parameter81, Variant82 = effect.Parameters?.Parameter82 },
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
