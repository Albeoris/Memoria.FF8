using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static EventEngine CreateNativeLadder(bool up = false, int? speed = null, bool followers = false, bool automatic = false, Jsm.Opcode? request = null)
    {
        (Jsm.Opcode, int)[] Initialize(int label, int character)
        {
            List<(Jsm.Opcode, int)> initialization = [(Jsm.Opcode.LBL, label)];
            if (character == 0)
            {
                initialization.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, followers ? 1 : 255), (Jsm.Opcode.PSHN_L, followers ? 2 : 255), (Jsm.Opcode.SETPARTY, 0)]);
            }
            initialization.AddRange([(Jsm.Opcode.SETMODEL, character), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 7), (Jsm.Opcode.FOOTSTEPOFF, 0), (Jsm.Opcode.PSHN_L, 5 + character * 10), (Jsm.Opcode.PSHN_L, 4 + character * 10), (Jsm.Opcode.LADDERANIME, 3 + character * 10), (Jsm.Opcode.RET, 0)]);
            return initialization.ToArray();
        }
        List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.INITTRACE, 0)];
        if (automatic)
        {
            main.Add((Jsm.Opcode.UCOFF, 0));
        }
        if (speed is int speedOperand)
        {
            main.Add((Jsm.Opcode.PSHN_L, speedOperand));
            main.Add(((Jsm.Opcode)372, 0));
        }
        foreach (int value in new[] { 65537, 0x100002, 0x100003, 65565, 0x100002, 0x100003, 65569, 0x100002, 0x100003 })
        {
            main.Add((Jsm.Opcode.PSHN_L, value));
        }
        main.AddRange([(up ? Jsm.Opcode.LADDERUP2 : Jsm.Opcode.LADDERDOWN2, 9), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6000), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
        EventEngine engine = new(session, session.State.Variables)
        {
            AnimationFrameCount = (_, _) => 4,
            ResolveNativeTriangleHeight = (_, _, _) => 3,
            ResolvePartyTraceTriangle = (_, _, _) => 7
        };
        List<(Jsm.Opcode, int)[]> scripts = [Initialize(0, 0), main.ToArray()];
        if (request is { } requestOpcode)
        {
            scripts.Add([(Jsm.Opcode.LBL, 2), (Jsm.Opcode.HIDE, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.POPM_B, 6001), (Jsm.Opcode.RET, 8)]);
            scripts.Add([(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 8)]);
            scripts.Add([(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.PSHN_L, 2), (requestOpcode, 0), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.POPM_B, 6002), (Jsm.Opcode.RET, 8)]);
        }
        if (followers)
        {
            foreach (int slot in new[] { 1, 2 })
            {
                scripts.Add(Initialize(slot * 16, slot));
                scripts.Add([(Jsm.Opcode.LBL, slot * 16 + 1), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
            }
        }
        ushort[] groups = request is not null ? [2, (16 << 7) | 1] : followers ? [1, (16 << 7) | 1, (32 << 7) | 1] : [1];
        engine.LoadField(600, SyntheticObjects(groups, scripts), [], new FieldTransition(600, 1, 2, 7, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static void NativeLadderButtons(EventEngine engine, int held)
    {
        Require(engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons, held)).Accepted, "Native ladder fixture refused raw held input.");
    }

    private static void ReachNativeLadderClimb(EventEngine engine)
    {
        for (int update = 0; update < 8 && (engine.Actors[0].NativeLadder is null || engine.Actors[0].NativeMovementCompletion542 != 1); update++)
        {
            engine.Step();
        }
        Require(engine.Actors[0].NativeLadder is { Active: true } && engine.Actors[0].NativeMovementCompletion542 == 1 && engine.Actors[0].NativeTurnAccumulator474 == 0, "Native ladder did not finish approach at the old-counter pose and enter idle climb.");
    }

    private static void ReachNativeLadderIssue(EventEngine engine)
    {
        for (int update = 0; update < 8 && engine.Actors[0].NativeLadder is null; update++)
        {
            engine.Step();
        }
        Require(engine.Actors[0].NativeLadder is { Active: true } && engine.Actors[0].NativeMovementCompletion542 == 3 && engine.Actors[0].NativeTurnAccumulator474 == 1, "Native ladder fixture did not reach its first post-VM mode4 update.");
    }

    private static void NativeLadderContract()
    {
        foreach (bool up in new[] { false, true })
        {
            EventEngine live = CreateNativeLadder(up);
            ReachNativeLadderIssue(live);
            ActorState actor = live.Actors[0];
            NativeLadderState issued = actor.NativeLadder ?? throw new InvalidOperationException("Mode4 ladder did not establish its body owner.");
            Require(issued.Entry == new FieldFixedPosition(4096, 8192, 12288) && issued.Approach == new FieldFixedPosition(65537 << 12, 8192, 12288) && issued.ClimbingEnd == new FieldFixedPosition(65565 << 12, 8192, 12288) && issued.Exit == new FieldFixedPosition(65569 << 12, 8192, 12288) && issued.Up == up && actor.NativeLandingTriangle508 == 9 && actor.WalkmeshTriangle == 7, "Ladder narrowed fullDWORD coordinates to WORDs, reversed its nine operands, snapped entry or changed triangle during setup.");
            Require(actor.NativeMovementCompletion542 == 3 && actor.NativeTurnAccumulator474 == 1 && issued.FixedX == 4096, "Ladder stage0 failed same-callback stage3 evaluation with its original entry and old counter.");
            ReachNativeLadderClimb(live);
            NativeLadderState approach = actor.NativeLadder!;
            Require(approach.Duration == 4 && approach.FixedX == NativeJumpMath.Interpolate(4096, 65537 << 12, 2, 1), "Ladder snapped the approach endpoint or did not derive climb duration from native fixed deltas and speedBYTE28.");
            JsonElement saved = live.Capture();
            JsonElement owner = saved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Sequence").GetInt64() == approach.FiberSequence);
            Require(owner.GetProperty("Position").GetInt32() == approach.Operation && owner.GetProperty("Stack").GetArrayLength() == 0 && owner.GetProperty("WaitReason").GetString() == "NativeLadder", "Ladder failed to consume exactly nine operands and retain the issuing instruction.");
            for (int idle = 0; idle < 2; idle++)
            {
                live.Step();
                Require(actor.NativeLadder == approach && actor.NativeTurnAccumulator474 == 0, "Idle ladder advanced its physical counter or changed its retained pose.");
            }
            EventEngine independent = CreateNativeLadder(up);
            independent.Restore(live.Capture());
            int forward = up ? 0x3000 : 0xC000;
            void StepBoth(int held)
            {
                NativeLadderButtons(live, held);
                NativeLadderButtons(independent, held);
                live.Step();
                independent.Step();
                Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Independent ladder restore diverged in phases, fixed placement, animation, trace or held PC.");
            }
            StepBoth(forward);
            Require(actor.NativeTurnAccumulator474 == 1 && actor.NativeLadder!.FixedX == 65537 << 12, "Forward ladder did not interpolate before incrementing its shared counter.");
            StepBoth(forward);
            Require(live.ButtonInput!.RawPressed == 0 && actor.NativeTurnAccumulator474 == 2 && actor.NativeLadder!.FixedX == NativeJumpMath.Interpolate(65537 << 12, 65565 << 12, 4, 1), "Ladder consumed Pressed edges instead of held mapped buttons or evaluated after incrementing.");
            StepBoth(0xF000);
            Require(actor.NativeTurnAccumulator474 == 1 && actor.NativeMovementCompletion542 == 1, "Both ladder input groups did not prioritize reverse for the selected direction.");
            for (int remaining = 0; remaining < 3; remaining++)
            {
                StepBoth(forward);
            }
            Require(actor.NativeMovementCompletion542 == 4 && actor.NativeTurnAccumulator474 == 0 && actor.NativeLadder!.FixedX == NativeJumpMath.Interpolate(65537 << 12, 65565 << 12, 4, 3) && actor.WalkmeshTriangle == 7, "Forward ladder failed stage4 entry, changed triangle early or snapped its climb endpoint.");
            StepBoth(0);
            Require(actor.NativeMovementCompletion542 == 4 && actor.NativeTurnAccumulator474 == 1 && actor.NativeLadder!.FixedX == 65565 << 12, "Exit phase failed its own old-counter origin.");
            StepBoth(0);
            Require(actor.NativeMovementCompletion542 == 6 && actor.WalkmeshTriangle == 9 && actor.NativeLadder!.FixedX == NativeJumpMath.Interpolate(65565 << 12, 65569 << 12, 2, 1), "Exit phase snapped the endpoint or failed the landing-triangle transfer to stage6.");
            StepBoth(0);
            Require(actor.NativeMovementCompletion542 == 2 && actor.NativeLadder is { Active: true } && !live.Capture().GetProperty("Variables").TryGetProperty("6000", out _), "Zero-follower stage6 did not complete after its single callback while preserving the held instruction.");
            StepBoth(0);
            Require(actor.NativeLadder is not { Active: true } && live.Capture().GetProperty("Variables").GetProperty("6000").GetInt32() == 73, "Ladder completion return2 failed to execute its immediate successor within the same VM budget.");
        }
        NativeLadderAutomaticContract();
        NativeLadderRequestContract();
        Console.WriteLine("Native mode4 ladder: fullDWORD coordinates, old-counter phases, direction masks/priority, mapped held input, triangle transfer, return2 successor and independent restore passed.");
    }

    private static void NativeLadderReverseAndRestoreContract()
    {
        foreach (bool up in new[] { false, true })
        {
            EventEngine live = CreateNativeLadder(up, 284);
            ReachNativeLadderClimb(live);
            ActorState actor = live.Actors[0];
            Require(live.Scene.NativeLadderSpeed490E == 28 && actor.NativeLadder!.Duration == 4, "Opcode372 did not store the lowBYTE speed used by native ladder duration.");
            JsonElement valid = live.Capture();
            foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
            {
                state => state["Actors"]![0]!["NativeLadder"]!["Operation"] = actor.NativeLadder!.Operation + 1,
                state => state["Actors"]![0]!["NativeLadder"]!["FixedX"] = actor.NativeLadder!.FixedX + 1,
                state => state["Actors"]![0]!["NativeMovementCompletion542"] = 7,
                state => state["Scene"]!.AsObject().Remove("NativeLadderSpeed490E")
            })
            {
                JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
                corrupt(malformed);
                try
                {
                    live.Restore(JsonSerializer.SerializeToElement(malformed));
                    throw new InvalidOperationException("Malformed ladder owner, body, stage or missing speed producer restored.");
                }
                catch (Exception exception) when (exception is InvalidDataException or JsonException)
                {
                }
                Require(JsonElement.DeepEquals(valid, live.Capture()), "Malformed ladder restoration partially mutated the live VM.");
            }
            NativeLadderButtons(live, up ? 0xC000 : 0x3000);
            live.Step();
            Require(actor.NativeMovementCompletion542 == 5 && actor.NativeTurnAccumulator474 == 0 && actor.NativeLadder!.FixedX == 65537 << 12, "Reverse from counter0 did not evaluate approach then enter stage5 after signed decrement.");
            EventEngine independent = CreateNativeLadder(up, 284);
            independent.Restore(live.Capture());
            NativeLadderButtons(live, 0);
            NativeLadderButtons(independent, 0);
            for (int update = 0; update < 4; update++)
            {
                live.Step();
                independent.Step();
                Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Independent reverse ladder restoration changed exit/finish continuation.");
                Require(actor.WalkmeshTriangle == 7, "Reversed ladder copied its forward landing triangle.");
            }
            Require(actor.NativeLadder is not { Active: true } && actor.X == NativeJumpMath.Interpolate(65537 << 12, 4096, 2, 1) / 4096f, "Reverse ladder failed native unsnapped original-entry interpolation and completion.");
        }
        Console.WriteLine("Native ladder reverse: opcode372 byte speed, stage5 original entry, retained triangle, atomic malformed restore and independent return passed.");
    }

    private static void NativeLadderFollowerContract()
    {
        foreach (bool up in new[] { false, true })
        {
            EventEngine live = CreateNativeLadder(up, followers: true);
            ReachNativeLadderIssue(live);
            Require(live.Scene.PartyTrace is { FirstOffset: 16, SecondOffset: 31 }, "Three-party ladder fixture did not advance original follower delays15/30 toward31/62 on entry.");
            int entryCursor = (live.Scene.PartyTrace!.Cursor - 1) & 63;
            ActorState actor = live.Actors[0];
            Require(live.Scene.FieldPartyNativeActorIds.SequenceEqual([0, 1, 2]), "Three-party ladder fixture lost physical body bindings.");
            void CheckSample(int cursor, int kind, int step, int x)
            {
                PartyTraceState trace = live.Scene.PartyTrace!;
                foreach (PartyTraceSample? sample in new[] { trace.FirstTrail[cursor], trace.SecondTrail[cursor] })
                {
                    Require(sample is { FirstFlag: 0 } && sample.Animation == kind && sample.SecondFlag == unchecked((byte)(sbyte)step) && sample.X == unchecked((short)x), "Native ladder dual rings lost animation kind, signed step byte or pre-interpolation body position.");
                }
            }
            void CheckFollowers()
            {
                PartyTraceState trace = live.Scene.PartyTrace!;
                foreach (int slot in new[] { 1, 2 })
                {
                    ActorState follower = live.Actors[slot];
                    int offset = slot == 1 ? trace.FirstOffset : trace.SecondOffset;
                    PartyTraceSample sample = (slot == 1 ? trace.FirstTrail : trace.SecondTrail)[(trace.Cursor - offset) & 63]!;
                    NativeLadderAnimationState animations = follower.NativeLadderAnimation!;
                    int animation = sample.Animation switch
                    {
                        0 => follower.WalkAnimationId,
                        1 => follower.RunAnimationId,
                        2 => follower.BaseAnimationId,
                        3 => animations.Start594,
                        4 => animations.Loop595,
                        5 => animations.End596,
                        _ => throw new InvalidOperationException("Unexpected ladder follower animation kind.")
                    };
                    Require(follower.X == sample.X && follower.Y == sample.Y && follower.Z == sample.Z && follower.WalkmeshTriangle == sample.Triangle && follower.AnimationId == animation, "Ladder follower failed its delayed ring placement or selected the leader's animation bytes instead of its own.");
                }
            }
            CheckSample(entryCursor, up ? 3 : 5, up ? 1 : -1, 1);
            EventEngine entryRestored = CreateNativeLadder(up, followers: true);
            entryRestored.Restore(live.Capture());
            live.Step();
            entryRestored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), entryRestored.Capture()), "Independent stage3 ladder restoration changed the first delayed follower placement.");
            ReachNativeLadderClimb(live);
            int forward = up ? 0x3000 : 0xC000;
            int reverse = up ? 0xC000 : 0x3000;
            void Move(int held, int kind, int step)
            {
                NativeLadderState before = actor.NativeLadder!;
                int cursor = live.Scene.PartyTrace!.Cursor;
                NativeLadderButtons(live, held);
                live.Step();
                CheckSample(cursor, kind, step, before.FixedX / 4096);
                CheckFollowers();
            }
            for (int cycle = 0; cycle < 8; cycle++)
            {
                Move(forward, 4, up ? 1 : -1);
                Move(forward, 4, up ? 1 : -1);
                Move(reverse, 4, up ? -1 : 1);
                Move(reverse, 4, up ? -1 : 1);
            }
            Require(actor.NativeMovementCompletion542 == 1 && actor.NativeTurnAccumulator474 == 0 && live.Scene.PartyTrace is { FirstOffset: 31, SecondOffset: 62 }, "Native ladder movement did not grow actual follower delays toward31/62 while held forward/reverse preserved climb ownership.");
            for (int update = 0; update < 4; update++)
            {
                Move(forward, 4, up ? 1 : -1);
            }
            for (int update = 0; update < 2; update++)
            {
                Move(0, up ? 5 : 3, up ? 1 : -1);
            }
            Require(actor.NativeMovementCompletion542 == 6 && actor.NativeTurnAccumulator474 == 0, "Three-party ladder missed finish-phase setup.");
            EventEngine independent = CreateNativeLadder(up, followers: true);
            independent.Restore(live.Capture());
            for (int elapsed = 1; elapsed <= 32; elapsed++)
            {
                int cursor = live.Scene.PartyTrace!.Cursor;
                int x = actor.NativeLadder!.FixedX / 4096;
                live.Step();
                independent.Step();
                CheckSample(cursor, 2, -1, x);
                CheckFollowers();
                Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Independent three-party ladder restoration changed delayed animation, trace positions or finish timing.");
                Require(actor.NativeMovementCompletion542 == (elapsed == 32 ? 2 : 6) && actor.NativeTurnAccumulator474 == elapsed && live.Scene.PartyTrace!.FirstOffset == Math.Max(15, 31 - elapsed) && live.Scene.PartyTrace.SecondOffset == 62 - elapsed, "Two-follower ladder finish did not wait32callbacks and restore actual delays toward15/30 one sample at a time.");
            }
            live.Step();
            independent.Step();
            Require(actor.NativeLadder is not { Active: true } && JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Three-party ladder completion changed delayed follower continuation.");

            EventEngine retreat = CreateNativeLadder(up, followers: true);
            ReachNativeLadderClimb(retreat);
            NativeLadderButtons(retreat, reverse);
            retreat.Step();
            Require(retreat.Actors[0].NativeMovementCompletion542 == 5, "Three-party ladder did not reach its original-entry reverse transition.");
            EventEngine retreatRestored = CreateNativeLadder(up, followers: true);
            retreatRestored.Restore(retreat.Capture());
            NativeLadderButtons(retreat, 0);
            NativeLadderButtons(retreatRestored, 0);
            for (int update = 0; update < 35; update++)
            {
                retreat.Step();
                retreatRestored.Step();
                Require(JsonElement.DeepEquals(retreat.Capture(), retreatRestored.Capture()) && retreat.Actors[0].WalkmeshTriangle == 7, "Independent three-party reverse transition lost its original triangle, delayed follower pose or animation.");
            }
            Require(retreat.Actors[0].NativeLadder is not { Active: true }, "Three-party reverse ladder did not complete its32callback follower finish.");
        }
        Console.WriteLine("Native ladder followers: dual-ring pre-interpolation samples, kinds3/4/5 and signed byte9,31/62 delays,32callback finish toward15/30 and independent three-party restore passed.");
    }
}
