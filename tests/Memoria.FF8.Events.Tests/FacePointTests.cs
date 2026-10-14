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
    private static void NativeFacePointContract()
    {
        byte[] Content(int x, int duration, int y, int z, int yawLimit) => SyntheticObjects([1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.FACEDIRINIT, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, yawLimit), (Jsm.Opcode.FACEDIRLIMIT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, y), (Jsm.Opcode.PSHN_L, z), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.FACEDIR, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create(int x = 100, int duration = 4, bool facing = true, int y = 0, int z = 100, int yawLimit = 10)
        {
            GameSession session = GameSession.CreateColdProcess();
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                ReadFaceBaseline = _ => new FaceDirectionBaseline(0, 0),
                ReadFaceAnchorHeight = _ => 0,
                ResolveNativeActorFacing = facing ? query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY) : null
            };
            engine.LoadField(600, Content(x, duration, y, z, yawLimit), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        List<NativeActorFacingQuery> queries = [];
        Func<NativeActorFacingQuery, byte?> provider = live.ResolveNativeActorFacing!;
        live.ResolveNativeActorFacing = query =>
        {
            queries.Add(query);
            return provider(query);
        };
        while (live.Actors[0].FaceTargetPoint is null && live.Frame < 8)
        {
            live.Step();
        }
        ActorState actor = live.Actors[0];
        Require(actor.FaceTargetPoint == new Vector3(100, 0, 100) && actor.FaceDuration == 4 && actor.FaceProgress == 1 && actor.FirstControlRotation is not null && queries.Count == 4 && !live.Capture().GetProperty("Variables").TryGetProperty("5000", out _), "FACEDIR did not preserve its XYZ/duration issue words, pure preflight, native wait and same-callback post-VM face sample.");
        Require(queries[0] == new NativeActorFacingQuery(0, 0, 100 * 4096, 0) && queries[1] == new NativeActorFacingQuery(0, 100 * 4096, 100 * 4096, 0), "FACEDIR did not use the shared native477380 yaw/pitch queries.");
        EventEngine restored = Create();
        restored.Restore(live.Capture());
        JsonElement beforeRestore = restored.Capture();
        foreach (string malformedProperty in new[] { "FaceProgress", "FaceElapsed" })
        {
            JsonNode malformed = JsonNode.Parse(beforeRestore.GetRawText())!;
            if (malformedProperty == "FaceProgress")
            {
                malformed["Actors"]![0]![malformedProperty] = 5;
            }
            else
            {
                malformed["Actors"]![0]!.AsObject().Remove(malformedProperty);
            }
            try
            {
                restored.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Malformed current face words restored.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(beforeRestore, restored.Capture()), "Malformed native face phase partially mutated the VM.");
        }
        int[] pitch = [1, 4, 6, 8];
        int[] yaw = [1, 5, 8, 10];
        Vector3 firstRotation = actor.FirstControlRotation!.Value;
        Require(Math.Abs(firstRotation.Y - pitch[0] * MathF.Tau / 256) < 0.00001 && Math.Abs(firstRotation.Z + yaw[0] * MathF.Tau / 256) < 0.00001, "FACEDIR first sample was postponed past its actual post-VM callback.");
        for (int callback = 1; callback < 4; callback++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "FACEDIR pending point/cosine wait changed across independent restore.");
            Vector3 rotation = actor.FirstControlRotation!.Value;
            Require(Math.Abs(rotation.Y - pitch[callback] * MathF.Tau / 256) < 0.00001 && Math.Abs(rotation.Z + yaw[callback] * MathF.Tau / 256) < 0.00001, "FACEDIR first-control-node override differs from original cosine/signs.");
        }
        Require(actor.FaceProgress == 4 && !live.Capture().GetProperty("Variables").TryGetProperty("5000", out _), "FACEDIR consumed completion before the next native VM observed post-body equality.");
        live.Step();
        restored.Step();
        Require(actor.FaceElapsed == 8 && actor.FaceSecondAngle == 10 && live.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 77 && actor.Direction == 0 && actor.MovementDirectionLimit == 0 && actor.FirstControlRotation is not null && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "FACEDIR closing callback delayed its successor, changed physical facing or released the completed joint override.");
        foreach ((int x, int duration, int expectedDuration) in new[] { (100 + 65536, 65536 + 4, 4), (100, 65535, 65535), (100, 0, 0) })
        {
            EventEngine edge = Create(x, duration);
            while (edge.Actors[0].FaceTargetPoint is null && edge.Frame < 8)
            {
                edge.Step();
            }
            Require(edge.Actors[0].FaceTargetPoint == new Vector3(100, 0, 100) && edge.Actors[0].FaceDuration == expectedDuration, "FACEDIR lost low signed targetWORD or unsigned durationWORD.");
            if (duration == 0)
            {
                Require(edge.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 77 && edge.Actors[0].FirstControlRotation is null, "Zero-duration FACEDIR yielded or invented a joint rotation.");
            }
            else
            {
                edge.Step();
                EventEngine clone = Create(x, duration);
                clone.Restore(edge.Capture());
                edge.Step();
                clone.Step();
                Require(JsonElement.DeepEquals(edge.Capture(), clone.Capture()), "FACEDIR WORD edge changed its retained phase after restore.");
            }
        }
        EventEngine near = Create(1, 1, y: 1, z: 0, yawLimit: 255);
        while (near.Actors[0].FaceTargetPoint is null && near.Frame < 8)
        {
            near.Step();
        }
        near.Step();
        Require(near.Actors[0].FaceYawTarget == 96 && Math.Abs(near.Actors[0].FirstControlRotation!.Value.Z + 96 * MathF.Tau / 256) < 0.00001, "Near diagonal face target used the old asin64 proposal instead of native B80C18's saturated index128 angle32.");
        EventEngine missing = Create(facing: false);
        try
        {
            while (missing.Frame < 8)
            {
                missing.Step();
            }
            throw new InvalidOperationException("FACEDIR substituted approximate facing for an absent native provider.");
        }
        catch (EventScriptException) when (missing.LastFault?.Opcode == nameof(Jsm.Opcode.FACEDIR))
        {
        }
        JsonElement fiber = missing.Capture().GetProperty("Fibers").EnumerateArray().Single(item => !item.GetProperty("Initialization").GetBoolean());
        Require(fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([4, 100, 0, 100]) && missing.Actors[0].FaceTargetPoint is null && missing.Actors[0].FaceProgress == 0 && missing.Actors[0].FaceDuration == 0 && missing.Actors[0].FirstControlRotation is null, "FACEDIR missing-provider refusal consumed operands or changed its joint state.");
        Console.WriteLine("FACEDIR: signedXYZ/unsignedduration WORDs, shared477380 queries, exact clamped cosine/joint signs, zero/fullWORD edges, successor timing, pending restore and missing-provider atomic refusal passed.");
    }
}
