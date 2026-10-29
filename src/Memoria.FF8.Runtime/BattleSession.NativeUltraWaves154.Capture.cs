namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleUltraWaves154State State, NativeBattleGraphicsState Graphics, int Cursor) CaptureNativeUltraWaves154(NativeBattleUltraWaves154State state, NativeBattleGraphicsState graphics, NativeBattleUltraWaves154Resources resources, int cursor, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleEffectArena offsets = state.CaptureOffsets ?? throw new NotSupportedException("Ultra Waves requires its observed retained capture offsets.");
        NativeUltraWavesCaptureResult captured = CaptureNativeUltraWaves(graphics, state.Packets, state.CapturePackets, cursor, "24224D4", "DBAEE0", resources.StageDraw, pilotIdentity: pilotIdentity);
        offsets = offsets.Write(0, BitConverter.GetBytes((short)384)).Write(2, BitConverter.GetBytes((short)256));
        return (state with { Packets = captured.Packets, CapturePackets = captured.CapturePackets, CaptureOffsets = offsets }, captured.Graphics, captured.Cursor);
    }
}
