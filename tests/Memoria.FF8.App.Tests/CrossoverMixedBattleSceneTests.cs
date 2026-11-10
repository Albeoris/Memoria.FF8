using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF7.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverMixedBattleSceneTests(ITestOutputHelper output)
{
    [Fact]
    public void Explicit_guest_option_runs_original_attack_and_combined_settlement_through_the_scene()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using GameAssets game = new(ff8Root!);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(game, false) with { Characters = GameplaySaveCharacters.Capture(game.ReadNewGameCharacterVitals()) };
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, document);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        CombinedSessionSave source = new() { ActiveGame = SessionGame.Ff7, Ff7 = initial.Capture(), Ff8Payload = payload.ToArray(), Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec };
        using GpuTestDevice device = new();
        using Ff7Scene scene = new(device.Device, installation, "", new(50, 32000), Movie, source, audioEnabled: false);
        scene.ConfigureCharacterIdentity(ff8Root!);
        scene.Continue(source);
        Assert.False(scene.MixedCombatEnabled);
        Press(Keys.F2);
        Press(Keys.Up);
        Press(Keys.Enter);
        Assert.True(scene.MixedCombatEnabled);
        Press(Keys.F2);
        Ff7Session session = scene.Session!;
        for (int tick = 0; tick < 4500 && session.Battle is null; tick++)
        {
            if (session.Host.Process.Movie is { Playing: true } movie)
            {
                session.Host.PublishMovieFrame(Math.Min(movie.Frame + tick % 2, 1799), movie.Frame >= 1799);
            }
            if (tick % 30 == 0)
            {
                foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed))
                {
                    session.Host.CompleteDialogue(dialogue.RequestId, dialogue.FirstChoice ?? 0);
                }
            }
            if (tick >= 4110 && !session.Host.Process.PlayerLocked)
            {
                session.MovePlayer(0, -1, true);
            }
            session.AdvanceTick();
        }
        Assert.NotNull(session.Battle);
        using SpriteBatch batch = new(device.Device);
        using RenderTarget2D target = device.CreateTarget(320, 240);
        Draw();
        Assert.Equal(486U, session.Battle!.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints);
        CombinedSessionSave active = scene.Capture();
        Assert.NotEmpty(active.MixedBattlePayload!);
        scene.Continue(active);
        session = scene.Session!;
        bool submitted = false;
        bool triggered = false;
        for (int frame = 0; frame < 6500 && scene.Session!.Battle is { ResultReady: false }; frame++)
        {
            Ff7BattleSession battle = scene.Session.Battle!;
            if (battle.ReadyPlayer == 0)
            {
                Press(Keys.Enter);
                Press(Keys.Enter);
                submitted = true;
            }
            if (scene.MixedTriggerWindowOpen)
            {
                Press(Keys.G);
                triggered = true;
            }
            scene.Update(new(TimeSpan.FromSeconds((frame + 1) / 60d), TimeSpan.FromSeconds(1d / 60)), new KeyboardState());
            Assert.Null(scene.Error);
            if (frame % 17 == 0)
            {
                Draw();
            }
        }
        Assert.True(submitted && triggered);
        Assert.Equal(Ff7BattleOutcome.Victory, scene.Session!.Battle!.Outcome);
        CombinedSessionSave? committed = null;
        scene.MixedCombatSettled += result => committed = result;
        Press(Keys.Enter);
        Assert.NotNull(committed);
        Assert.Null(scene.Session!.Battle);
        Assert.NotNull(committed.GuestBattleSettlements);
        Assert.Null(committed.MixedBattlePayload);
        Assert.True(scene.MixedCombatEnabled);
        output.WriteLine($"Guest scene rendered and settled request {committed.GuestBattleSettlements![0].RequestId}, origin HP {committed.GuestBattleSettlements[0].SettledHp}");
        scene.Session.AdvanceTick();
        string dormant = System.Text.Json.JsonSerializer.Serialize(scene.Session.Progress.Characters.Single(character => character.Id == 0));
        int fieldActor = scene.Session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Entity;
        scene.Session.Host.Execute(new(9000001, fieldActor, 7, 0, new FieldHitPointCommand(0, -100)));
        Assert.Equal((ushort)(committed.GuestBattleSettlements[0].SettledHp - 100), scene.Session.GuestFieldVitals!.Read().Hp);
        scene.Session.Host.Execute(new(9000002, fieldActor, 7, 0, new FieldPartyRecovery(true)));
        Assert.Equal((ushort)486, scene.Session.GuestFieldVitals.Read().Hp);
        Assert.Equal(dormant, System.Text.Json.JsonSerializer.Serialize(scene.Session.Progress.Characters.Single(character => character.Id == 0)));

        void Press(Keys key)
        {
            scene.Update(new(TimeSpan.Zero, TimeSpan.Zero), new KeyboardState());
            scene.Update(new(TimeSpan.Zero, TimeSpan.Zero), new KeyboardState(key));
            Assert.Null(scene.Error);
        }

        void Draw()
        {
            device.Device.SetRenderTarget(target);
            scene.Draw(batch, new(0, 0, 320, 240));
            Assert.Null(scene.Error);
            Assert.True(GpuTestDevice.Read(target).Select(pixel => pixel.PackedValue).Distinct().Count() > 16);
        }

        Ff7MovieSource Movie(int id)
        {
            Ff7MovieResources resource = Ff7MovieResources.Load(installation, id);
            Ff7MovieCameraTrack? track = resource.Cameras is { } cameras ? new(cameras) : null;
            return new(resource.Path, new MovieInformation(320, 240, 15, 120, 1800), 1800, 15) { CameraRecordCount = resource.Cameras?.Count ?? 0, SnapshotCameraFrame = track is null ? null : (frame, words) => track.SnapshotAt(frame, words) };
        }
    }
}
