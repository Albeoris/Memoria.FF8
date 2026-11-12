using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCurrentFieldPartyBindingContract(string gameDirectory)
    {
        JsonNode fixture = JsonSerializer.SerializeToNode(new SceneEventState())!;
        fixture["Party"] = new JsonArray(0, 255, 5);
        fixture["NativeActorLayoutVersion"] = 1;
        fixture["NativeActorObjectIds"] = new JsonArray(7, 2, 12);
        fixture["FieldPartyNativeActorIds"] = new JsonArray(2, 255, 0);
        SceneEventState scene = fixture.Deserialize<SceneEventState>()!;
        ActorState[] actors = [new(2, 16), new(12, 24), new(7, 8)];
        Require(NativeFieldPartyBinding.TryGetObjectId(scene, 0, out int leader) && leader == 12 && NativeFieldPartyBinding.ResolveActor(scene, actors, 0) == actors[1], "Current runtime leader was confused with canonical character or compact actor index.");
        Require(!NativeFieldPartyBinding.TryGetObjectId(scene, 1, out _) && NativeFieldPartyBinding.TryGetObjectId(scene, 2, out int follower) && follower == 7, "Sparse runtime follower slots were treated as a prefix.");
        scene.Party.Clear();
        scene.Party.AddRange([8, 9, 10]);
        Require(NativeFieldPartyBinding.ResolveActor(scene, actors, 0) == actors[1], "Changing global identities changed an existing current-field body binding.");
        scene.FieldPartyNativeActorIds[1] = 2;
        Require(!NativeFieldPartyBinding.TryGetObjectId(scene, 0, out _), "Duplicate runtime body bindings were admitted.");
        scene.FieldPartyNativeActorIds[1] = -1;
        Require(!NativeFieldPartyBinding.TryGetObjectId(scene, 0, out _), "A negative native actor index was admitted.");
        scene.FieldPartyNativeActorIds[1] = 255;
        Require(NativeFieldPartyBinding.ResolveActor(scene, [actors[1], actors[1]], 0) is null, "Duplicate actual actor objects were admitted.");
        scene.NativeActorObjectIds[0] = 12;
        Require(!NativeFieldPartyBinding.TryGetObjectId(scene, 0, out _), "Duplicate compact object IDs were admitted.");

        (Jsm.Opcode, int)[] Actor(int label, int character, int x) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] binding = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.INITTRACE, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([1, (8 << 7) | 1, (16 << 7) | 1, (24 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], binding, Actor(8, 8, 88), [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Actor(16, 9, 99), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Actor(24, 0, 11), [(Jsm.Opcode.LBL, 25), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        using GameAssets assets = new(gameDirectory);
        GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
        engine.Scene.Party.Clear();
        engine.Scene.Party.AddRange([8, 9, 0]);
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        Require(engine.LastFault is null && engine.PlayerActor is { CharacterId: 8, X: 88 } && engine.Scene.Party.SequenceEqual([0, 1, 5]), "Executed SETPARTY replaced the current runtime leader before REFRESHPARTY.");
        Require(engine.Scene.PartyTrace is { } trace && trace.FirstTrail.All(sample => sample is { X: 88 }), "INITTRACE still selected the active character0 body instead of the retained runtime leader.");

        FieldFixedPosition oldPosition = new(-22866941, -19661416, -4251648);
        JsonNode beforeRebind = JsonNode.Parse(engine.Capture().GetRawText())!;
        beforeRebind["UserControlEnabled"] = false;
        beforeRebind["Scene"]!["NativePartyTraceVersion"] = 1;
        beforeRebind["Scene"]!["NativePlayerPosition"] = JsonSerializer.SerializeToNode(oldPosition);
        beforeRebind["Actors"]![1]!["X"] = oldPosition.X / 4096f;
        beforeRebind["Actors"]![1]!["Y"] = oldPosition.Y / 4096f;
        beforeRebind["Actors"]![1]!["Z"] = oldPosition.Z / 4096f;
        engine.Restore(JsonSerializer.SerializeToElement(beforeRebind));
        ActorState previous = engine.PlayerActor!;
        Require(previous.ObjectId == 1 && engine.Scene.NativePlayerPosition == oldPosition, "The pre-rebind checkpoint lost the original native leader.");
        NativeReleasedPlayerPlacement formerPlacement = new(oldPosition, previous.WalkmeshTriangle);
        engine.Step();
        engine.Step();
        Require(engine.LastFault is null && engine.PlayerActor is { ObjectId: 3 } && engine.Scene.NativePlayerPosition == new FieldFixedPosition(11 * 4096, 0, 0), "REFRESHPARTY did not project the incoming leader's own position.");
        Require(engine.Actors[1].NativeReleasedPlayerPosition == formerPlacement, $"REFRESHPARTY discarded the former leader's fractional coordinates: {engine.Actors[1].NativeReleasedPlayerPosition} instead of {formerPlacement}.");
        engine.Restore(engine.Capture());
        Require(engine.Actors[1].NativeReleasedPlayerPosition == formerPlacement, "Checkpoint restore changed the former leader's raw coordinates.");
    }
}
