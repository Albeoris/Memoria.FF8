using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MagicSavedMenuPerformanceTests
{
    [Fact]
    public void Standalone_magic_navigation_reports_cost_without_advancing_gameplay()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_MAGIC_MENU_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && directory is not null, "Select a standalone save and installed resources.");
        GameState state = GameSession.Load(input!);
        using GameAssets assets = new(directory!);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        MenuData data = LiveMenuData.Build(state, catalog: catalog);
        int projections = 0;
        MenuController menu = new(() =>
        {
            projections++;
            return data;
        }, _ => Assert.Fail("Navigation must not apply gameplay actions."));
        List<object> samples = [];
        foreach (bool sorted in new[] { false, true })
        {
            menu.Open("magic");
            if (sorted)
            {
                menu.Execute(new MenuAction("magic-sort"));
            }
            foreach (string screen in new[] { "magic", "magic-stock", "magic-use" })
            {
                menu.Open(screen);
                Assert.NotEmpty(menu.Entries);
                Draw();
                int initialProjections = projections;
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                for (int index = 0; index < 10; index++)
                {
                    int before = menu.SelectedIndex;
                    menu.Navigate(MenuInput.Down);
                    Assert.NotEqual(before, menu.SelectedIndex);
                    Draw();
                }
                double milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                samples.Add(new { Screen = screen, Sorted = sorted, Steps = 10, Milliseconds = milliseconds, AllocatedBytes = bytes, Entries = menu.Entries.Count });
                Console.WriteLine($"{screen}, sorted {sorted}: 10 steps {milliseconds:F3} ms, {bytes:N0} bytes.");
                Assert.Equal(initialProjections, projections);
            }
        }
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MAGIC_MENU_OUTPUT");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "magic-menu-performance.json"), JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
        }

        void Draw()
        {
            UiView view = menu.BuildView();
            view.Resize(1280, 720);
            view.Draw(new BattleUiTests.TestCanvas(), new UiTheme());
        }
    }
}
