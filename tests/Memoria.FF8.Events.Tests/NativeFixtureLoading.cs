using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void LoadInitializedFixtureField(EventEngine engine, GameSession session, byte[] content)
    {
        (Jsm.Opcode, int)[] boot = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] depart = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.MAPJUMP, 0), (Jsm.Opcode.RET, 8)];
        engine.LoadField(74, SyntheticObjects([1], [boot, depart]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        FieldTransition arrival = session.State.FieldArrival ?? throw new InvalidOperationException("Native fixture bootstrap did not execute its actual MAPJUMP.");
        Require(arrival.FieldId == 600 && engine.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "Native fixture lost its native bootstrap departure.");
        engine.LoadField(600, content, [], arrival, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
    }
}
