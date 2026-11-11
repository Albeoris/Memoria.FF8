using System.Reflection;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AchievementNoticeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Progress_respects_preferences_and_unlocks_always_notify(bool showProgress, bool unlocked)
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-achievement-notice-" + Guid.NewGuid().ToString("N"));
        try
        {
            using RecreateGame game = new(new LaunchOptions { MuteAudio = true, AchievementsLocalOnly = true, SettingsDirectory = directory }, new GameSession(new GameState()));
            typeof(RecreateGame).GetField("uiPreferences", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, new UiPreferences { ShowAchievementProgress = showProgress });
            typeof(RecreateGame).GetMethod("ShowSaveNotice", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(string), typeof(double)])!.Invoke(game, ["Quick Saved", 5d]);
            AchievementSnapshot previous = new(new("test-progress"), 0, 100, false, Name: "Test achievement");
            AchievementSnapshot current = previous with { Counter = unlocked ? 100 : 1, Unlocked = unlocked };

            typeof(RecreateGame).GetMethod("PresentAchievementChange", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, [new AchievementChange(previous, current)]);

            NoticeWindow notice = (NoticeWindow)typeof(RecreateGame).GetField("noticeWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
            Assert.Equal(unlocked ? "Achievement unlocked: Test achievement" : showProgress ? "Test achievement: 1/100" : "Quick Saved", notice.Text);
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
