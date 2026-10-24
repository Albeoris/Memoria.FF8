using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSavedDuelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedStandaloneDuelAcceptsCommandsOrTimesOutAndRestores(bool enterCommands)
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Select an ordinary battle save and an output directory.");
        GameState source = GameSession.Load(input!);
        BattleSnapshot initial = Assert.IsType<BattleSnapshot>(source.Battle);
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        GameSession host = new(source);
        using GameWorld world = new(host, game.GameDirectory, sharedAssets: game);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant actor = battle.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeLimit?.Command == 11);
        BattleCombatant target = battle.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsActive && candidate.IsAlive);
        Assert.True(battle.CanChoosePlayerCommand(actor.Id));
        Assert.True(battle.GetNativeLimitLevel(actor.Id) > 0);
        battle.QueueAction(actor.Id, target.Id, BattleAbility.LimitBreak, 11 << 16);
        TimeSpan elapsed = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        string directory = Path.Combine(output!, enterCommands ? "commands" : "timeout");
        Directory.CreateDirectory(directory);
        bool ready = false;
        int moves = 0;
        int hp = target.CurrentHp;
        for (int callback = 0; callback < 1600; callback++)
        {
            battle.AdvanceNativeGraphicsPresentation(elapsed);
            world.SynchronizeBattlePresentation();
            if (battle.GetNativeDuel(actor.Id) is { Phase: BattleNativeDuelPhase.Ready } duel)
            {
                if (!ready)
                {
                    ready = true;
                    Save("ready");
                    BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { });
                    UiView view = hud.BuildView();
                    view.Resize(960, 672);
                    using Bitmap bitmap = new(960, 672, PixelFormat.Format32bppArgb);
                    using (BattlePreviewCanvas canvas = new(bitmap, game.GameDirectory))
                    {
                        view.Draw(canvas, new UiTheme());
                    }
                    bitmap.Save(Path.Combine(directory, "ready.png"), ImageFormat.Png);
                    BattleSession cold = Restore(GameSession.Load(Path.Combine(directory, "ready.json")).Battle!);
                    battle.AdvanceNativeDuelPresentation(elapsed, 0);
                    cold.AdvanceNativeDuelPresentation(elapsed, 0);
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
                }
                if (enterCommands && moves < 2)
                {
                    BattleNativeDuelChoice choice = battle.GetNativeDuelChoices(actor.Id).First(candidate => candidate.Learned && !candidate.Finisher);
                    foreach (ushort button in choice.Buttons)
                    {
                        battle.AdvanceNativeDuelPresentation(elapsed, 0);
                        battle.AdvanceNativeDuelPresentation(elapsed, button);
                    }
                    Assert.Equal(BattleNativeDuelPhase.AwaitingEffect, battle.GetNativeDuel(actor.Id)!.Phase);
                    moves++;
                    Save("input-" + moves);
                }
            }
            battle.AdvanceNativeDuelPresentation(elapsed, 0);
            if (ready && battle.GetNativeDuel(actor.Id) is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
            {
                Assert.Equal(enterCommands ? 2 : 0, moves);
                if (enterCommands)
                {
                    Assert.True(target.CurrentHp < hp);
                }
                Save("completed");
                BattleSession cold = Restore(GameSession.Load(Path.Combine(directory, "completed.json")).Battle!);
                battle.AdvanceNativeGraphicsPresentation(elapsed);
                world.SynchronizeBattlePresentation();
                cold.AdvanceNativeGraphicsPresentation(elapsed);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(cold.Capture()));
                Save("completed-next");
                File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { source.FieldId, InitialFrame = initial.Frame, battle.Frame, ready, moves, BeforeHp = hp, AfterHp = target.CurrentHp, Callbacks = callback + 1, RestoredNextEqual = true }));
                return;
            }
        }
        Save("failed-boundary");
        Assert.Fail("The selected Duel must expose input, execute accepted commands and close through its original timeout control.");

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }

        void Save(string name)
        {
            world.CaptureBattleState();
            host.Save(Path.Combine(directory, name + ".json"), compact: true);
        }
    }
}
