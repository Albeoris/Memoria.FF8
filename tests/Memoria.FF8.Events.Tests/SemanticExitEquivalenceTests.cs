using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void SemanticExitEquivalenceContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        EventEngine Create(bool writeState, bool conditionalWriter = false, int story = 0, bool conditionalDestination = false, bool presentation = false, int postPresentation = 0, int conditionalStop = 0)
        {
            (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 8)];
            player = [.. player.Take(player.Length - 1), (Jsm.Opcode.PSHN_L, story), (Jsm.Opcode.POPM_W, 256), player[^1]];
            if (conditionalStop != 0)
            {
                player = [.. player.Take(player.Length - 1), (Jsm.Opcode.PSHN_L, story), (Jsm.Opcode.POPM_B, 530), player[^1]];
            }
            List<(Jsm.Opcode, int)[]> scripts = [player, [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]];
            foreach (int label in new[] { 16, 32 })
            {
                scripts.Add([(Jsm.Opcode.LBL, label), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -20), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 8)]);
                for (int local = 1; local < 7; local++)
                {
                    scripts.Add(local == 1 ? [(Jsm.Opcode.LBL, label + local), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, label + local), (Jsm.Opcode.RET, 8)]);
                }
                List<(Jsm.Opcode, int)> exit = [(Jsm.Opcode.LBL, label + 7), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.LINEOFF, 0)];
                if (conditionalStop != 0)
                {
                    exit.AddRange([(Jsm.Opcode.PSHM_B, 530), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.CAL, 12), (Jsm.Opcode.JPF, 3), (Jsm.Opcode.LINEOFF, 0), (conditionalStop == 1 ? Jsm.Opcode.HALT : Jsm.Opcode.RET, conditionalStop == 1 ? 0 : 8)]);
                }
                if (presentation)
                {
                    exit.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 60), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MUSICVOLTRANS, 0)]);
                    if (postPresentation == 1)
                    {
                        exit.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.JPF, 1)]);
                    }
                    if (postPresentation == 2)
                    {
                        exit.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000)]);
                    }
                }
                if (conditionalDestination)
                {
                    exit.AddRange([(Jsm.Opcode.PSHM_W, 256), (Jsm.Opcode.PSHN_L, 482), (Jsm.Opcode.CAL, 8), (Jsm.Opcode.JPF, 8), (Jsm.Opcode.PSHN_L, 176), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.MAPJUMP3, 0), (Jsm.Opcode.JMP, 7), (Jsm.Opcode.PSHN_L, 175), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 567), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.MAPJUMP3, 0), (Jsm.Opcode.RET, 8)]);
                    scripts.Add(exit.ToArray());
                    continue;
                }
                if (conditionalWriter)
                {
                    exit.AddRange([(Jsm.Opcode.PSHM_W, 256), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.CAL, 6), (Jsm.Opcode.JPF, 6), (Jsm.Opcode.PSHN_L, 175), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.MAPJUMP, 0)]);
                }
                if (writeState || conditionalWriter)
                {
                    exit.AddRange([(Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000)]);
                }
                exit.AddRange([(Jsm.Opcode.PSHN_L, 175), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.MAPJUMP, 0), (Jsm.Opcode.RET, 8)]);
                scripts.Add(exit.ToArray());
            }
            byte[] content = SyntheticObjects([(16 << 7) | 7, (32 << 7) | 7, 1], scripts);
            content[1] = 2;
            content[3] = 1;
            GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            EventEngine engine = new(session, session.EventMemory)
            {
                AnimationFrameCount = (_, _) => 1,
                ResolveNativeTriangleHeight = (_, _, _) => 0,
                ResolvePartyTraceTriangle = (_, _, _) => 0
            };
            LoadSemanticFixtureField(engine, session, content);
            engine.Step();
            return engine;
        }

        EventEngine pure = Create(writeState: false);
        ReplaySemanticTrigger[] exits = pure.GetSemanticExitCandidates(SceneKind.Field, 175).ToArray();
        JsonElement before = pure.Capture();
        Require(exits.Length == 2 && exits.All(exit => exit.Kind == ReplaySemanticTriggerKind.DoorEntry) && pure.AreSemanticExitCandidatesEquivalent(exits) && JsonElement.DeepEquals(before, pure.Capture()), "Identical current native door-exit bodies were not proved equivalent without mutation.");
        pure.InfGateways = [new InfGateway(Vector3.Zero, new Vector3(0, 10, 0), new FieldTransition(175, 10, 20, 0, 64), NativeIndex: 9)];
        Require(!pure.AreSemanticExitCandidatesEquivalent(pure.GetSemanticExitCandidates(SceneKind.Field, 175)), "A native door body was equated to an INF packet without proving their distinct event effects.");
        ReplaySemanticTrigger[] mixed = pure.GetSemanticExitCandidates(SceneKind.Field, 175).ToArray();
        Require(!pure.CanCanonicalizeLegacySpatialExits(mixed, SceneKind.Field, 175), "Legacy spatial canonicalization escaped its explicit replay scope.");
        using (pure.BeginSemanticSpatialReplay())
        {
            JsonElement beforeSpatialProof = pure.Capture();
            Require(pure.CanCanonicalizeLegacySpatialExits(mixed, SceneKind.Field, 175) && JsonElement.DeepEquals(beforeSpatialProof, pure.Capture()), "Current pure scripted and INF exits were not canonicalized without mutation.");
            Require(!pure.CanCanonicalizeLegacySpatialExits(mixed.Select(candidate => candidate with { Inferred = false }).ToArray(), SceneKind.Field, 175), "Captured actual admissions were treated as missing legacy spatial causes.");
            Require(!pure.CanCanonicalizeLegacySpatialExits(mixed, SceneKind.Field, 176), "Legacy spatial policy ignored the requested current destination.");
        }
        InfGateway first = pure.InfGateways[0];
        pure.InfGateways = [first, first with { Start = new Vector3(10, 0, 0), NativeIndex = 11 }];
        ReplaySemanticTrigger[] samePackets = pure.GetSemanticExitCandidates(SceneKind.Field, 175).Where(exit => exit.Kind == ReplaySemanticTriggerKind.FieldGateway).ToArray();
        Require(samePackets.Length == 2 && pure.AreSemanticExitCandidatesEquivalent(samePackets), "Equivalent current INF destination packets were distinguished only by their discarded physical segments.");
        pure.InfGateways = [first, first with { Destination = first.Destination with { X = 11 }, NativeIndex = 11 }];
        ReplaySemanticTrigger[] differentPackets = pure.GetSemanticExitCandidates(SceneKind.Field, 175).Where(exit => exit.Kind == ReplaySemanticTriggerKind.FieldGateway).ToArray();
        Require(differentPackets.Length == 2 && !pure.AreSemanticExitCandidatesEquivalent(differentPackets), "Different full current INF destination packets were silently collapsed.");
        Require(pure.CanCanonicalizeLegacyGatewayEntries(differentPackets) && !pure.CanCanonicalizeLegacyGatewayEntries(differentPackets.Select(candidate => candidate with { Inferred = false }).ToArray()), "Explicit legacy physical-arrival policy rejected valid geometry changes or accepted authenticated captured identities.");
        pure.InfGateways = [first, first with { Destination = first.Destination with { Triangle = short.MaxValue }, NativeIndex = 11 }];
        Require(!pure.CanCanonicalizeLegacyGatewayEntries(pure.GetSemanticExitCandidates(SceneKind.Field, 175).Where(exit => exit.Kind == ReplaySemanticTriggerKind.FieldGateway).ToArray()), "Legacy arrival policy erased the native leader-default reset discriminator.");
        pure.InfGateways = [first, first with { Destination = first.Destination with { NativeExtraWord = 1 }, NativeIndex = 11 }];
        Require(!pure.CanCanonicalizeLegacyGatewayEntries(pure.GetSemanticExitCandidates(SceneKind.Field, 175).Where(exit => exit.Kind == ReplaySemanticTriggerKind.FieldGateway).ToArray()), "Legacy arrival policy erased a retained native extra WORD.");
        // Declared, unexecuted discriminator: no mounted disc or live transition is claimed.
        NativeDiscJumpTransition disc = new(1, NativeDiscJumpTransition.CurrentRule, 600, differentPackets[0].ProgramHash, NativeFieldRuntimeProfile.FF8EnExecutableHash, 0, 1, 0, 2, 0, new NativeSteamDiscMount(1, 1, NativeSteamDiscMount.VerifiedWrapperSha256, 1, new string('A', 64), new string('B', 64)));
        pure.InfGateways = [first with { Destination = first.Destination with { DiscJump = disc } }, first with { Destination = first.Destination with { DiscJump = disc }, NativeIndex = 11 }];
        Require(!pure.CanCanonicalizeLegacyGatewayEntries(pure.GetSemanticExitCandidates(SceneKind.Field, 175).Where(exit => exit.Kind == ReplaySemanticTriggerKind.FieldGateway).ToArray()), "Legacy arrival policy erased the DISCJUMP lifecycle discriminator.");
        EventEngine writers = Create(writeState: true);
        ReplaySemanticTrigger[] stateful = writers.GetSemanticExitCandidates(SceneKind.Field, 175).ToArray();
        Require(stateful.Length == 2 && !writers.AreSemanticExitCandidatesEquivalent(stateful) && !writers.Capture().GetProperty("Variables").TryGetProperty("5000", out _), "Even identical state-writing exit scripts were admitted by the pure-exit proof or executed during metadata review.");
        writers.InfGateways = [first];
        using (writers.BeginSemanticSpatialReplay())
        {
            Require(!writers.CanCanonicalizeLegacySpatialExits(writers.GetSemanticExitCandidates(SceneKind.Field, 175), SceneKind.Field, 175) && !writers.Capture().GetProperty("Variables").TryGetProperty("5000", out _), "A reached persistent writer was ignored or executed during mixed-exit canonicalization.");
        }
        EventEngine pureBranch = Create(writeState: false, conditionalWriter: true, story: 0);
        JsonElement beforeBranchProof = pureBranch.Capture();
        int commandsDuringProof = 0;
        pureBranch.CommandExecuted += _ => commandsDuringProof++;
        Require(pureBranch.AreSemanticExitCandidatesEquivalent(pureBranch.GetSemanticExitCandidates(SceneKind.Field, 175)) && commandsDuringProof == 0 && JsonElement.DeepEquals(beforeBranchProof, pureBranch.Capture()), "Current pure branch was rejected because of an unreachable future writer, or detached native proof emitted callbacks/state changes.");
        EventEngine futureBranch = Create(writeState: false, conditionalWriter: true, story: 1);
        Require(!futureBranch.AreSemanticExitCandidatesEquivalent(futureBranch.GetSemanticExitCandidates(SceneKind.Field, 175)) && !futureBranch.Capture().GetProperty("Variables").TryGetProperty("5000", out _), "A currently reached future writer was ignored or executed during pure-prefix proof.");
        foreach (int story in new[] { 481, 482 })
        {
            EventEngine conditionalExit = Create(writeState: false, story: story, conditionalDestination: true);
            conditionalExit.InfGateways = [first];
            using (conditionalExit.BeginSemanticSpatialReplay())
            {
                JsonElement beforeDestinationProof = conditionalExit.Capture();
                Require(conditionalExit.CanCanonicalizeLegacySpatialExits(conditionalExit.GetSemanticExitCandidates(SceneKind.Field, 175), SceneKind.Field, 175) == (story < 482) && JsonElement.DeepEquals(beforeDestinationProof, conditionalExit.Capture()), "Current482 destination branch or MAPJUMP3 height-only spatial distinction was not respected.");
                conditionalExit.InfGateways = [first with { Destination = first.Destination with { Triangle = short.MaxValue } }];
                Require(!conditionalExit.CanCanonicalizeLegacySpatialExits(conditionalExit.GetSemanticExitCandidates(SceneKind.Field, 175), SceneKind.Field, 175), "Mixed exits erased the native leader-default reset discriminator.");
            }
        }
        EventEngine presentationExit = Create(writeState: false, presentation: true);
        presentationExit.InfGateways = [first];
        ReplaySemanticTrigger[] presentationCandidates = presentationExit.GetSemanticExitCandidates(SceneKind.Field, 175).ToArray();
        int audioRequests = 0;
        presentationExit.AudioCommandExecuted += _ => audioRequests++;
        using (presentationExit.BeginSemanticSpatialReplay())
        {
            JsonElement beforePresentationProof = presentationExit.Capture();
            Require(!presentationExit.AreSemanticExitCandidatesEquivalent(presentationCandidates) && !presentationExit.CanCanonicalizeLegacySpatialExits(presentationCandidates, SceneKind.Field, 175), "Strict or pure equivalence accepted a music-volume state write.");
            Require(presentationExit.CanCanonicalizeLegacyPresentationExits(presentationCandidates, SceneKind.Field, 175) && audioRequests == 0 && JsonElement.DeepEquals(beforePresentationProof, presentationExit.Capture()), "Presentation policy failed its detached proof or mutated current audio/state.");
            ReplaySemanticTrigger door = presentationCandidates.First(candidate => candidate.Kind != ReplaySemanticTriggerKind.FieldGateway);
            Require(presentationExit.ApplySemanticTrigger(door).Accepted, "Canonical presentation exit was not admitted through its actual native door dispatcher.");
            presentationExit.Step();
            Require(presentationExit.LastFault is null && presentationExit.Scene.SoundMusicVolume197 == 0 && presentationExit.Scene.MusicVolumeRequest is { RequestedDuration: 120, EffectiveDuration: 120, StoredVolume: 0 } && audioRequests == 1 && presentationExit.NextTrigger.Kind == PlayerTriggerKind.FieldTransition, "Actual presentation exit lost its original BYTE197 write, audio request or field departure.");
        }
        foreach (int suffix in new[] { 1, 2 })
        {
            EventEngine invalidPresentation = Create(writeState: false, presentation: true, postPresentation: suffix);
            invalidPresentation.InfGateways = [first];
            using (invalidPresentation.BeginSemanticSpatialReplay())
            {
                JsonElement beforePresentationProof = invalidPresentation.Capture();
                Require(!invalidPresentation.CanCanonicalizeLegacyPresentationExits(invalidPresentation.GetSemanticExitCandidates(SceneKind.Field, 175), SceneKind.Field, 175) && JsonElement.DeepEquals(beforePresentationProof, invalidPresentation.Capture()), "Presentation policy admitted or executed a post-effect branch or persistent write.");
            }
        }
        foreach (int terminal in new[] { 1, 2 })
        {
            EventEngine stoppedExit = Create(writeState: false, story: 214, conditionalStop: terminal);
            stoppedExit.InfGateways = [first];
            Require(stoppedExit.GetSemanticExitCandidates(SceneKind.Field, 175).Count == 3, "Ordinary potential-exit metadata discarded a current stopped branch outside spatial replay.");
            using (stoppedExit.BeginSemanticSpatialReplay())
            {
                JsonElement beforeStopProof = stoppedExit.Capture();
                ReplaySemanticTrigger[] viable = stoppedExit.GetSemanticExitCandidates(SceneKind.Field, 175).ToArray();
                Require(viable.Length == 1 && viable[0].Kind == ReplaySemanticTriggerKind.FieldGateway && JsonElement.DeepEquals(beforeStopProof, stoppedExit.Capture()), "A proven current safe-prefix HALT/RET remained a competing spatial exit or metadata review mutated state.");
            }
        }
        EventEngine unknownExit = Create(writeState: true, story: 0, conditionalStop: 1);
        unknownExit.InfGateways = [first];
        using (unknownExit.BeginSemanticSpatialReplay())
        {
            JsonElement beforeUnknownProof = unknownExit.Capture();
            Require(unknownExit.GetSemanticExitCandidates(SceneKind.Field, 175).Count == 3 && JsonElement.DeepEquals(beforeUnknownProof, unknownExit.Capture()), "A reached unknown state-writing prefix was mistaken for a proven no-exit branch.");
        }
        Console.WriteLine("Semantic exit equivalence: current enabled native door bodies match excluding only their LBL identity, pure query preserves state, state-writer and script/INF equivalence are refused PASS.");
    }
}
