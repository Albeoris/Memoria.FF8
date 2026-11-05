using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleStageOverlayCheckpointTests
{
    [Fact]
    public void Installed_stage_overlay_keeps_screen_coverage_after_world_restore()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_STAGE_OVERLAY_SAVE");
        Assert.SkipUnless(source is not null && File.Exists(source), "Set MEMORIA_FF8_STAGE_OVERLAY_SAVE to an optional standalone save before a stage color overlay encounter.");
        string directory = TestResources.GameDirectory;
        using GameResourceHost resources = new(directory, watch: false);
        resources.ConnectEncounterMods();
        using GameAssets assets = new(directory, resources: resources.Resources, modelOverrides: resources);
        GameSession session = new(PlayerStateLoader.Load(source!, directory, sharedAssets: assets, resources: resources.Resources, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy));
        using GameWorld world = World(session);
        world.EnsureField();
        bool diagnose = Environment.GetEnvironmentVariable("MEMORIA_FF8_STAGE_OVERLAY_DIAGNOSE") == "1";
        if (diagnose)
        {
            Describe(world, session);
        }
        string? configuredActions = Environment.GetEnvironmentVariable("MEMORIA_FF8_STAGE_OVERLAY_ACTIONS");
        PlayerAction[]? actions = configuredActions is null ? null : HeadlessPlayer.ReadActions(File.ReadAllText(configuredActions));
        string? configuredTarget = Environment.GetEnvironmentVariable("MEMORIA_FF8_STAGE_OVERLAY_TARGET");
        int? targetObject = configuredTarget is null ? null : int.Parse(configuredTarget, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(actions is null || actions.Length <= 1500 && actions.All(action => action.Frames is >= 1 and <= 1500) && actions.Sum(action => action.Frames) <= 1500, "Optional stage overlay actions must contain at most 1500 input frames.");
        int limit = actions?.Length ?? 1500;
        for (int tick = 0; tick < limit && world.Battle is null && !world.HasPendingBattle; tick++)
        {
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction input = actions?[tick] ?? (dialogue?.FirstChoice is int choice ? new(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable ? new(PlayerActionKind.Move, Vertical: -1) : new(PlayerActionKind.Wait));
            if (actions is null && targetObject is int objectId && dialogue is null && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                ActorState target = Assert.Single(world.Events.Actors, actor => actor.ObjectId == objectId);
                ActorState player = Assert.IsType<ActorState>(world.Events.PlayerActor);
                float dx = target.X - player.X;
                float dy = target.Y - player.Y;
                input = dx * dx + dy * dy <= 80 * 80 ? new(PlayerActionKind.Confirm) : HeadlessPlayer.FieldGuideAction(dx, dy, world.Events.InputAxisDirection + world.Events.PlayerInputDirectionOffset);
            }
            PlayerActionResult result = world.Apply(input);
            Assert.Null(world.Events.LastFault);
            if (diagnose && (actions is not null || tick % 120 == 119 || tick == limit - 1))
            {
                Console.WriteLine($"Input {tick}: {JsonSerializer.Serialize(input)}; accepted {result.Accepted}, detail {result.Detail}.");
                Describe(world, session);
            }
        }
        if (world.Battle is null && !world.HasPendingBattle)
        {
            Describe(world, session);
        }
        Assert.True(world.Battle is not null || world.HasPendingBattle, $"The supplied save did not reach an encounter within the bounded natural input route; field {session.State.FieldId}, frame {world.Events.Frame}, trigger {world.Events.NextTrigger.Kind}.");
        if (world.Battle is null)
        {
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: session.State.GameplayPreferences.AtbMode, NativeSeed: 0));
        }
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleDefinition definition = battle.ReadDefinition();
        uint? callback = definition.NativeStageSource?.Callback?.Address;
        Assert.True(callback is 0x518440 or 0x5180C0, $"Encounter {definition.EncounterId}, stage {definition.StageId} uses callback {callback:X8}; supply a save reaching a full-screen stage color overlay.");
        for (int tick = 0; tick < 600 && (!battle.NativeEntryReady || !Commands(battle).Any(command => command.Opcode == 0x62)); tick++)
        {
            Advance(world);
        }
        Assert.True(battle.NativeEntryReady);
        AssertOverlay(battle);
        world.CaptureBattleState();
        session.SetEventState(world.Events.Capture());
        GameState checkpoint = session.PrepareSnapshot();
        string json = JsonSerializer.Serialize(checkpoint);
        GameState restoredState = JsonSerializer.Deserialize<GameState>(json)!;
        using GameWorld restored = World(new GameSession(restoredState));
        restored.EnsureField();
        BattleSession restoredBattle = Assert.IsType<BattleSession>(restored.Battle);
        AssertOverlay(restoredBattle);
        Console.WriteLine($"Stage overlay: field {checkpoint.FieldId}, field frame {world.Events.Frame}, encounter {definition.EncounterId}, stage {definition.StageId}, callback {callback:X8}, battle frame {battle.Frame}, graphics callback {battle.NativeGraphics!.Callback}.");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_STAGE_OVERLAY_OUTPUT");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "stage-overlay-standalone.json"), json);
            File.WriteAllText(Path.Combine(output, "stage-overlay-battle.json"), JsonSerializer.Serialize(checkpoint.Battle));
            BattleScenePresentation scene = new(world.BattleResources, definition, true);
            byte[] pixels = scene.Render(battle, 1280, 720);
            using var bitmap = Memoria.FF8.GameUi.BattlePreviewCanvas.BitmapFromRgba(pixels, 1280, 720);
            bitmap.Save(Path.Combine(output, "stage-overlay.png"));
        }
        Advance(world);
        Advance(restored);
        AssertOverlay(battle);
        AssertOverlay(restoredBattle);
        Assert.Equal(battle.NativeGraphics!.Callback, restoredBattle.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restoredBattle.Capture()));

        GameWorld World(GameSession selected) => new(selected, directory, resources: resources.Resources, resolveGameFile: resources.ResolveGameFile, sharedAssets: assets, readSceneMods: () => resources.SceneMods, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy, applyEncounterMods: resources.ApplyEncounterMods);
    }

    private static IEnumerable<NativeBattleLogicalCommand> Commands(BattleSession battle)
    {
        return battle.NativeGraphics?.PrimitiveFrame?.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank is "1D9BDC8" or "1D9B958") ?? [];
    }

    private static void AssertOverlay(BattleSession battle)
    {
        NativeBattleLogicalCommand[] tiles = Commands(battle).Where(command => command.Opcode == 0x62).ToArray();
        Assert.NotEmpty(tiles);
        foreach (NativeBattleLogicalCommand tile in tiles)
        {
            Assert.Equal(NativeBattleScreenCoverage.Full, tile.ScreenCoverage);
            Assert.Equal(3, tile.Words.Length);
            Assert.Equal(0u, tile.Words[1]);
            Assert.Equal(320u | 224u << 16, tile.Words[2]);
        }
        foreach (NativeBattleLogicalCommand mode in Commands(battle).Where(command => command.Opcode == 0xE1))
        {
            Assert.Null(mode.ScreenCoverage);
        }
    }

    private static void Advance(GameWorld world)
    {
        world.Battle!.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30 + 1));
        world.SynchronizeBattlePresentation();
    }

    private static void Describe(GameWorld world, GameSession session)
    {
        Console.WriteLine($"Field {session.State.FieldId}, frame {world.Events.Frame}, trigger {world.NextTrigger.Kind}, input direction {world.Events.InputAxisDirection + world.Events.PlayerInputDirectionOffset}, full playthrough {session.State.FullBattlePlaythrough}, music keep {session.State.WorldControls?.MusicKeep207}; encounters {JsonSerializer.Serialize(session.State.RandomEncounters)}; field encounters {JsonSerializer.Serialize(world.Field?.RandomEncounters)}.");
        foreach (ActorState actor in world.Events.Actors)
        {
            Console.WriteLine($"Actor {actor.ObjectId}: active {actor.Active}, visible {actor.Visible}, talk {actor.TalkEnabled}, ({actor.X},{actor.Y},{actor.Z}), triangle {actor.WalkmeshTriangle}.");
        }
    }
}
