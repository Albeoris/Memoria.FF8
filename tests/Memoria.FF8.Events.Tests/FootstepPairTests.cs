using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFootstepPairContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeFootstepCutContract(vitals, economy);
        byte[] Content(bool direct = true, bool initializationCopy = false)
        {
            List<(Jsm.Opcode Opcode, int Parameter)> callerInit = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.FOOTSTEPON, 0)];
            if (direct)
            {
                callerInit.AddRange([(Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.FOOTSTEP, 257)]);
            }
            if (initializationCopy)
            {
                callerInit.Add((Jsm.Opcode.FOOTSTEPCOPY, 0));
            }
            callerInit.Add((Jsm.Opcode.RET, 0));
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.FOOTSTEPCOPY, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] hiddenInit = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] hidden = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HIDE, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] inactiveInit = initializationCopy ? [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)] : [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.FOOTSTEPON, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] inactive = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 0)];
            return SyntheticObjects([1, (16 << 7) | 1, (32 << 7) | 1], [callerInit.ToArray(), caller, hiddenInit, hidden, inactiveInit, inactive]);
        }
        (GameSession Session, EventEngine Engine) Create(byte[] content, bool authenticated = true)
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(600, content, [], nativeRuntimeProfile: authenticated ? NativeFieldRuntimeProfile.FF8En : null);
            return (session, engine);
        }
        byte[] content = Content();
        var live = Create(content);
        live.Engine.Step();
        Require(live.Engine.Actors[0].NativeFootstepPair is { First392: 255, Second393: 1, Source.RawValue: 511, Source.RawParameter: 257 } && live.Engine.Actors[1].NativeFootstepPair is { First392: 255, Second393: 255, Source.Kind: "Allocation" }, "FOOTSTEP lost independent operand/immediate BYTE narrowing or allocation FF/FF.");
        live.Engine.Step();
        Require(!live.Engine.Actors[1].Visible && !live.Engine.Actors[2].Active && live.Engine.Scene.NativeActorObjectIds.SequenceEqual([0, 1, 2]), "Synthetic copy fixture did not retain hidden/inactive dense native records.");
        JsonElement beforeCopy = live.Engine.Capture();
        var restored = Create(content);
        restored.Engine.Restore(beforeCopy);
        int copyOperands = -1;
        live.Engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.FOOTSTEPCOPY))
            {
                copyOperands = command.Arguments.Count;
            }
        };
        live.Engine.Step();
        restored.Engine.Step();
        Require(copyOperands == 0 && live.Session.State.Variables[500] == 9 && live.Engine.Actors.All(actor => actor.NativeFootstepPair is { First392: 255, Second393: 1, Copy.ActorLayoutVersion: 1 }) && live.Engine.Actors.All(actor => actor.NativeFootstepPair!.Copy!.Targets.SequenceEqual([0, 1, 2])), "FOOTSTEPCOPY consumed operands, yielded, filtered targets or lost the captured pair.");
        Require(live.Engine.Actors[0].FootstepsEnabled && !live.Engine.Actors[1].FootstepsEnabled && live.Engine.Actors[2].FootstepsEnabled, "FOOTSTEPCOPY copied the independent actor352 mask80 flag.");
        Require(live.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && !fiber.GetProperty("Initialization").GetBoolean()).GetProperty("Results").GetProperty("3").GetInt32() == 77 && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Zero-operand copy lost the stack sentinel or independent restored progression.");
        JsonElement copied = live.Engine.Capture();
        foreach (string malformedPart in new[] { "pair", "target", "source" })
        {
            JsonNode malformed = JsonNode.Parse(copied.GetRawText())!;
            JsonNode pair = malformed["Actors"]![1]!["NativeFootstepPair"]!;
            if (malformedPart == "pair")
            {
                pair["Second393"] = 2;
            }
            else if (malformedPart == "target")
            {
                pair["Copy"]!["Targets"]![0] = 2;
            }
            else
            {
                pair["Source"]!["RawParameter"] = 258;
            }
            try
            {
                live.Engine.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Malformed footstep byte/copy authority was restored.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(copied, live.Engine.Capture()), "Footstep checkpoint refusal mutated the current VM.");
        }

        var allocation = Create(Content(false, true));
        allocation.Engine.Step();
        Require(allocation.Engine.Scene.NativeActorObjectIds.SequenceEqual([0, 1]) && allocation.Engine.Actors.All(actor => actor.NativeFootstepPair is { First392: 255, Second393: 255, Copy.ActorLayoutVersion: 0 }), "Initialization copy omitted an allocated actor before compaction or invented source bytes.");
        var allocationRestored = Create(Content(false, true));
        allocationRestored.Engine.Restore(allocation.Engine.Capture());
        Require(JsonElement.DeepEquals(allocation.Engine.Capture(), allocationRestored.Engine.Capture()), "Precompaction footstep copy targets failed independent restore.");

        var unknown = Create(Content(false));
        unknown.Engine.Step();
        unknown.Engine.Step();
        JsonNode unknownSource = JsonNode.Parse(unknown.Engine.Capture().GetRawText())!;
        unknownSource["Actors"]![0]!.AsObject().Remove("NativeFootstepPair");
        unknown.Engine.Restore(JsonSerializer.SerializeToElement(unknownSource));
        try
        {
            unknown.Engine.Step();
            throw new InvalidOperationException("Unknown source pair acquired allocation defaults during copy.");
        }
        catch (EventScriptException) when (unknown.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.FOOTSTEPCOPY))
        {
        }
        Require(unknown.Engine.Actors[0].NativeFootstepPair is null && unknown.Engine.Actors[1].NativeFootstepPair?.Copy is null && unknown.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && !fiber.GetProperty("Initialization").GetBoolean()).GetProperty("Stack")[0].GetInt32() == 77, "Unknown-pair refusal seeded bytes, changed targets or consumed a sentinel.");
        live.Engine.LoadField(601, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(live.Engine.Actors[0].NativeFootstepPair is { First392: 255, Second393: 255, Copy: null, Source.Kind: "Allocation", Source.FieldId: 601 }, "New native allocation retained another field's footstep pair/copy.");
        byte[] noActors = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.FOOTSTEPCOPY, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.RET, 0));
        noActors[2] = 1;
        noActors[3] = 0;
        var empty = Create(noActors);
        empty.Engine.Step();
        Require(empty.Session.State.Variables[500] == 9 && empty.Engine.Scene.NativeActorObjectIds.Count == 0 && empty.Engine.Actors[0].NativeFootstepPair is null, "Zero native actor count read an unknown module pair or introduced copy writes/wait.");
    }

    private static void NativeFootstepCutContract(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        byte[] content = SyntheticObjects([1, 16 << 7],
        [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.FOOTSTEPON, 0), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.FOOTSTEP, 34), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.FOOTSTEPCUT, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.FOOTSTEPCOPY, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 500), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)],
            [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.PSHN_L, 56), (Jsm.Opcode.FOOTSTEP, 78), (Jsm.Opcode.RET, 0)]
        ]);
        (GameSession Session, EventEngine Engine) Create()
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var live = Create();
        live.Engine.Step();
        Require(live.Engine.Actors[0].NativeFootstepPair is { First392: 12, Second393: 34 } && live.Engine.Actors[1].NativeFootstepPair is { First392: 56, Second393: 78 }, "Cut fixture did not establish differing executed pairs.");
        var restored = Create();
        restored.Engine.Restore(live.Engine.Capture());
        bool observed = false;
        live.Engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.FOOTSTEPCUT))
            {
                observed = true;
                Require(command.Arguments.Count == 0 && live.Engine.Actors[0].NativeFootstepPair is { First392: 255, Second393: 255, Source.Kind: "FOOTSTEPCUT", Copy: null } && live.Engine.Actors[1].NativeFootstepPair is { First392: 56, Second393: 78 } && live.Engine.Actors[0].FootstepsEnabled && !live.Engine.Actors[1].FootstepsEnabled, "FOOTSTEPCUT consumed a stack operand, touched another actor, changed mask80 or failed FF/FF writes.");
            }
        };
        live.Engine.Step();
        restored.Engine.Step();
        Require(observed && live.Session.State.Variables[500] == 9 && live.Engine.Actors.All(actor => actor.NativeFootstepPair is { First392: 255, Second393: 255, Source.Kind: "FOOTSTEPCUT", Copy: not null }) && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Executed CUT source did not support immediate COPY or exact restored progression.");
        Require(live.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && !fiber.GetProperty("Initialization").GetBoolean()).GetProperty("Results").GetProperty("3").GetInt32() == 77, "FOOTSTEPCUT did not preserve the stack sentinel.");
        foreach (string part in new[] { "byte", "raw", "site" })
        {
            JsonNode invalid = JsonNode.Parse(live.Engine.Capture().GetRawText())!;
            JsonNode pair = invalid["Actors"]![0]!["NativeFootstepPair"]!;
            if (part == "byte")
            {
                pair["Second393"] = 0;
            }
            else if (part == "raw")
            {
                pair["Source"]!["RawValue"] = 255;
            }
            else
            {
                pair["Source"]!["Operation"] = 0;
            }
            JsonElement before = restored.Engine.Capture();
            try
            {
                restored.Engine.Restore(JsonSerializer.SerializeToElement(invalid));
                throw new InvalidOperationException("Malformed CUT receipt passed restore.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, restored.Engine.Capture()), "Rejected CUT receipt mutated the receiver.");
        }
    }
}
