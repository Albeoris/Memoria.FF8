using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSlopedLineContract(string gameDirectory)
    {
        EventEngine Create(int startZ = 10, int endZ = 0, int targetX = 2, int groundZ = 0, bool competing = false)
        {
            List<(Jsm.Opcode, int)[]> scripts =
            [
                [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, groundZ), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)],
                [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -20), (Jsm.Opcode.PSHN_L, startZ), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, endZ), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 0)]
            ];
            for (int label = 17; label < 23; label++)
            {
                scripts.Add(label == 17 && competing ? [(Jsm.Opcode.LBL, label), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)] : [(Jsm.Opcode.LBL, label), (Jsm.Opcode.RET, 0)]);
            }
            scripts.Add([(Jsm.Opcode.LBL, 23), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
            byte[] content = SyntheticObjects([(16 << 7) | 7, 0], scripts);
            content[1] = 1;
            content[3] = 1;
            JsmProgram decoded = JsmProgram.Read(content);
            Require(!decoded.Objects[0].IsDoor && decoded.Objects[1].IsDoor && decoded.NativeObjects.SequenceEqual([0, 1]), "Original raw door group was not correlated before sorting/native pointer-table ordering.");
            GameSession session = new(new GameState());
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 2,
                ResolveWalkmesh = (x, y) => new WalkmeshPosition(new Vector3(x, y, groundZ), 0),
                ResolveNativeTriangleHeight = (_, _, _) => groundZ,
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                CanUseNativePlayerMovement = _ => true,
                ResolveNativePlayerMovement = query => new NativePlayerMovementResult(targetX * 4096, 0, groundZ * 4096, 0, query.Direction, [new NativeJoinMovementProposal(targetX * 4096, 0, query.Direction)])
            };
            engine.LoadField(172, content, [], new FieldTransition(172, -2, 0, 0, 0));
            engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 10));
            Require(engine.Scene.NativePartyTraceVersion == 1 && engine.Actors[0].X == -2, $"Synthetic native line fixture did not finish original initialization/party placement: frame{engine.Frame}, load{engine.Scene.NativeFieldLoadVersion}, trace{engine.Scene.NativePartyTraceVersion}, playerX{engine.Actors[0].X}.");
            return engine;
        }

        EventEngine engine = Create();
        engine.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        JsonElement pending = engine.Capture();
        Require(engine.Scene.NativePlayerMovementVersion == 1 && engine.Actors[1].NativeLineContactVersion == 1 && engine.Actors[1].LineContact && engine.Actors[1].NativeLineCrossingPending && !CapturedVariables(pending).TryGetProperty("5000", out _) && engine.LineCrossings.Count == 1, "Native sloped line did not stage its radius-qualified crossing after the issuing action, or ran it one update early.");
        EventEngine independent = Create();
        independent.Restore(pending);
        Require(JsonElement.DeepEquals(pending, independent.Capture()), "Pending native line crossing did not restore independently.");
        engine.Apply(new PlayerAction(PlayerActionKind.Wait));
        independent.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()) && !engine.Actors[1].NativeLineCrossingPending && CapturedVariables(engine.Capture()).GetProperty("5000").GetInt32() == 1, "The following update did not consume the native crossing exactly once across independent restore.");
        Require(engine.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ObjectId").GetInt32() == 1 && fiber.GetProperty("ScriptId").GetInt32() == 23 && fiber.GetProperty("Priority").GetInt32() == 2 && fiber.GetProperty("WaitReason").GetString() == "Frames"), "Native crossing did not select original local7 at priority2.");
        for (int tick = 0; tick < 4; tick++)
        {
            engine.Apply(new PlayerAction(PlayerActionKind.Wait));
            independent.Apply(new PlayerAction(PlayerActionKind.Wait));
            Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "Native line script wait changed after independent restore.");
        }

        foreach (string gate in new[] { "control", "line" })
        {
            EventEngine gated = Create();
            JsonNode disabled = JsonNode.Parse(pending.GetRawText())!;
            if (gate == "control")
            {
                disabled["UserControlEnabled"] = false;
            }
            else
            {
                disabled["Actors"]![1]!["LineEnabled"] = false;
            }
            gated.Restore(JsonSerializer.SerializeToElement(disabled));
            gated.Step();
            Require(!gated.Actors[1].NativeLineCrossingPending && !CapturedVariables(gated.Capture()).TryGetProperty("5000", out _), "Native disabled/control gate did not clear pending crossing without dispatch.");
        }

        foreach ((int height, int targetX, bool expected) in new[] { (47, 0, true), (48, 0, false), (80, 2, false) })
        {
            EventEngine threshold = Create(height, height, targetX);
            threshold.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
            Require(threshold.Actors[1].LineContact == expected && threshold.Actors[1].NativeLineCrossingPending == expected, "Native SETLINE strict3D radius threshold or endpoint-side zero handling changed.");
        }
        EventEngine noCrossing = Create(targetX: -1);
        noCrossing.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        Require(noCrossing.Actors[1].LineContact && !noCrossing.Actors[1].NativeLineCrossingPending, "Native line proximity incorrectly became a same-side crossing.");

        foreach (string invalid in new[] { "schema", "unknown", "command", "owner", "width", "movement" })
        {
            EventEngine rejected = Create();
            JsonElement before = rejected.Capture();
            JsonNode malformed = JsonNode.Parse(pending.GetRawText())!;
            switch (invalid)
            {
                case "schema":
                    malformed["Actors"]![1]!["NativeLineContactVersion"] = 2;
                    break;
                case "unknown":
                    malformed["Actors"]![1]!["NativeLineContactVersion"] = 0;
                    break;
                case "command":
                    malformed["Actors"]![1]!["LineOperation"] = 0;
                    break;
                case "owner":
                    malformed["Actors"]![0]!["NativeLineContactVersion"] = 1;
                    break;
                case "width":
                    malformed["Actors"]![1]!["LineStart"]!["Z"] = 40000;
                    break;
                case "movement":
                    malformed["Scene"]!["NativePlayerMovementVersion"] = 0;
                    break;
            }
            try
            {
                rejected.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Malformed native line checkpoint was accepted.");
            }
            catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
            {
            }
            Require(JsonElement.DeepEquals(before, rejected.Capture()), "Native line checkpoint validation mutated the VM before refusal.");
        }

        foreach (string invalid in new[] { "geometry", "owner" })
        {
            EventEngine rejected = Create();
            JsonNode malformed = JsonNode.Parse(rejected.Capture().GetRawText())!;
            if (invalid == "geometry")
            {
                malformed["Actors"]![1]!["LineEnd"] = malformed["Actors"]![1]!["LineStart"]!.DeepClone();
            }
            else
            {
                malformed["Actors"]![1]!["LineOperation"] = 0;
            }
            rejected.Restore(JsonSerializer.SerializeToElement(malformed));
            JsonElement before = rejected.Capture();
            try
            {
                rejected.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
                throw new InvalidOperationException("Unsupported native line query was accepted.");
            }
            catch (NotSupportedException)
            {
            }
            Require(JsonElement.DeepEquals(before, rejected.Capture()), "Native line geometry/owner refusal changed position, frame, contacts, trace or action log.");
        }
        EventEngine competing = Create(competing: true);
        JsonElement competingBefore = competing.Capture();
        try
        {
            competing.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
            throw new InvalidOperationException("Native line crossing with unproved priority suspension was accepted.");
        }
        catch (NotSupportedException)
        {
        }
        Require(JsonElement.DeepEquals(competingBefore, competing.Capture()), "Native line competing-fiber refusal consumed the ordinary movement.");
        LineContactAndResume();
        Console.WriteLine("Native SETLINE: sloped finite3D distance/currentZ, strict radius and integer side crossing; declared doors, atomic preflight, next-update local7/priority2, gated pending clear and checkpoint restore; legacy coplanar line regression preserved.");
    }
}
