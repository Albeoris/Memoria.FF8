using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeAnimationBackupContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.RANIME, Jsm.Opcode.RANIMELOOP, Jsm.Opcode.RANIMEKEEP })
        {
            EventEngine live = CreateNativeAnimationBackupFixture(vitals, economy, [(Jsm.Opcode.PSHN_L, 77), (opcode, 2), (Jsm.Opcode.POPM_B, 5000)]);
            Require(live.Actors[0].NativeAnimationHeader == new NativeActorAnimationHeader(1, 255, 0, 16, 0, 0, 0, null) && live.Actors[0].NativeAnimationBackup == new NativeActorAnimationBackup(1, 0, 0, 0, 0, 0, 0), "Fresh native animation allocation lost590FF,52016 or the independently zeroed physical backup.");
            ushort mode = opcode == Jsm.Opcode.RANIME ? (ushort)0x4000 : opcode == Jsm.Opcode.RANIMELOOP ? (ushort)0x2000 : (ushort)0x8000;
            AdvanceToNativeAnimationIssue(live, 2, mode);
            RequireNativeAnimationWords(live.Actors[0], 2, 0, 16, 0, 48, mode);
            Require(live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([77]), "Native animation selection consumed an unrelated operand.");
            EventEngine restored = CreateNativeAnimationBackupFixture(vitals, economy, [(Jsm.Opcode.PSHN_L, 77), (opcode, 2), (Jsm.Opcode.POPM_B, 5000)]);
            restored.Restore(live.Capture());
            (int Clip, int Frame, int Terminal, int Flags)[] expected = opcode switch
            {
                Jsm.Opcode.RANIME => [(2, 16, 48, 0x4000), (2, 32, 48, 0x4000), (0, 0, 48, 0x2800), (0, 16, 48, 0x2000), (0, 32, 48, 0x2000), (0, 0, 48, 0x2800)],
                Jsm.Opcode.RANIMELOOP => [(2, 16, 48, 0x2000), (2, 32, 48, 0x2000), (2, 0, 48, 0x2800), (2, 16, 48, 0x2000)],
                _ => [(2, 16, 48, 0x8000), (2, 32, 48, 0x8000), (2, 32, 32, 0x1800), (2, 32, 32, 0x1800)]
            };
            foreach (var sample in expected)
            {
                live.Step();
                restored.Step();
                RequireNativeAnimationWords(live.Actors[0], sample.Clip, sample.Frame, 16, 0, sample.Terminal, sample.Flags);
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Native animation physical words, completion flags or renderer frame diverged after independent restoration.");
            }
        }
        NativeAnimationSpeedWords(vitals, economy);
        NativeAnimationRangeWords(vitals, economy);
        NativeAnimationBlockingAndSyncClock(vitals, economy);
        NativeAnimationSingleBackup(vitals, economy);
        NativeAnimationFreshPopAndOpaqueRefusal(vitals, economy);
        Console.WriteLine("Native animation: physical full/ranged frame words, return-idle/loop/keep flags, zero/wrapped speed, one overwritten backup slot, repeated immediate POP, current binding/flags and independent restore pass.");
    }

    private static void NativeAnimationSpeedWords(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        foreach (int speed in new[] { 0, 98305 })
        {
            (Jsm.Opcode, int)[] operations = [(Jsm.Opcode.PSHN_L, speed), (Jsm.Opcode.ANIMESPEED, 0), (Jsm.Opcode.RANIMELOOP, 2)];
            EventEngine live = CreateNativeAnimationBackupFixture(vitals, economy, operations);
            AdvanceToNativeAnimationIssue(live, 2, 0x2000);
            EventEngine restored = CreateNativeAnimationBackupFixture(vitals, economy, operations);
            restored.Restore(live.Capture());
            int[] frames = speed == 0 ? [0, 0, 0] : [32769, 2, 32771];
            foreach (int frame in frames)
            {
                live.Step();
                restored.Step();
                RequireNativeAnimationWords(live.Actors[0], 2, frame, unchecked((ushort)speed), 0, 48, 0x2000);
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "ANIMESPEED zero/lowWORD addition or signed terminal comparison diverged after restore.");
            }
        }
    }

    private static void NativeAnimationRangeWords(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        (Jsm.Opcode, int)[] operations = [(Jsm.Opcode.PSHN_L, 65578), (Jsm.Opcode.PSHN_L, 65560), (Jsm.Opcode.RCANIMEKEEP, 6)];
        EventEngine live = CreateNativeAnimationBackupFixture(vitals, economy, operations);
        AdvanceToNativeAnimationIssue(live, 6, 0x8000);
        RequireNativeAnimationWords(live.Actors[0], 6, 368, 16, 368, 656, 0x8000);
        EventEngine restored = CreateNativeAnimationBackupFixture(vitals, economy, operations);
        restored.Restore(live.Capture());
        for (int sample = 1; sample <= 19; sample++)
        {
            live.Step();
            restored.Step();
            RequireNativeAnimationWords(live.Actors[0], 6, sample < 18 ? 368 + 16 * sample : 640, 16, 368, sample < 18 ? 656 : 640, sample < 18 ? 0x8000 : 0x1800);
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Ranged animation lost signed lowWORD1-based frame conversion, terminal−increment keep phase or independent restore.");
        }
    }

    private static void NativeAnimationBlockingAndSyncClock(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        (Jsm.Opcode, int)[] operations = [(Jsm.Opcode.ANIME, 2), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.ANIMESYNC, 0), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_B, 5001)];
        EventEngine live = CreateNativeAnimationBackupFixture(vitals, economy, operations);
        AdvanceToNativeAnimationIssue(live, 2, 0x4000);
        long issued = live.Frame;
        EventEngine restored = CreateNativeAnimationBackupFixture(vitals, economy, operations);
        restored.Restore(live.Capture());
        for (int callback = 1; callback <= 3; callback++)
        {
            live.Step();
            restored.Step();
            RequireNativeAnimationWords(live.Actors[0], callback < 3 ? 2 : 0, callback < 3 ? 16 * callback : 0, 16, 0, 48, callback < 3 ? 0x4000 : 0x2800);
            Require(live.Frame == issued + callback && !live.Capture().GetProperty("Variables").TryGetProperty("5000", out _) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Blocking ANIME resumed before its third physical sample or executed a successor on its native return3 completion callback.");
        }
        for (int callback = 4; callback <= 6; callback++)
        {
            live.Step();
            restored.Step();
            JsonElement variables = live.Capture().GetProperty("Variables");
            Require(variables.GetProperty("5000").GetInt32() == 77 && variables.TryGetProperty("5001", out JsonElement syncSuccessor) == (callback == 6) && (callback != 6 || syncSuccessor.GetInt32() == 88) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "ANIME did not release its successor on the following callback, or ANIMESYNC inserted a yield after its native return2 completion.");
        }
        Require(live.Frame == issued + 6 && live.Actors[0].NativeAnimationHeader!.Flags == 0x2800, "Native blocking/SYNC clock did not preserve the independent three-frame idle completion phase.");
    }

    private static void NativeAnimationSingleBackup(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        (Jsm.Opcode, int)[] operations =
        [
            (Jsm.Opcode.PSHN_L, 77),
            (Jsm.Opcode.RANIMELOOP, 2),
            (Jsm.Opcode.PUSHANIME, 0),
            (Jsm.Opcode.SETMODEL, 3),
            (Jsm.Opcode.PSHN_L, 8),
            (Jsm.Opcode.ANIMESPEED, 0),
            (Jsm.Opcode.RANIMEKEEP, 3),
            (Jsm.Opcode.PSHN_L, 1),
            (Jsm.Opcode.WAIT, 0),
            (Jsm.Opcode.PUSHANIME, 0),
            (Jsm.Opcode.HIDE, 0),
            (Jsm.Opcode.TALKOFF, 0),
            (Jsm.Opcode.THROUGHON, 0),
            (Jsm.Opcode.PSHN_L, 16),
            (Jsm.Opcode.ANIMESPEED, 0),
            (Jsm.Opcode.RANIMELOOP, 4),
            (Jsm.Opcode.POPANIME, 0),
            (Jsm.Opcode.POPANIME, 0),
            (Jsm.Opcode.PSHN_L, 88),
            (Jsm.Opcode.POPM_B, 5000),
            (Jsm.Opcode.POPM_B, 5001)
        ];
        EventEngine live = CreateNativeAnimationBackupFixture(vitals, economy, operations);
        List<NativeActorAnimationBackup> pushes = [];
        List<(long Frame, NativeActorAnimationHeader Header)> pops = [];
        live.CommandExecuted += command =>
        {
            if (command.Name is "PUSHANIME" or "POPANIME")
            {
                Require(command.Arguments.Count == 0, "PUSHANIME/POPANIME consumed unrelated VM operands.");
                if (command.Name == "PUSHANIME")
                {
                    pushes.Add(live.Actors[0].NativeAnimationBackup!);
                }
                else
                {
                    pops.Add((live.Frame, live.Actors[0].NativeAnimationHeader!));
                }
            }
        };
        AdvanceToNativeAnimationIssue(live, 4, 0x2000);
        ActorState actor = live.Actors[0];
        Require(pushes.SequenceEqual([new NativeActorAnimationBackup(1, 2, 16, 16, 0, 48, 0x2000), new NativeActorAnimationBackup(1, 3, 16, 8, 0, 48, 0x8000)]), "A second PUSH did not overwrite the same actor-global physical slot with its current fractional-frame/speed/flag words.");
        Require(actor.ModelId == 3 && actor.NativeAnimationHeader!.BoundModelId == 2 && !actor.Visible && !actor.TalkEnabled && !actor.CollisionEnabled, "SETMODEL rebound an existing instance or independent live visibility/talk/collision producers did not execute.");
        EventEngine restored = CreateNativeAnimationBackupFixture(vitals, economy, operations);
        restored.Restore(live.Capture());
        live.Step();
        restored.Step();
        Require(pops.Count == 2 && pops[0].Frame == pops[1].Frame && pops[0].Header == pops[1].Header && pushes.Count == 2, "POP cleared its reusable backup or inserted an animation callback/yield between immediate same-turn instructions.");
        RequireNativeAnimationWords(actor, 3, 16, 8, 0, 48, 0x8000);
        Require(actor.NativeAnimationBackup == pushes[1] && actor.ModelId == 3 && actor.NativeAnimationHeader!.BoundModelId == 2 && !actor.Visible && !actor.TalkEnabled && !actor.CollisionEnabled, "POP restored physical model identity or lower current control/visibility/collision state instead of only animation ownership.");
        Require(live.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 88 && live.Capture().GetProperty("Variables").GetProperty("5001").GetInt32() == 77 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Immediate backup commands lost their successor, unrelated stack operand or independent continuation.");
        JsonElement valid = live.Capture();
        foreach (string owner in new[] { "NativeAnimationHeader", "NativeAnimationBackup" })
        {
            JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
            malformed["Actors"]![0]!.AsObject().Remove(owner);
            bool refused = false;
            try
            {
                live.Restore(JsonSerializer.SerializeToElement(malformed));
            }
            catch (InvalidDataException)
            {
                refused = true;
            }
            catch (JsonException)
            {
                refused = true;
            }
            Require(refused && JsonElement.DeepEquals(valid, live.Capture()), "Malformed required animation ownership was admitted or partially mutated its restore target.");
        }
    }

    private static void NativeAnimationFreshPopAndOpaqueRefusal(NativeCharacterVitals vitals, NativeEconomy economy)
    {
        EventEngine fresh = CreateNativeAnimationBackupFixture(vitals, economy, [(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPANIME, 0), (Jsm.Opcode.POPANIME, 0), (Jsm.Opcode.POPM_B, 5000)]);
        for (int callback = 0; callback < 8 && !fresh.Capture().GetProperty("Variables").TryGetProperty("5000", out _); callback++)
        {
            fresh.Step();
        }
        RequireNativeAnimationWords(fresh.Actors[0], 0, 0, 0, 0, 0, 0);
        Require(fresh.Capture().GetProperty("Variables").GetProperty("5000").GetInt32() == 77 && fresh.Actors[0].NativeAnimationBackup == new NativeActorAnimationBackup(1, 0, 0, 0, 0, 0, 0), "POP before PUSH invented a clip range/speed or consumed the authentic zero slot.");

        EventEngine opaque = CreateNativeAnimationBackupFixture(vitals, economy, [(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PUSHANIME, 0), (Jsm.Opcode.POPM_B, 5000)], ushort.MaxValue);
        bool refused = false;
        try
        {
            for (int callback = 0; callback < 8; callback++)
            {
                opaque.Step();
            }
        }
        catch (EventScriptException)
        {
            refused = true;
        }
        JsonElement held = opaque.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(refused && opaque.LastFault is { Opcode: nameof(Jsm.Opcode.PUSHANIME) } fault && held.GetProperty("Position").GetInt32() == fault.Operation && held.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([77]) && opaque.Actors[0].NativeAnimationHeader!.BoundModelId == -1 && opaque.Actors[0].NativeAnimationHeader!.Terminal is null && opaque.Actors[0].NativeAnimationBackup == new NativeActorAnimationBackup(1, 0, 0, 0, 0, 0, 0) && !opaque.Capture().GetProperty("Variables").TryGetProperty("5000", out _), "Opaque empty-model animation was replaced with inferred metadata, or PUSH refused only after changing PC, operand or backup state.");
    }

    private static EventEngine CreateNativeAnimationBackupFixture(NativeCharacterVitals vitals, NativeEconomy economy, (Jsm.Opcode Opcode, int Parameter)[] operations, int model = 2)
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, model), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] main = new[] { (Jsm.Opcode.LBL, 1) }.Concat(operations).Concat([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]).ToArray();
        byte[] content = SyntheticObjects([1], [init, main]);
        int start = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6));
        for (int offset = start; offset < content.Length; offset += 4)
        {
            int encoded = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(offset));
            if (encoded == (int)Jsm.Opcode.BASEANIME)
            {
                BinaryPrimitives.WriteInt32LittleEndian(content.AsSpan(offset), encoded << 24);
            }
        }
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        EventEngine engine = new(session, session.State.Variables)
        {
            AnimationFrameCount = (boundModel, clip) => boundModel == 2 && clip is >= 0 and <= 6 ? clip == 6 ? 42 : 3 : throw new InvalidOperationException($"Animation queried unknown/unbound resource model{boundModel},clip{clip}; physical SETMODEL must not replace the retained binding.")
        };
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static void AdvanceToNativeAnimationIssue(EventEngine engine, int clip, ushort flags)
    {
        for (int callback = 0; callback < 16; callback++)
        {
            engine.Step();
            if (engine.Actors[0].NativeAnimationHeader is { } header && header.AnimationId == clip && header.Flags == flags)
            {
                return;
            }
        }
        throw new InvalidOperationException($"Native animation fixture never issued clip{clip},flags{flags:X4}.");
    }

    private static void RequireNativeAnimationWords(ActorState actor, int clip, int frame, int increment, int first, int terminal, int flags)
    {
        NativeActorAnimationHeader header = actor.NativeAnimationHeader ?? throw new InvalidOperationException("Native animation header is missing.");
        Require(header.Version == 1 && header.AnimationId == clip && header.Frame == frame && header.Increment == increment && header.First == first && header.Terminal == terminal && header.Flags == flags, $"Native animation words differ: expected clip{clip},518={frame},520={increment},522={first},524={terminal},F800={flags:X4}; actual{header}.");
        Require(actor.AnimationId == clip && actor.AnimationFrame == frame / 16f, "Rendered animation selection/WORD82 differs from the admitted physical body phase.");
    }
}
