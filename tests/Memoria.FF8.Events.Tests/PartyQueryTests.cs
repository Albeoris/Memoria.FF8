using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyQueryContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        Dictionary<EventEngine, GameSession> sessions = [];
        EventEngine Create(byte[] content, int[] party, NativeCharacterVitals? vitals = null)
        {
            GameSession session = new(new GameState { CharacterVitals = vitals });
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(600, content, []);
            engine.Scene.Party.Clear();
            engine.Scene.Party.AddRange(party);
            sessions[engine] = session;
            return engine;
        }
        byte[] query = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 640), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 641), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 642), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.ISPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 643), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ISPARTY, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_B, 644), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8));
        EventEngine live = Create(query, [0, 255, 5]);
        live.Step();
        JsonElement pending = live.Capture();
        EventEngine copy = Create(query, [7, 255, 255]);
        copy.Restore(pending);
        for (int tick = 0; tick < 8; tick++)
        {
            live.Step();
            copy.Step();
            Require(JsonElement.DeepEquals(live.Capture(), copy.Capture()), "Sparse GETPARTY/ISPARTY continuation diverged after independent restore.");
        }
        Require(live.Scene.Party.SequenceEqual([0, 255, 5]) && live.Scene.ActiveParty.SequenceEqual([0, 5]) && sessions[live].State.Variables[640] == 0 && sessions[live].State.Variables[641] == 255 && sessions[live].State.Variables[642] == 5 && sessions[live].State.Variables[643] == 1 && sessions[live].State.Variables[644] == 2, "Native party queries compressed an FF hole or changed raw slot identity.");
        live.LoadField(601, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)), []);
        Require(live.Scene.Party.SequenceEqual([0, 255, 5]), "Field load compressed sparse character slots.");
        foreach (int[] packet in new[] { new[] { 0, 255, 5 }, new[] { 255, 0, 255 }, new[] { 255, 255, 255 } })
        {
            byte[] write = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, packet[0]), (Jsm.Opcode.PSHN_L, packet[1]), (Jsm.Opcode.PSHN_L, packet[2]), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.RET, 8));
            EventEngine written = Create(write, [0, 1, 5], initial);
            written.Step();
            Require(written.Scene.Party.SequenceEqual(packet) && sessions[written].CharacterVitals!.CombatParty.Select(value => (int)value).SequenceEqual(new[] { packet[1], packet[0], packet[2] }), "SETPARTY lost FF bytes or native combat order1,0,2.");
        }
        byte[] remove = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 257), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.RET, 8));
        EventEngine removed = Create(remove, [0, 1, 5], initial.WithMemberFlags(1, true).WithMemberFlags(5, true) with { CombatParty = [1, 0, 5] });
        removed.Step();
        Require(removed.Scene.Party.SequenceEqual([0, 255, 5]) && removed.Scene.FieldPartyNativeActorIds.Count == 0 && removed.NormalMembers!.SetEquals([0, 1, 5]) && sessions[removed].CharacterVitals!.CombatParty.Select(value => (int)value).SequenceEqual(new[] { 255, 0, 5 }), "SUBPARTY did not remove only its lowBYTE-matched field/combat slot or implicitly changed bindings/membership.");
        EventEngine resumed = Create(remove, [255, 255, 255]);
        resumed.Restore(removed.Capture());
        for (int tick = 0; tick < 6; tick++)
        {
            removed.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(removed.Capture(), resumed.Capture()), "Sparse removal/firstFF addition diverged after restore.");
        }
        Require(removed.Scene.Party.SequenceEqual([0, 3, 5]) && sessions[removed].CharacterVitals!.CombatParty.Select(value => (int)value).SequenceEqual(new[] { 3, 0, 5 }), "ADDPARTY appended instead of filling the original first FF independently in field/combat slots.");
        JsonElement valid = copy.Capture();
        foreach (JsonArray malformed in new[] { new JsonArray(0, 5), new JsonArray(0, 0, 255), new JsonArray(0, 256, 255) })
        {
            JsonNode damaged = JsonNode.Parse(valid.GetRawText())!;
            damaged["Scene"]!["Party"] = malformed;
            try
            {
                copy.Restore(JsonSerializer.SerializeToElement(damaged));
                throw new InvalidOperationException("Malformed or compressed party snapshot was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(valid, copy.Capture()), "Invalid party restore mutated VM/services.");
        }
        JsonNode missingParty = JsonNode.Parse(valid.GetRawText())!;
        missingParty["Scene"]!.AsObject().Remove("Party");
        try
        {
            copy.Restore(JsonSerializer.SerializeToElement(missingParty));
            throw new InvalidOperationException("Missing party bytes inferred constructor defaults during restore.");
        }
        catch (JsonException)
        {
        }
        Require(JsonElement.DeepEquals(valid, copy.Capture()), "Missing party refusal mutated VM/services.");
        foreach (int slot in new[] { -1, 3, 256, 65536 })
        {
            EventEngine refused = Create(SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.GETPARTY, 0), (Jsm.Opcode.RET, 8)), [0, 255, 5]);
            try
            {
                refused.Step();
                throw new InvalidOperationException("Out-of-range native party query was accepted.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == "GETPARTY")
            {
            }
            Require(refused.Capture().GetProperty("Fibers")[0].GetProperty("Stack")[0].GetInt32() == slot && refused.Scene.Party.SequenceEqual([0, 255, 5]), "Rejected GETPARTY consumed operands or compressed slots.");
        }
        NativeSparsePartyRequestContract(initial);
        NativePartyRequestPreemptionContract(initial);
        Console.WriteLine("Native raw3 party: sparse GET/ISPARTY, allFF/leadingFF SETPARTY, combat1,0,2, lowBYTE SUBPARTY, firstFF ADDPARTY, independent restore/field persistence and malformed snapshot atomic refusal passed.");
    }

    private static void NativeSparsePartyRequestContract(NativeCharacterVitals initial)
    {
        Dictionary<EventEngine, GameSession> sessions = [];
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.PREQ, Jsm.Opcode.PREQSW, Jsm.Opcode.PREQEW })
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (opcode, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 700), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (opcode, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 701), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] content = SyntheticObjects([1, (8 << 7) | 1, (16 << 7) | 2], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.RET, 8)], caller, [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 5), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 702), (Jsm.Opcode.RET, 8)]]);
            EventEngine Create()
            {
                GameSession session = new(new GameState { CharacterVitals = initial.DeepCopy() });
                EventEngine engine = new(session, session.State.Variables) { AnimationFrameCount = (_, _) => 1 };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                sessions[engine] = session;
                return engine;
            }
            EventEngine live = Create();
            bool issued = false;
            live.CommandExecuted += command =>
            {
                if (command.Name == opcode.ToString() && command.Arguments.Count == 2 && !issued)
                {
                    issued = true;
                    Require(!sessions[live].State.Variables.ContainsKey(700) && live.Scene.FieldPartyNativeActorIds[2] == 255 && live.Scene.PartyBindingMemory?.Third == 2, "Native PREQ did not preserve return3 yield or detached saved body identity.");
                }
            };
            while (!issued && live.Frame < 12)
            {
                live.Step();
            }
            Require(issued && sessions[live].State.Variables[702] == 1 && !sessions[live].State.Variables.ContainsKey(700), "PREQ used runtime FOLLOW slots instead of saved B194 pointers or continued its caller within return3 budget.");
            EventEngine resumed = Create();
            resumed.Restore(live.Capture());
            for (int tick = 0; tick < 8; tick++)
            {
                live.Step();
                resumed.Step();
                Require(JsonElement.DeepEquals(live.Capture(), resumed.Capture()), "Sparse PREQ continuation diverged after restore.");
            }
            Require(sessions[live].State.Variables[700] == 1 && sessions[live].State.Variables[701] == 1 && live.Scene.Party.SequenceEqual([0, 255, 5]), "Absent PREQ slot did not consume/drop and continue after its original yield.");
        }
    }

    private static void NativePartyRequestPreemptionContract(NativeCharacterVitals initial)
    {
        foreach (bool moving in new[] { true, false })
        {
            List<(Jsm.Opcode, int)> controller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0)];
            if (!moving)
            {
                controller.AddRange([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.REQ, 2)]);
            }
            controller.AddRange([(Jsm.Opcode.PSHN_L, moving ? 3 : 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PREQEW, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_B, 710), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            (Jsm.Opcode, int)[] move = [(Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0)];
            List<(Jsm.Opcode, int)> lower = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.FOLLOWOFF, 0)];
            lower.AddRange(moving ? move : [(Jsm.Opcode.HALT, 0)]);
            lower.Add((Jsm.Opcode.RET, 8));
            List<(Jsm.Opcode, int)> upper = [(Jsm.Opcode.LBL, 20)];
            upper.AddRange(move);
            upper.AddRange([(Jsm.Opcode.RANIMEKEEP, 3), (Jsm.Opcode.ANIMESYNC, 0), (Jsm.Opcode.RET, 8)]);
            byte[] content = SyntheticObjects([1, (8 << 7) | 1, (16 << 7) | 4], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.RET, 8)], controller.ToArray(), [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 5), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)], lower.ToArray(), [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)], upper.ToArray()]);
            EventEngine Create()
            {
                GameSession session = new(new GameState { CharacterVitals = initial.DeepCopy() });
                EventEngine engine = new(session, session.State.Variables)
                {
                    AnimationFrameCount = (_, _) => 30,
                    CanUseNativeScriptMove = actor => actor.ObjectId == 2,
                    ResolveNativeScriptDirection = query => !moving && query.FixedX >= 2 * 4096 ? new(NativeScriptDirectionStatus.Snapped, query.TargetX, query.TargetY, query.MovementDirection, query.TurnAccumulator) : new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, query.TurnAccumulator),
                    ResolveNativeScriptMovement = query =>
                    {
                        int x = query.FixedX + 4096;
                        NativeScriptGroundProbe fan = new(x, query.FixedY, query.FixedZ >> 12);
                        return new NativeScriptMovementResult(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, [fan, fan, fan])]);
                    }
                };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create();
            bool issued = false;
            live.CommandExecuted += command => issued |= command.Name == "PREQEW";
            while (!issued && live.Frame < 15)
            {
                live.Step();
            }
            Require(issued && (moving ? live.Actors[2].NativeMovementRequest is { RequestSlot: 1, SavedMovement: { Active: true } } && live.Actors[2].NativeScriptMovement is { Suspended: true } : live.Actors[2].NativeMovementRequest is null && live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => !fiber.GetProperty("Complete").GetBoolean() && fiber.GetProperty("ScriptId").GetInt32() == 20 && fiber.GetProperty("WaitReason").GetString() == "Animation")), "PREQEW did not use its shared movement backup or queue below the existing native animation slot.");
            EventEngine resumed = Create();
            resumed.Restore(live.Capture());
            for (int tick = 0; tick < 40; tick++)
            {
                live.Step();
                resumed.Step();
                Require(JsonElement.DeepEquals(live.Capture(), resumed.Capture()), "Native party request above MOVE/below animation diverged after restore.");
            }
            Require(live.Capture().GetProperty("Variables").GetProperty("710").GetInt32() == 1 && live.Actors[2].NativeMovementRequest is null && (!moving || live.Actors[2].NativeScriptMovement is { Suspended: false, Active: true }), "Native PREQEW did not complete its actual queued request or restore lower MOVE ownership.");
        }
    }
}
