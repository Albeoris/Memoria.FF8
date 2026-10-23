using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class SlotsCardHostInputTests
{
    [Fact]
    public void Pending_choice_blocks_host_command_bypasses_without_changing_the_deal()
    {
        using GameAssets installed = new(TestResources.GameDirectory);
        using BattleAssets assets = new(installed.GameDirectory);
        BattleSession battle = CreateBattle(installed, assets);
        BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("selphie");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetGameplayPreferences(new() { SelphieCardSlots = true });
        session.SetBattle(battle.Capture());
        string directory = Path.Combine(Path.GetTempPath(), "ff8-slots-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            LaunchOptions options = new() { GameDirectory = installed.GameDirectory, SavePath = Path.Combine(directory, "session.json"), MuteAudio = true };
            using RecreateGame host = new(options, session);
            typeof(RecreateGame).GetField("debugBattle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, battle);
            string before = JsonSerializer.Serialize(battle.Capture());
            GameState stateBefore = session.State;
            foreach (string command in new[] { "debug-room", "ui-open", "ui-input", "new-game", "battle-start", "battle-close", "battle-debug-open", "battle-debug-close", "battle-settings", "action", "advance" })
            {
                AssertOwned(() => Invoke(host, "DispatchControl", new ControlRequest("blocked", command)));
            }
            foreach (string command in new[] { "battle-start", "battle-close", "battle-debug-open", "battle-debug-close", "battle-debug-input", "battle-settings" })
            {
                AssertOwned(() => Invoke(host, "DispatchBattleControl", new ControlRequest("blocked", command)));
            }
            foreach (BattleInputKind kind in Enum.GetValues<BattleInputKind>().Where(kind => kind != BattleInputKind.Tick))
            {
                BattleInput input = new(kind, ActorId: "selphie", TargetId: "enemy");
                AssertOwned(() => Invoke(host, "ApplyBattleCommand", input));
                ControlRequest request = new("blocked", "battle-input", Battle: new(Input: input));
                AssertOwned(() => Invoke(host, "DispatchControl", request));
                AssertOwned(() => Invoke(host, "DispatchBattleControl", request));
            }
            Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
            Assert.Same(stateBefore, session.State);
            Assert.Same(battle, typeof(RecreateGame).GetField("debugBattle", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host));
            Assert.Equal(deal.Id, battle.PendingSlotsDeal!.Id);
            Assert.Null(battle.PendingSlotsDeal.SelectedIndex);

            Assert.NotNull(Invoke(host, "DispatchControl", new ControlRequest("query", "status", Compact: true)));
            Assert.NotNull(Invoke(host, "DispatchControl", new ControlRequest("query", "battle-status")));
            Assert.NotNull(Invoke(host, "DispatchBattleControl", new ControlRequest("query", "battle-status")));
            Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));

            string checkpoint = Path.Combine(directory, "pending-choice.json");
            Invoke(host, "DispatchControl", new ControlRequest("save", "save", Path: checkpoint, Compact: true));
            GameState restored = GameSession.Load(checkpoint);
            Assert.True(restored.GameplayPreferences.SelphieCardSlots);
            Assert.Equal(JsonSerializer.Serialize(deal), JsonSerializer.Serialize(restored.Battle!.PendingSlotsDeal));
            Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));

            // A completed/replaced battle must release the old HUD's exclusive host ownership.
            typeof(RecreateGame).GetField("battleHud", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, new BattleHudController(battle, battle.QueueAttack, () => { }, () => { }));
            typeof(RecreateGame).GetField("presentedBattle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, battle);
            typeof(RecreateGame).GetField("slotsInputReleasePending", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, true);
            PropertyInfo ownership = typeof(RecreateGame).GetProperty("SlotsModalBlocksGameplay", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.True((bool)ownership.GetValue(host)!);
            typeof(RecreateGame).GetField("debugBattle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, null);
            Assert.False((bool)ownership.GetValue(host)!);
            typeof(RecreateGame).GetField("debugBattle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, CreateBattle(installed, assets));
            Assert.False((bool)ownership.GetValue(host)!);
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                File.Delete(file);
            }
            Directory.Delete(directory);
        }
    }

    private static BattleSession CreateBattle(GameAssets game, BattleAssets assets)
    {
        BattleSession battle = new(new BattleDefinition
        {
            SelphieCardSlots = true,
            NativeGlobalRandom = new(17),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = assets.NativeActions.ToArray(),
            NativeLimitsDisabled = false,
            LimitProgress = game.ReadNewGameLimitProgress(),
            Combatants = [new() { Id = "selphie", Name = "Selphie", IsPlayer = true, ModelId = 5, Level = 20, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(5) }, new() { Id = "enemy", Name = "Enemy", Speed = 1 }]
        }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("selphie", 1)] });
    }

    private static void AssertOwned(Action action)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("Slots card choice owns", error.Message);
    }

    private static object? Invoke(RecreateGame host, string name, params object[] arguments)
    {
        try
        {
            return typeof(RecreateGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }
}
