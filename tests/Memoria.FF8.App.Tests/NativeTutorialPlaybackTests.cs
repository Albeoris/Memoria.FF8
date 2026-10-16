using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialPlaybackTests
{
    [Fact]
    public void StatusLessonsShowOriginalResistanceCompatibilityAndLearnedLimitData()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog live = LiveMenuCatalog.ReadInstalled(assets);
        foreach (int id in new[] { 5, 6, 7 })
        {
            NativeTutorialPlayer player = new(catalog, live, id);
            player.Menu.Open("status-elements");
            Assert.Equal(21, player.Menu.CurrentScreen.Details(player.Menu).Count);
            Assert.Contains(player.Menu.CurrentScreen.Details(player.Menu), line => line.StartsWith("Fire   Atk", StringComparison.Ordinal));
            Assert.NotNull(player.Menu.BuildView());
            player.Menu.Open("status-gf");
            int row = player.Menu.Character!.NativeId!.Value;
            Assert.Equal(player.Menu.Data.GuardianForces.Count, player.Menu.CurrentScreen.Details(player.Menu).Count);
            Assert.Contains(player.Party.Records[row].GuardianForceCompatibility![0].ToString(), player.Menu.CurrentScreen.Details(player.Menu)[0]);
            Assert.NotNull(player.Menu.BuildView());
            if (id == 6)
            {
                player.Menu.Open("status-duel");
                Assert.Equal(System.Numerics.BitOperations.PopCount(catalog.CreateIsolatedLimitProgress(id).DuelFlags), player.Menu.CurrentScreen.Details(player.Menu).Count);
                Assert.NotNull(player.Menu.BuildView());
            }
            if (id == 7)
            {
                player.Menu.Open("status-angelo");
                Assert.Equal(new[] { "0", "1", "4" }, player.Menu.Entries.Select(entry => entry.Id));
                Assert.Equal(player.Menu.FormatNativeTutorialText(catalog.ReadStatusKernel().RinoaAngeloAttacks[0].Name!), player.Menu.Entries[2].Label);
                Assert.Equal(new[] { "Angelo Rush", "Angelo Recover", "Angelo Cannon" }, player.Menu.Entries.Select(entry => entry.Label));
                Assert.Contains(player.Menu.CurrentScreen.Details(player.Menu), line => line.Contains("Counter 255", StringComparison.Ordinal));
                Assert.DoesNotContain(player.Menu.CurrentScreen.Details(player.Menu), line => line.Contains('%'));
                Assert.NotNull(player.Menu.BuildView());
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void EveryOriginalLessonCompletesWithExactRestoreAtMenuInputsAndBarriers(int id)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menuCatalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialDefinition definition = catalog.Get(id);
        GameSession live = new(new GameState { CharacterVitals = assets.ReadNewGameCharacterVitals() });
        JsonElement liveBefore = JsonSerializer.SerializeToElement(live.State);
        NativeTutorialPlayer player = new(catalog, menuCatalog, id);
        NativeTutorialPlayer restored = new(catalog, menuCatalog, id);
        List<int> executed = [];
        List<ushort> callbackWords = [];
        player.OperationExecuted += (pc, word) =>
        {
            executed.Add(pc);
            callbackWords.Add(word);
        };
        int restoredBoundaries = 0;
        int ticks;
        for (ticks = 0; ticks < 30000 && !player.IsComplete; ticks++)
        {
            callbackWords.Clear();
            PlayerAction action = new(player.WaitingForInput ? PlayerActionKind.Confirm : PlayerActionKind.Wait);
            Assert.True(player.Apply(action));
            Assert.True(restored.Apply(action));
            Assert.True(JsonElement.DeepEquals(player.Capture(), restored.Capture()), $"Tutorial{id} diverged after restored callback{ticks}, {JsonSerializer.Serialize(player.Status)}.");
            bool meaningful = callbackWords.Any(word => word >> 12 is 1 or 9) || player.WaitingForInput || player.IsComplete;
            if (meaningful)
            {
                JsonElement snapshot = player.Capture();
                restored = new NativeTutorialPlayer(catalog, menuCatalog, id, snapshot);
                Assert.True(JsonElement.DeepEquals(snapshot, restored.Capture()), $"Tutorial{id} lost its exact menu/input/delay boundary.");
                Assert.Equal(player.Menu.ScreenId, restored.Menu.ScreenId);
                Assert.Equal(player.Menu.SelectedEntry?.Id, restored.Menu.SelectedEntry?.Id);
                Assert.True(JsonElement.DeepEquals(liveBefore, JsonSerializer.SerializeToElement(live.State)), "Tutorial menu actions changed a live game owner.");
                restoredBoundaries++;
            }
        }
        Assert.True(player.IsComplete, $"Tutorial{id} did not complete within30000 callbacks: {JsonSerializer.Serialize(player.Status)}.");
        int terminal = definition.Inputs.IndexOf((ushort)0xF000);
        Assert.Equal(Enumerable.Range(0, terminal + 1), executed);
        Assert.True(restoredBoundaries > 1);
        Assert.True(JsonElement.DeepEquals(liveBefore, JsonSerializer.SerializeToElement(live.State)));
        Assert.NotSame(live.State.CharacterVitals, player.Party);
        if (id == 4)
        {
            Assert.Equal((byte)10, player.Party.GuardianForces[1].Learning);
        }
        Console.WriteLine($"Original tutorial{id}: {ticks} callbacks, {executed.Count} original words, {restoredBoundaries} independently restored menu/input/delay boundaries; live owners unchanged.");
    }
}
