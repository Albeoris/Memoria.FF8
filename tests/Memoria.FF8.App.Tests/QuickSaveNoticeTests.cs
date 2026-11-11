using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
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
            string gameDirectory = TestResources.GameDirectory;
            using GameAssets assets = new(gameDirectory);
            GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, true);
            GameSession session = new(GameplaySaveSessionLoader.Restore(initial, gameDirectory, sharedAssets: assets));
            string checkpoint = Path.Combine(directory, "checkpoint.json");
            using RecreateGame game = new(new LaunchOptions { MuteAudio = true, AchievementsLocalOnly = true, SettingsDirectory = directory, SavePath = checkpoint, GameDirectory = gameDirectory }, session);
            PlayerSaveStore store = new(checkpoint, Path.Combine(directory, "saves"));
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RecreateGame).GetField("ff8PlayerSaves", privateInstance)!.SetValue(game, store);
            GameWorld world = new(session, gameDirectory, sharedAssets: assets);
            typeof(RecreateGame).GetField("world", privateInstance)!.SetValue(game, world);
            world.EnsureField();

            typeof(RecreateGame).GetMethod("SavePlayerSlot", privateInstance)!.Invoke(game, [0]);

            Assert.True(File.Exists(store.GetPath(0)));
            NoticeWindow notice = (NoticeWindow)typeof(RecreateGame).GetField("noticeWindow", privateInstance)!.GetValue(game)!;
            Assert.Equal("Quick Saved", notice.Text);
            Assert.Equal(1d, notice.RemainingSeconds);
            notice.Advance(TimeSpan.FromSeconds(0.5));
            Assert.Equal("Quick Saved", notice.Text);
            notice.Advance(TimeSpan.FromSeconds(0.5));
            Assert.Null(notice.Text);
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
