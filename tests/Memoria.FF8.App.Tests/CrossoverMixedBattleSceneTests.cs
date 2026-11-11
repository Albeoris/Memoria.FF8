using Memoria.FF7.Resources;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF7.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverMixedBattleSceneTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Squall_mapping_runs_original_command_and_combined_settlement_through_the_scene(bool useLimit) => RunOpeningBattle(useLimit, useWorldSwitch: false);

    [Fact]
    public void World_switch_mapping_owns_combat_without_changing_a_restored_native_battle() => RunOpeningBattle(useLimit: false, useWorldSwitch: true);

    private void RunOpeningBattle(bool useLimit, bool useWorldSwitch)
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using GameAssets game = new(ff8Root!);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals();
        if (useLimit)
        {
            origin = origin.WithSavedHitPoints(origin.FindPhysicalRow(0), 60);
        }
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(game, false) with { Characters = GameplaySaveCharacters.Capture(origin) };
        if (useLimit)
        {
            document = document with { Limits = document.Limits with { Options = (ushort)((document.Limits.Options ?? 0) & ~0x8001) } };
        }
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, document);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        CombinedSessionSave source = new() { ActiveGame = SessionGame.Ff7, Ff7 = initial.Capture(), Ff8Payload = payload.ToArray(), Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec };
        using GpuTestDevice device = new();
        using Ff7Scene scene = new(device.Device, installation, "", new(50, 32000), Movie, source, audioEnabled: false);
        scene.ConfigureCharacterIdentity(ff8Root!);
        scene.Continue(source with { MixedCombat = new(0) });
        Assert.False(scene.MixedCombatEnabled);
        using SpriteBatch batch = new(device.Device);
        scene.ConfigureSharedDialogueDefaults(batch, ff8Root!, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        CharacterAppearanceMapping[] mappings = CharacterMappingAssociations.Assign([], 0, 0);
        if (useWorldSwitch)
        {
            using Texture2D pixel = new(device.Device, 1, 1);
            using SystemTextRenderer text = new(device.Device);
            using MonoGameUiCanvas canvas = new(device.Device, batch, pixel, text, ff8Root!);
            using CrossoverMappingMenu menu = new(device.Device, installation, ff8Root!, mappings, default, default, text, canvas, () => new UiTheme());
            menu.Confirmed += selectedMappings =>
            {
                Assert.Equal(mappings, selectedMappings);
                scene.Continue(source with { Mappings = selectedMappings });
                scene.PrepareMappedCombatForSwitch();
            };
            menu.Apply(MenuInput.Down);
            menu.Apply(MenuInput.Down);
            menu.Apply(MenuInput.Down);
            menu.Apply(MenuInput.Confirm);
        }
        else
        {
            scene.Continue(source with { Mappings = mappings });
        }
        Assert.True(scene.MixedCombatEnabled);
        Ff7Session session = scene.Session!;
        Assert.True(session.Progress.FullCombatEnabled);
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
        Ff7SessionSave nativeBattle = session.Capture();
        using RenderTarget2D target = device.CreateTarget(320, 240);
        Draw();
        string originName = document.Progression!.SceneFacts!.Names[0];
        Assert.Equal(originName, session.Battle!.Name(0));
        Assert.Equal(useLimit ? 60U : 486U, session.Battle!.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints);
        CombinedSessionSave active = scene.Capture();
        Assert.NotEmpty(active.MixedBattlePayload!);
        if (useWorldSwitch)
        {
            ColdContinue(active);
            Assert.Equal(originName, scene.Session!.Battle!.Name(0));
            ColdContinue(source with { Mappings = mappings, Ff7 = nativeBattle });
            Assert.Equal(nativeBattle.Battle!.Tick, scene.Session!.Battle!.Tick);
            Assert.Null(scene.Session.Battle.GuestVitals);
            Assert.Equal("Cloud", scene.Session.Battle.Name(0));
            return;
        }
        scene.Continue(active);
        session = scene.Session!;
        bool submitted = false;
        bool triggered = false;
        bool restoredLimit = false;
        bool restoredFinisher = false;
        int presentedContacts = 0;
        int expectedContacts = 0;
        HashSet<SceneCameraPlacement> leasedCameras = [];
        for (int frame = 0; frame < 6500 && scene.Session!.Battle is { ResultReady: false }; frame++)
        {
            Ff7BattleSession battle = scene.Session.Battle!;
            if (useLimit && battle.GuestRenderCameraOutput is { Basis: not null } lease)
            {
                leasedCameras.Add(lease);
            }
            if (useLimit && restoredLimit && !restoredFinisher && battle.Action?.GuestLimit is { PendingHit: { } finisher, Contacts: int contacts } && contacts == expectedContacts && battle.Tick - finisher.Tick >= 200)
            {
                CombinedSessionSave window = scene.Capture();
                CrossoverMixedBattleSnapshot native = JsonSerializer.Deserialize<CrossoverMixedBattleSnapshot>(window.MixedBattlePayload!)!;
                Assert.NotNull(native.Actions?.Limit?.Rough);
                SceneCameraPlacement camera = battle.GuestRenderCameraOutput;
                ColdContinue(window);
                battle = scene.Session!.Battle!;
                Assert.Equal(camera, battle.GuestRenderCameraOutput);
                Assert.Equal(finisher, battle.Action!.GuestLimit!.PendingHit);
                restoredFinisher = true;
            }
            if (battle.ReadyPlayer == 0)
            {
                if (!submitted && useLimit)
                {
                    Assert.Equal(Ff7BattleCommand.Limit, battle.Commands(0).First().Command);
                }
                Press(Keys.Enter);
                Press(Keys.Enter);
                submitted = true;
            }
            if (scene.MixedTriggerWindowOpen)
            {
                if (useLimit && !restoredLimit)
                {
                    CombinedSessionSave window = scene.Capture();
                    Assert.NotNull(window.Ff7!.Battle!.Action!.GuestLimit);
                    CrossoverMixedBattleSnapshot native = JsonSerializer.Deserialize<CrossoverMixedBattleSnapshot>(window.MixedBattlePayload!)!;
                    CrossoverGuestRenzokukenHostState limit = Assert.IsType<CrossoverGuestRenzokukenHostState>(native.Actions?.Limit);
                    Assert.Equal((byte)0, limit.Plan.Finisher);
                    expectedContacts = limit.Plan.Variant == 141 ? 6 : 5;
                    SceneCameraPlacement camera = battle.GuestRenderCameraOutput;
                    ColdContinue(window);
                    Assert.True(scene.MixedTriggerWindowOpen);
                    Assert.Equal(camera, scene.Session!.Battle!.GuestRenderCameraOutput);
                    restoredLimit = true;
                }
                Press(Keys.G);
                triggered = true;
            }
            scene.Update(new(TimeSpan.FromSeconds((frame + 1) / 60d), TimeSpan.FromSeconds(1d / 60)), new KeyboardState());
            Assert.Null(scene.Error);
            presentedContacts = Math.Max(presentedContacts, scene.Session!.Battle!.Action?.GuestLimit?.PresentedContacts ?? 0);
            if (frame % 17 == 0)
            {
                Draw();
            }
        }
        Assert.True(submitted && triggered);
        if (useLimit)
        {
            Assert.True(restoredLimit && restoredFinisher);
            Assert.True(presentedContacts >= expectedContacts, $"Expected {expectedContacts} original Limit and finisher contacts, got {presentedContacts}.");
            Assert.True(leasedCameras.Count > 2 && leasedCameras.Select(camera => camera.FocalLength).Distinct().Count() > 1);
            Assert.Null(scene.Session!.Battle!.GuestRenderCameraOutput.Basis);
        }
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
        if (useLimit)
        {
            return;
        }
        scene.Session.AdvanceTick();
        string dormant = JsonSerializer.Serialize(scene.Session.Progress.Characters.Single(character => character.Id == 0));
        int fieldActor = scene.Session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Entity;
        scene.Session.Host.Execute(new(9000001, fieldActor, 7, 0, new FieldHitPointCommand(0, -100)));
        Assert.Equal((ushort)(committed.GuestBattleSettlements[0].SettledHp - 100), scene.Session.GuestFieldVitals!.Read().Hp);
        scene.Session.Host.Execute(new(9000002, fieldActor, 7, 0, new FieldPartyRecovery(true)));
        Assert.Equal((ushort)486, scene.Session.GuestFieldVitals.Read().Hp);
        Assert.Equal(dormant, JsonSerializer.Serialize(scene.Session.Progress.Characters.Single(character => character.Id == 0)));

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

        void ColdContinue(CombinedSessionSave saved)
        {
            using MemoryStream standalone = new();
            CombinedSessionCodec.Write(standalone, saved);
            standalone.Position = 0;
            scene.Continue(CombinedSessionCodec.Read(standalone));
            Draw();
        }

        Ff7MovieSource Movie(int id)
        {
            Ff7MovieResources resource = Ff7MovieResources.Load(installation, id);
            Ff7MovieCameraTrack? track = resource.Cameras is { } cameras ? new(cameras) : null;
            return new(resource.Path, new MovieInformation(320, 240, 15, 120, 1800), 1800, 15) { CameraRecordCount = resource.Cameras?.Count ?? 0, SnapshotCameraFrame = track is null ? null : (frame, words) => track.SnapshotAt(frame, words) };
        }
    }
}
