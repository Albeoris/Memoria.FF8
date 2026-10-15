using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFaceSyncContract()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        byte[] content = SyntheticObjects([2, (8 << 7) | 1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.FACEDIRINIT, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.FACEDIRLIMIT, 0), (Jsm.Opcode.PSHN_L, 511), (Jsm.Opcode.EFFECTLOAD, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.FACEDIR, 0), (Jsm.Opcode.PSHN_L, 70), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.FACEDIRSYNC, 0), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, 71), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create(byte[] program)
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                ReadFaceBaseline = _ => new FaceDirectionBaseline(0, 0),
                ReadFaceAnchorHeight = _ => 0,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY)
            };
            engine.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create(content);
        JsonElement SyncFiber(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 2);
        while (!live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("WaitReason").GetString() == "NativeFaceSync") && live.Frame < 10)
        {
            live.Step();
        }
        ActorState actor = live.Actors[0];
        JsonElement waiting = live.Capture();
        JsonElement held = SyncFiber(live);
        int syncOperation = JsmProgram.Read(content).Operations.Single(operation => operation.Opcode == Jsm.Opcode.FACEDIRSYNC).Index;
        Require(actor.FaceProgress > 0 && actor.FaceProgress < actor.FaceDuration && held.GetProperty("Position").GetInt32() == syncOperation && held.GetProperty("Stack").GetArrayLength() == 1 && held.GetProperty("Stack")[0].GetProperty("ActorInfoOrigin").GetProperty("NativeActorIndex").GetInt32() == 255 && !waiting.GetProperty("Variables").TryGetProperty("5001", out _), "FACEDIRSYNC consumed the unrelated unknown scalar, advanced its retry PC or declared completion before WORD equality.");
        EventEngine restored = Create(content);
        restored.Restore(waiting);
        while (actor.FaceProgress < actor.FaceDuration)
        {
            int previous = actor.FaceProgress;
            live.Step();
            restored.Step();
            Require(actor.FaceProgress == previous + 1 && SyncFiber(live).GetProperty("Position").GetInt32() == syncOperation && !live.Capture().GetProperty("Variables").TryGetProperty("5001", out _) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Face sync/cosine phase changed after restore or completed before the VM observed its post-body progress.");
        }
        JsonElement completedBody = live.Capture();
        EventEngine completedCopy = Create(content);
        completedCopy.Restore(completedBody);
        live.Step();
        restored.Step();
        completedCopy.Step();
        Require(live.Capture().GetProperty("Variables").GetProperty("5001").GetInt32() == 71 && SyncFiber(live).GetProperty("Stack").GetArrayLength() == 0 && SyncFiber(live).GetProperty("Results").GetProperty("7").GetProperty("ActorInfoOrigin").GetProperty("NativeActorIndex").GetInt32() == 255 && JsonElement.DeepEquals(live.Capture(), restored.Capture()) && JsonElement.DeepEquals(live.Capture(), completedCopy.Capture()), "FACEDIRSYNC equality inserted another callback, discarded the opaque scalar or diverged across a completion-boundary restore.");
        Require(actor.FaceDuration == 8 && actor.FaceProgress == 8 && actor.Direction == 0 && actor.X == 0 && actor.Y == 0 && actor.Z == 0 && actor.FirstControlRotation is not null, "FACEDIRSYNC changed face words, physical pose/facing or the completed joint override.");
        JsonNode malformed = JsonNode.Parse(waiting.GetRawText())!;
        JsonObject malformedFiber = malformed["Fibers"]!.AsArray().Select(node => node!.AsObject()).Single(fiber => fiber["ScriptId"]!.GetValue<int>() == 2);
        malformedFiber["Position"] = syncOperation + 1;
        JsonElement before = restored.Capture();
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Face sync wait restored without its original opcode PC.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, restored.Capture()), "Malformed face sync restore changed VM or native owners.");

        byte[] zero = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.FACEDIRSYNC, 0), (Jsm.Opcode.POPM_B, 5002), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine idle = Create(zero);
        idle.ReadFaceBaseline = null;
        idle.ReadFaceAnchorHeight = null;
        idle.ResolveNativeActorFacing = null;
        idle.Step();
        idle.Step();
        Require(idle.Actors[0].FaceProgress == 0 && idle.Actors[0].FaceDuration == 0 && idle.Actors[0].FirstControlRotation is null && idle.Capture().GetProperty("Variables").GetProperty("5002").GetInt32() == 77, "Fresh allocated zero face words required unused model queries, consumed the unrelated value or inserted a wait.");
        EventEngine unknown = Create(zero);
        unknown.Step();
        // Fault injection removes only the face owner after genuine native initialization.
        typeof(ActorState).GetProperty(nameof(ActorState.FaceStateVersion))!.SetValue(unknown.Actors[0], 0);
        try
        {
            unknown.Step();
            throw new InvalidOperationException("Face sync inferred native completion without allocation provenance.");
        }
        catch (EventScriptException) when (unknown.LastFault?.Opcode == "FACEDIRSYNC")
        {
        }
        JsonElement refused = unknown.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(refused.GetProperty("Stack")[0].GetInt32() == 77 && refused.GetProperty("Position").GetInt32() == unknown.LastFault!.Operation && !unknown.Capture().GetProperty("Variables").TryGetProperty("5002", out _), "Unknown face-word admission consumed stack or advanced original instruction.");
    }
}
