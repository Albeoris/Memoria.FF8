using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMapJumpOriginContract()
    {
        byte[] jump = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 65636), (Jsm.Opcode.PSHN_L, 65538), (Jsm.Opcode.MAPJUMPO, 0), (Jsm.Opcode.RET, 0));
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 99 });
        EventEngine issuing = new(session, session.State.Variables);
        issuing.LoadField(99, jump, []);
        issuing.Step();
        Require(session.State.FieldArrival == new FieldTransition(100, 32767, 32767, 2, 0) && session.State.EntryPoint == 0 && issuing.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "MAPJUMPO did not preserve its low WORD field/triangle and original centroid sentinels.");
        GameSession missingSession = new(new GameState());
        EventEngine missing = new(missingSession, missingSession.State.Variables);
        missing.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 175), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.MAPJUMPO, 0), (Jsm.Opcode.RET, 0)), []);
        missing.Step();
        JsonObject missingSnapshot = JsonNode.Parse(missing.Capture().GetRawText())!.AsObject();
        missingSnapshot["Fibers"]![0]!["Stack"]!.AsArray().RemoveAt(0);
        missingSnapshot["Fibers"]![0]!["StackExpressions"]!.AsArray().RemoveAt(0);
        missing.Restore(JsonSerializer.SerializeToElement(missingSnapshot));
        try
        {
            missing.Step();
            throw new InvalidOperationException("MAPJUMPO consumed a missing triangle operand.");
        }
        catch (EventScriptException)
        {
        }
        Require(missing.LastFault?.Opcode == "MAPJUMPO" && missing.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 5 && missing.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == 175 && missingSession.State.FieldArrival is null, "Missing MAPJUMPO operands changed the issuing PC, stack or destination.");

        FieldWalkmesh mesh = new([new WalkmeshTriangle(new Vector3(-5, -4, 8), new Vector3(-5, 3, 10), new Vector3(2, -4, 9), -1, -1, -1)]);
        Require(FieldPartyTraceGeometry.ResolveTriangleCentroid(mesh, 0) == new Vector3(-2, -1, 9), "Native centroid did not divide signed XYZ vertex sums toward zero independently of plane height.");
        Require(FieldPartyTraceGeometry.ResolveTriangleCentroid(mesh, -1) is null && FieldPartyTraceGeometry.ResolveTriangleCentroid(mesh, 1) is null, "Invalid original triangle acquired a centroid.");
        byte[] init = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 0));
        EventEngine Create(int triangle)
        {
            GameSession target = new(new GameState());
            EventEngine result = new(target, target.State.Variables)
            {
                AnimationFrameCount = (_, _) => 3,
                ResolveNativeTriangleCentroid = index => FieldPartyTraceGeometry.ResolveTriangleCentroid(mesh, index),
                ResolveNativeTriangleHeight = (index, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(mesh, index, x, y),
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(mesh, x, y, z)
            };
            result.Scene.Party.Add(0);
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
