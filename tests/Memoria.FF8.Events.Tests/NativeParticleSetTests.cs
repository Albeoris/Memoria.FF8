using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeParticleSetContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        foreach (int rawSlot in new[] { -16, 65552, 31, -1 })
        {
            EventEngine engine = CreateParticleSetFixture(vitals, economy, [(Jsm.Opcode.NOP, 0), (Jsm.Opcode.PSHN_L, rawSlot), (Jsm.Opcode.PARTICLEON, 0), (Jsm.Opcode.PSHN_L, rawSlot), (Jsm.Opcode.PARTICLESET, 0), (Jsm.Opcode.PSHN_L, rawSlot), (Jsm.Opcode.PARTICLEOFF, 0)]);
            JsonElement? beforeActor = null;
            JsonElement? beforeRandom = null;
            long? commandFrame = null;
            int commands = 0;
            engine.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.NOP) && command.ObjectId == 7)
                {
                    beforeActor = JsonSerializer.SerializeToElement(engine.Actors[7]);
                    beforeRandom = engine.Capture().GetProperty("EventRandom");
                }
                if (command.Name is nameof(Jsm.Opcode.PARTICLEON) or nameof(Jsm.Opcode.PARTICLESET) or nameof(Jsm.Opcode.PARTICLEOFF))
                {
                    commandFrame ??= engine.Frame;
                    commands++;
                    Require(command.Arguments.SequenceEqual([rawSlot]) && engine.Frame == commandFrame && JsonElement.DeepEquals(beforeActor!.Value, JsonSerializer.SerializeToElement(engine.Actors[7])), "Particle byte writes changed the original raw operand, yielded their VM callback or mutated actor body/animation state.");
                    if (command.Name == nameof(Jsm.Opcode.PARTICLESET))
                    {
                        Require(engine.Scene.NativeActorObjectIds.SequenceEqual([3, 6, 7, 8, 12]) && engine.Scene.NativeParticleSlots[rawSlot & 15] == 0x82 && engine.ReadNativeParticleAttachment(rawSlot & 15) is { Value: 0x82, InstanceIndex: 2, ObjectId: 7, X: -19, Y: -22, Z: 32760 }, "PARTICLESET confused its actual dense renderer byte with logical object/model7, narrowed the slot incorrectly or changed native raw attachment coordinates.");
                    }
                }
            };
            ReachParticleSetSuccessor(engine);
            NativeParticleRequest[] requests = engine.Scene.NativeParticleRequests.ToArray();
            JsonElement completed = engine.Capture();
            JsonElement caller = ParticleSetCaller(engine);
            Require(commands == 3 && requests.Select(request => request.RawSlot).SequenceEqual(Enumerable.Repeat(rawSlot, 3)) && requests.Select(request => request.Slot).SequenceEqual(Enumerable.Repeat(rawSlot & 15, 3)) && requests.Select(request => request.Value).SequenceEqual(new byte[] { 1, 0x82, 0 }) && requests.Select(request => request.InstanceIndex).SequenceEqual(new int?[] { null, 2, null }) && engine.Scene.NativeParticleSlots.Count == 1 && engine.ReadNativeParticleAttachment(rawSlot & 15) is null && CapturedVariables(completed).GetProperty("6000").GetInt32() == 73 && caller.GetProperty("Stack").GetArrayLength() == 0 && caller.GetProperty("Results").GetProperty("7").GetProperty("ActorInfoOrigin").GetProperty("NativeActorIndex").GetInt32() == 255 && JsonElement.DeepEquals(beforeRandom!.Value, completed.GetProperty("EventRandom")), "ON/SET/OFF failed to overwrite one physical byte, preserve source-instance provenance and unrelated opaque scalar, return2 to its immediate successor or leave event RNG untouched.");
        }

        EventEngine live = CreateParticleSetFixture(vitals, economy, [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PARTICLEON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PARTICLESET, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0)], moving: true);
        for (int callback = 0; callback < 16 && !live.Scene.NativeParticleRequests.Any(request => request.InstanceIndex is not null); callback++)
        {
            live.Step();
        }
        Require(live.Scene.NativeParticleSlots.TryGetValue(0, out byte flag) && flag == 0x82 && live.Actors[7].NativeScriptMovement is { Active: true } && live.Actors[7].DisplayOffset == new System.Numerics.Vector3(101, -202, 303), "Live particle attachment fixture did not execute its binding and actual native fractional movement/display-offset producers.");
        JsonElement attached = live.Capture();
        EventEngine copy = CreateParticleSetFixture(vitals, economy, [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PARTICLEON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PARTICLESET, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0)], moving: true);
        copy.Restore(attached);
        NativeParticleAttachmentAnchor previous = live.ReadNativeParticleAttachment(0)!;
        for (int callback = 0; callback < 4; callback++)
        {
            live.Step();
            copy.Step();
            NativeScriptMovementState raw = live.Actors[7].NativeScriptMovement!;
            NativeParticleAttachmentAnchor anchor = live.ReadNativeParticleAttachment(0)!;
            Require(raw.FixedX < 0 && (raw.FixedX & 4095) != 0 && raw.FixedY < 0 && (raw.FixedY & 4095) != 0 && anchor.X == unchecked((short)(raw.FixedX / 4096)) && anchor.Y == unchecked((short)(raw.FixedY / 4096)) && anchor.Z == 32760 && anchor.Value == 0x82 && anchor.InstanceIndex == 2 && anchor.ObjectId == 7 && live.Scene.NativeParticleRequests.Count == 2 && JsonElement.DeepEquals(live.Capture(), copy.Capture()) && anchor == copy.ReadNativeParticleAttachment(0), "Particle attachment did not query the live dense body using signed division toward zero, included display offsets/bones, spawned another request or diverged after independent restore.");
        }
        Require(live.ReadNativeParticleAttachment(0) != previous, "Particle attachment retained issue coordinates instead of consuming the current raw body position.");
        foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
        {
            state => state["Scene"]!["NativeParticleRequests"]![1]!["InstanceIndex"] = 7,
            state => state["Scene"]!["NativeParticleRequests"]![1]!["InstanceIndex"] = null,
            state => state["Scene"]!["NativeParticleRequests"]![1]!["Value"] = 0x87,
            state => state["Scene"]!["NativeParticleSlots"]!["0"] = 0x87,
            state => state["Scene"]!["NativeParticleSlots"]!["0"] = 2,
            state => state["Scene"]!["NativeParticleSlots"]!.AsObject().Remove("0")
        })
        {
            JsonNode malformed = JsonNode.Parse(attached.GetRawText())!;
            corrupt(malformed);
            JsonElement before = live.Capture();
            try
            {
                live.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Malformed attached particle instance/byte provenance restored.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, live.Capture()), "Malformed particle attachment restore partially changed its destination.");
        }
        ExpectUnknownParticleAttachment(live, 1);

        EventEngine emitter = CreateParticleSetFixture(vitals, economy, [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PARTICLEON, 0)]);
        ReachParticleSetSuccessor(emitter);
        ExpectUnknownParticleAttachment(emitter, 0);
        NativeParticleRequest[] retainedHistory = live.Scene.NativeParticleRequests.ToArray();
        live.LoadField(601, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(live.Scene.NativeParticleSlots.Count == 0 && live.Scene.NativeParticleRequests.SequenceEqual(retainedHistory), "Field load invented particle zero flags or discarded executed prior-field binding history.");
        ExpectUnknownParticleAttachment(live, 0);
        JsonElement nextField = live.Capture();
        live.Restore(nextField);
        Require(JsonElement.DeepEquals(nextField, live.Capture()), "Prior-field particle binding history was incorrectly validated against a new field's body table.");

        foreach (bool duringInitialization in new[] { false, true })
        {
            EventEngine refused = CreateParticleSetFixture(vitals, economy, [(Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.PARTICLESET, 0)], preInitialization: duringInitialization);
            JsonElement? before = null;
            refused.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.NOP) && command.ObjectId == 7)
                {
                    before = refused.Capture();
                }
            };
            try
            {
                ReachParticleSetSuccessor(refused);
                throw new InvalidOperationException("PARTICLESET inferred an unknown slot or pre-finalization dense body index.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == nameof(Jsm.Opcode.PARTICLESET))
            {
            }
            JsonElement after = refused.Capture();
            Require(before is not null && refused.Scene.NativeParticleRequests.Count == 0 && refused.Scene.NativeParticleSlots.Count == 0 && JsonElement.DeepEquals(before.Value.GetProperty("Actors"), after.GetProperty("Actors")) && JsonElement.DeepEquals(before.Value.GetProperty("Fibers"), after.GetProperty("Fibers")) && JsonElement.DeepEquals(before.Value.GetProperty("Scene"), after.GetProperty("Scene")), "PARTICLESET unknown slot/initialization refusal changed typed operands, original PC, body or particle authority before validation.");
        }
        Console.WriteLine("PARTICLESET: real dense-byte binding, slot masking and ON/OFF overwrite, return2/opaque operands/RNG preservation, live raw attachment queries, independent restores, field invalidation and atomic unsupported ownership refusal pass; PMD simulation/rendering remain outside this component contract.");
    }

    private static EventEngine CreateParticleSetFixture(NativeCharacterVitals vitals, NativeEconomy economy, (Jsm.Opcode, int)[] commands, bool moving = false, bool preInitialization = false)
    {
        List<(Jsm.Opcode, int)[]> scripts = [];
        ushort[] groups = Enumerable.Range(0, 13).Select(index => checked((ushort)((index * 8 << 7) | 1))).ToArray();
        foreach (int index in Enumerable.Range(0, 13))
        {
            List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, index * 8)];
            if (new[] { 3, 6, 7, 8, 12 }.Contains(index))
            {
                init.Add((Jsm.Opcode.SETMODEL, index));
            }
            else
            {
                init.Add((Jsm.Opcode.UNUSE, 0));
            }
            if (index == 7)
            {
                init.AddRange([(Jsm.Opcode.PSHN_L, -19), (Jsm.Opcode.PSHN_L, -22), (Jsm.Opcode.PSHN_L, 32760), (Jsm.Opcode.SET3, 7), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.EFFECTLOAD, 0)]);
                if (preInitialization)
                {
                    init.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.NOP, 0), (Jsm.Opcode.PARTICLESET, 0)]);
                }
            }
            init.Add((Jsm.Opcode.RET, 8));
            scripts.Add(init.ToArray());
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, index * 8 + 1)];
            if (index == 7)
            {
                if (moving)
                {
                    main.AddRange([(Jsm.Opcode.PSHN_L, 101), (Jsm.Opcode.PSHN_L, -202), (Jsm.Opcode.PSHN_L, 303), (Jsm.Opcode.DOFFSET, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, -1000), (Jsm.Opcode.PSHN_L, -22), (Jsm.Opcode.PSHN_L, 32760), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0)]);
                }
                if (!moving)
                {
                    main.AddRange([(Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0)]);
                }
                main.AddRange(commands);
                if (!moving)
                {
                    main.Add((Jsm.Opcode.POPI_L, 7));
                }
                main.AddRange([(Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6000)]);
            }
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            scripts.Add(main.ToArray());
        }
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        EventEngine engine = new(session)
        {
            AnimationFrameCount = (_, _) => 4,
            LoadSoundResource = _ => SoundResourceStatus.Muted,
            CanUseNativeScriptMove = actor => actor.ObjectId == 7,
            ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 192, 0),
            ResolveNativeScriptMovement = query =>
            {
                int x = query.FixedX - 1377;
                int y = query.FixedY - 701;
                NativeScriptGroundProbe[] fan = Enumerable.Range(0, 3).Select(_ =>
                {
                    NativeScriptGroundProbe probe = new(x, y, query.FixedZ >> 12);
                    return probe with { ActorCollision = query.HasActorCollision!(probe) };
                }).ToArray();
                return new NativeScriptMovementResult(NativeScriptMovementStatus.Accepted, x, y, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(x, y, query.Direction, fan)]);
            }
        };
        engine.LoadField(600, SyntheticObjects(groups, scripts), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static void ReachParticleSetSuccessor(EventEngine engine)
    {
        for (int callback = 0; callback < 16 && !CapturedVariables(engine.Capture()).TryGetProperty("6000", out _); callback++)
        {
            engine.Step();
        }
        Require(CapturedVariables(engine.Capture()).TryGetProperty("6000", out _), "Particle component did not reach its immediate successor.");
    }

    private static JsonElement ParticleSetCaller(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 7 && fiber.GetProperty("ScriptId").GetInt32() == 57);

    private static void ExpectUnknownParticleAttachment(EventEngine engine, int slot)
    {
        JsonElement before = engine.Capture();
        try
        {
            _ = engine.ReadNativeParticleAttachment(slot);
            throw new InvalidOperationException("Particle consumer invented a disabled flag or an attachment for the deferred resource-emitter branch.");
        }
        catch (NotSupportedException)
        {
        }
        Require(JsonElement.DeepEquals(before, engine.Capture()), "Unsupported particle consumer query mutated field state.");
    }
}
