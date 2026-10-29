namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private BattleDiagnosticEffectDispatch? diagnosticPacketReadDispatch;
    private int diagnosticPacketReadThread;

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
}
