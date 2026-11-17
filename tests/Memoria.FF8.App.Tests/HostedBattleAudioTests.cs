using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class HostedBattleAudioTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Hosted_physical_attack_plays_only_committed_sounds_once(int allies)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        ExpandedBattleInstalledEntry.Prepare(world, session, current =>
        {
            NativeCharacterVitals vitals = current.CharacterVitals!.WithGuestBattleRecords().Rest();
            byte[] rows = Enumerable.Range(0, allies).Select(row => checked((byte)row)).ToArray();
            foreach (byte row in rows)
            {
                vitals.Records[row] = vitals.Records[row] with { MemberFlags = (ushort)(vitals.Records[row].MemberFlags | 1), Status = 0 };
            }
            current.SetCharacterVitals(vitals.WithBattleParty(rows, allies));
        }, allies: allies, additionalEnemies: 0);
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        BattleCombatant actor = battle.Combatants.First(actor => actor.IsPlayer);
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive);
        actor.Atb = BattleSession.AtbMaximum;
        List<NativeBattleModuleSound> sounds = [];
        BattleAudioPresentation audio = BattleAudioTestOutput.Create(sounds.Add);
        audio.Update(battle);
        int initialEvents = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: actor.Id, TargetId: target.Id) { ForcePhysical = true });
        for (int callback = 0; callback < 1600; callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            audio.Update(battle);
            int played = sounds.Count;
            audio.Update(battle);
            Assert.Equal(played, sounds.Count);
            if (battle.ActiveAction is null && battle.Events.Skip(initialEvents).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == actor.Id))
            {
                break;
            }
        }
        Assert.Contains(battle.Events.Skip(initialEvents), entry => entry.Kind == "ActionComplete" && entry.ActorId == actor.Id);
        Assert.Contains(battle.Events.Skip(initialEvents), entry => entry.NativeSound is not null && entry.NativeModuleSound is not null);
        Assert.NotEmpty(sounds);
        Assert.Equal(battle.Events.Skip(initialEvents).Where(entry => entry.NativeModuleSound is not null).Select(entry => entry.NativeModuleSound!), sounds);
    }

    [Fact]
    public void Saved_world_automatic_guardian_uses_committed_audio_through_return()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_AUTOMATIC_GUARDIAN_SAVE");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone world save with Odin unlocked is required.");
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source!));
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState saved = PlayerStateLoader.Load(source!, directory, sharedAssets: assets);
        Assert.Equal(SceneKind.WorldMap, saved.Scene);

        for (int seed = 0; seed < 256; seed++)
        {
            GameSession session = new(saved);
            using GameWorld world = new(session, directory, sharedAssets: assets);
            world.EnsureField();
            WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
            RandomEncounterSessionState random = Assert.IsType<RandomEncounterSessionState>(session.State.RandomEncounters);
            session.SetRandomEncounters(random with { Pending = new(SceneKind.WorldMap, session.State.FieldId, scene.State.Frame, 717, scene.AllocationProgramSha256) });
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: checked((byte)seed)));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
            if (!battle.QueuedActions.Any(command => command is { NativeQueuedCommand: 245, ActionId: 0 }))
            {
                continue;
            }

            TestContext.Current.TestOutputHelper!.WriteLine($"Automatic guardian entry: seed={seed}, field={session.State.FieldId}, worldFrame={scene.State.Frame}.");
            List<NativeBattleModuleSound> sounds = [];
            BattleAudioPresentation audio = BattleAudioTestOutput.Create(sounds.Add);
            audio.Update(battle);
            int initialEvents = battle.Events.Count;
            bool observedGuardian = false;
            for (int callback = 0; callback < 5000 && !battle.CanReturnToField; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (battle.ActiveAction is { IndependentGuardian.Action.ActionId: 0 } action)
                {
                    observedGuardian = true;
                    Assert.Equal<byte?>(0, action.NativeSequence);
                }
                audio.Update(battle);
                int played = sounds.Count;
                audio.Update(battle);
                Assert.Equal(played, sounds.Count);
            }

            Assert.True(observedGuardian);
            Assert.True(battle.CanReturnToField);
            Assert.Equal(BattleOutcome.Victory, battle.Outcome);
            Assert.NotEmpty(sounds);
            Assert.Equal(battle.Events.Skip(initialEvents).Where(entry => entry.NativeModuleSound is not null).Select(entry => entry.NativeModuleSound!), sounds);
            BattleAudioPresentation replacement = BattleAudioTestOutput.Create(_ => Assert.Fail("Historical module sound replayed."));
            replacement.Update(battle);
            replacement.Update(battle);
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            Assert.Equal(SceneKind.WorldMap, session.State.Scene);
            string? selectedOutput = Environment.GetEnvironmentVariable("MEMORIA_FF8_AUTOMATIC_GUARDIAN_OUTPUT");
            string output = selectedOutput ?? Path.Combine(Path.GetTempPath(), "Memoria.FF8-guardian-audio-" + Guid.NewGuid().ToString("N") + ".ff8save");
            Assert.False(File.Exists(output));
            try
            {
                session.Save(output, compact: true);
                GameSession restoredSession = new(GameSession.Load(output));
                using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
                restored.EnsureField();
                long frame = world.WorldScene!.State.Frame;
                PlayerActionResult advanced = world.Apply(new(PlayerActionKind.Wait));
                PlayerActionResult restoredAdvanced = restored.Apply(new(PlayerActionKind.Wait));
                Assert.True(advanced.Accepted, advanced.Detail);
                Assert.Equal(1, advanced.FramesAdvanced);
                Assert.Equal(advanced, restoredAdvanced);
                Assert.Equal(frame + 1, world.WorldScene.State.Frame);
                Assert.Equal(JsonSerializer.Serialize(world.WorldScene!.State), JsonSerializer.Serialize(restored.WorldScene!.State));
                TestContext.Current.TestOutputHelper!.WriteLine($"Returned checkpoint: field={session.State.FieldId}, worldFrame={scene.State.Frame}.");
            }
            finally
            {
                if (selectedOutput is null)
                {
                    File.Delete(output);
                }
            }
            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source!)));
            return;
        }
        Assert.Fail("No original entry roll admitted Odin for the supplied world save.");
    }
}
