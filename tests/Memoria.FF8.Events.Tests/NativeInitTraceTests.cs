using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeInitializationTraceContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        (Jsm.Opcode, int)[] opening = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 45), (Jsm.Opcode.PSHN_L, 67), (Jsm.Opcode.PSHN_L, 31), (Jsm.Opcode.SET3, 2), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 199), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 12345), (Jsm.Opcode.INITTRACE, 0), (Jsm.Opcode.POPM_L, 5000), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 5004), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([1], [init, main]);
        EventEngine Create()
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            return new EventEngine(session, session.EventMemory)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolveNativeTriangleHeight = (triangle, _, _) => triangle == 9 ? 49 : 31,
                ResolvePartyTraceTriangle = (_, _, _) => 9
            };
        }
        EventEngine live = Create();
        // Actual New Game field74 owns the initial global 00 FF FF party bytes.
        live.LoadField(74, SyntheticObjects([1], [opening, main]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        live.Step();
        live.LoadField(229, SyntheticObjects([1], [opening, main]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        live.Step();
        PartyTraceSample previous = live.Scene.PartyTrace?.FirstTrail[0] ?? throw new InvalidOperationException("Declared opening loader did not produce the prior record-zero metadata.");
        Require(previous is { X: 45, Y: 67, Z: 31, Triangle: 2, Direction: 77 } && live.Scene.NativePartyTraceVersion == 1, "Declared opening trace changed its native loader-owned baseline.");
        FieldTransition arrival = new(600, 100, 200, 9, 64);
        live.LoadField(600, content, [], arrival, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        bool observedOpaque = false;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.INITTRACE))
            {
                observedOpaque = true;
                Require(live.Scene.PartyTrace is { Cursor: 0, FirstOffset: 15, SecondOffset: 30 } opaque && opaque.FirstTrail.All(sample => sample is null) && opaque.SecondTrail.All(sample => sample is null) && live.Scene.NativeActorLayoutVersion == 0 && live.Scene.NativePartyTraceVersion == 0 && live.Scene.FieldPartyNativeActorIds.Count == 0 && live.PlayerActor is null, "Initialization INITTRACE invented a precompact body or observable temporary trace geometry.");
                Require(live.Scene.FieldLoadFirstRecordZero == previous && live.Scene.FieldLoadSecondRecordZero == previous, "Initialization INITTRACE changed carried record-zero metadata.");
            }
        };
        live.Step();
        JsonElement completedSnapshot = live.Capture();
        Require(observedOpaque && completedSnapshot.GetProperty("Variables").GetProperty("5000").GetInt32() == 12345 && completedSnapshot.GetProperty("Variables").GetProperty("5004").GetInt32() == 567, "Synchronous initialization failed its transient trace observation, stack, R0 or return2 continuation.");
        EventEngine independent = Create();
        independent.LoadField(600, content, [], arrival, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        independent.Restore(completedSnapshot);
        Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Independent completed initialization restore changed the automatic loader result.");
        PartyTraceState completed = live.Scene.PartyTrace!;
        Require(live.Scene.NativeFieldLoadVersion == 1 && live.Scene.NativePartyTraceVersion == 1 && live.PlayerActor is { X: 100, Y: 200, Z: 49, WalkmeshTriangle: 9, Direction: 64 } && completed.FirstTrail[0] == previous with { X = 100, Y = 200, FirstFlag = 1, SecondFlag = 1 } && completed.FirstTrail.Skip(1).All(sample => sample is { Z: 49, Triangle: 9, Direction: 64, Animation: 0 }) && completed.FirstTrail.SequenceEqual(completed.SecondTrail), "Automatic field setup failed to replace temporary records or preserve the original record-zero tail.");
        Console.WriteLine("INITTRACE normal precompact entry: transient opaque rings, preserved prior record-zero tail, unchanged operands/R0, synchronous automatic overwrite and independent completed-init restore PASS (declared component geometry).");
    }
}
