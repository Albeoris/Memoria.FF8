using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBattleCounterMirrorContract()
    {
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        NativeEventMemory memory = NativeEventMemory.Empty.Write(19, 4, NativeScalarValue.Exact(0x44332211)).Write(23, 4, NativeScalarValue.Exact(0x77665544)).Write(27, 2, NativeScalarValue.Exact(0x6655));
        GameSession session = new(initial.State with { BattleStatistics = new(uint.MaxValue, 0x1234, 0xABCD), EventMemory = memory });
        EventEngine engine = new(session);
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.POPM_L, 20), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(engine.EventMemory.Read(20, 4).Values[0] == -1 && engine.EventMemory.Read(24, 2).Values[0] == 0x1234 && engine.EventMemory.Read(26, 2).Values[0] == 0xABCD, "Native allocation did not copy the exact battle counter widths.");
        Require(engine.EventMemory.Read(19, 1).Values[0] == 0x11 && engine.EventMemory.Read(28, 1).Values[0] == 0x66, "Battle counter refresh overwrote adjacent generic bytes.");
        engine.Step();
        Require(engine.EventMemory.Read(20, 4).Values[0] == 17 && session.State.BattleStatistics!.Victories == uint.MaxValue, "Field counter memory became a permanent alias of the saved totals.");
        engine.LoadField(601, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En, restoringCheckpoint: true);
        Require(engine.EventMemory.Read(20, 4).Values[0] == 17, "Frozen field restoration refreshed an ordinary script-written counter mirror.");
        engine.LoadField(602, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(engine.EventMemory.Read(20, 4).Values[0] == -1, "A fresh field allocation retained the previous field's mutable counter mirror.");
        GameSession legacy = new(initial.State with { BattleStatistics = null, EventMemory = memory });
        EventEngine legacyEngine = new(legacy);
        legacyEngine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(legacyEngine.EventMemory.Read(20, 4).Values[0] == memory.Read(20, 4).Values[0], "Unknown historical totals manufactured a zero counter or blocked field allocation.");
    }
}
