using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class QuickSaveNoticeTests
{
    [Fact]
    public void Successful_quick_save_shows_confirmation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-quick-notice-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameSession session = new(new GameState { Scene = SceneKind.Movie, EventState = null });
            using RecreateGame game = new(new LaunchOptions { MuteAudio = true, AchievementsLocalOnly = true, SettingsDirectory = directory }, session);
            PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "saves"));
            typeof(RecreateGame).GetField("playerSaves", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, store);

            typeof(RecreateGame).GetMethod("SavePlayerSlot", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, [0]);

            Assert.True(File.Exists(store.GetPath(0)));
            Func<string> notice = (Func<string>)typeof(RecreateGame).GetField("saveNotice", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
            Assert.Equal("Quick Saved", notice());
            Assert.Equal(5d, typeof(RecreateGame).GetField("saveNoticeSeconds", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
