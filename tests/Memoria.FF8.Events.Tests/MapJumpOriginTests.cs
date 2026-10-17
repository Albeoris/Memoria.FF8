using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    // A transition is an ordinary callback command; synchronous initialization only prepares its default script.
    private static byte[] OriginCallbackProgram(params (Jsm.Opcode Opcode, int Parameter)[] operations)
    {
        return SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], operations]);
    }

    private static void NativeMapJumpOriginContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession CreateSession()
        {
            return CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
        }

        byte[] jump = OriginCallbackProgram((Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 65636), (Jsm.Opcode.PSHN_L, 65538), (Jsm.Opcode.MAPJUMPO, 0), (Jsm.Opcode.RET, 0));
        GameSession session = CreateSession();
        EventEngine issuing = new(session);
        issuing.LoadField(99, jump, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        issuing.Step();
        issuing.Step();
        Require(session.State.FieldArrival == new FieldTransition(100, 32767, 32767, 2, 0) && session.State.EntryPoint == 0 && issuing.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "MAPJUMPO did not preserve its low WORD field/triangle and original centroid sentinels.");
        GameSession missingSession = CreateSession();
        EventEngine missing = new(missingSession);
        missing.LoadField(100, OriginCallbackProgram((Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 175), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.MAPJUMPO, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        missing.Step();
        missing.Step();
        try
        {
            missing.Step();
            throw new InvalidOperationException("MAPJUMPO consumed a missing triangle operand.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement refused = missing.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean());
        Require(missing.LastFault?.Opcode == "MAPJUMPO" && refused.GetProperty("Position").GetInt32() == 6 && refused.GetProperty("Stack")[0].GetInt32() == 175 && missingSession.State.FieldArrival is null, "Missing MAPJUMPO operands changed the issuing PC, stack or destination.");

        FieldWalkmesh mesh = new([new WalkmeshTriangle(new Vector3(-5, -4, 8), new Vector3(-5, 3, 10), new Vector3(2, -4, 9), -1, -1, -1)]);
        Require(FieldPartyTraceGeometry.ResolveTriangleCentroid(mesh, 0) == new Vector3(-2, -1, 9), "Native centroid did not divide signed XYZ vertex sums toward zero independently of plane height.");
        Require(FieldPartyTraceGeometry.ResolveTriangleCentroid(mesh, -1) is null && FieldPartyTraceGeometry.ResolveTriangleCentroid(mesh, 1) is null, "Invalid original triangle acquired a centroid.");
        byte[] init = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 0));
        EventEngine Create(int triangle)
        {
            GameSession target = CreateSession();
            EventEngine result = new(target)
            {
                AnimationFrameCount = (_, _) => 3,
                ResolveNativeTriangleCentroid = index => FieldPartyTraceGeometry.ResolveTriangleCentroid(mesh, index),
                ResolveNativeTriangleHeight = (index, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(mesh, index, x, y),
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(mesh, x, y, z)
            };
            result.LoadField(100, init, [], FieldTransition.FromMapJumpOrigin(100, triangle), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return result;
        }
        EventEngine loaded = Create(0);
        loaded.Step();
        Require(loaded.Scene.NativeFieldLoadVersion == 1 && loaded.Scene.NativePartyTraceVersion == 1 && loaded.Scene.FieldPartyNativeActorIds.SequenceEqual([0, 255, 255]) && loaded.Scene.NativePlayerPosition == new FieldFixedPosition(-8192, -4096, 36864), "MAPJUMPO centroid entry failed to allocate authentic fixed locomotion and compact party bindings.");
        Require(loaded.Scene.PartyTrace!.FirstTrail[0] is null && loaded.Scene.PartyTrace.FirstTrail[63] is { Animation: 0 }, "Field-to-field MAPJUMPO used the startup filled-ring policy instead of the generated entry trail.");
        JsonElement snapshot = loaded.Capture();
        EventEngine restored = Create(0);
        restored.Restore(snapshot);
        Require(JsonElement.DeepEquals(snapshot, restored.Capture()), "Native centroid allocation did not restore independently.");
        EventEngine reset = Create(short.MaxValue);
        reset.Step();
        Require(reset.Scene.NativePlayerPosition == loaded.Scene.NativePlayerPosition && reset.Actors[0].WalkmeshTriangle == 0 && reset.Actors[0].MovementSpeed == 2696 && reset.Actors[0].PushRadius == 48 && reset.Actors[0].AnimationSpeed == 16 && reset.Actors[0].BaseAnimationId == 0 && reset.Actors[0].WalkAnimationId == 1 && reset.Actors[0].RunAnimationId == 2, "Native triangle7FFF did not select triangle0 centroid and execute its original locomotion resets.");
        EventEngine invalid = Create(1);
        try
        {
            invalid.Step();
            throw new InvalidOperationException("Native entry accepted an invalid destination triangle.");
        }
        catch (NotSupportedException)
        {
        }
        Require(invalid.Scene.NativeActorLayoutVersion == 0 && invalid.Scene.NativeFieldLoadVersion == 0 && invalid.Scene.PartyTrace is null && invalid.Capture().GetProperty("PendingArrival").ValueKind == JsonValueKind.Object, "Invalid destination triangle committed partial native party or trace state.");
        Console.WriteLine("MAPJUMPO: original WORD packet, signed centroid XYZ, generated entry trace, fixed locomotion, independent restore and atomic operand/triangle refusal passed.");
    }
}
