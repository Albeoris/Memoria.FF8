using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class ControlExclusiveLaunchOptionsTests
{
    private static string Root => Path.Combine(Path.GetTempPath(), "ff8-control-exclusive-options");

    [Fact]
    public void ExplicitControlledHostRetainsNormalStoryAndOptionalQueueStepping()
    {
        LaunchOptions options = LaunchOptions.Parse([.. Arguments(), "--control-step"]);
        Assert.True(options.ControlExclusive);
        Assert.False(options.AcceptsPhysicalInput);
        Assert.False(options.IsDiagnosticGraphicalHost);
        Assert.True(options.ControlStep);
        Assert.Equal(SceneKind.Title, options.InitialState.Scene);
        Assert.Equal(Path.Combine(Root, "save", "player-saves"), options.IsolatedPlayerSaveDirectory);
    }

    [Theory]
    [InlineData("--control-dir")]
    [InlineData("--settings-dir")]
    [InlineData("--save-state")]
    public void IsolationPathsMustBeExplicit(string missing)
    {
        List<string> arguments = Arguments().ToList();
        int index = arguments.IndexOf(missing);
        arguments.RemoveRange(index, 2);
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(arguments.ToArray()));
    }

    [Theory]
    [InlineData("--settings-dir", "control")]
    [InlineData("--settings-dir", "control/inside")]
    [InlineData("--control-dir", "save/inside")]
    public void ControlledDirectoriesCannotOverlap(string option, string relative)
    {
        string[] arguments = Arguments();
        arguments[Array.IndexOf(arguments, option) + 1] = Path.Combine(Root, relative);
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(arguments));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalUserConfigurationAndPlayerSavesAreExcluded(bool saves)
    {
        string[] arguments = Arguments();
        arguments[Array.IndexOf(arguments, "--settings-dir") + 1] = saves ? ApplicationPaths.Current.Saves : ApplicationPaths.Current.Configuration;
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(arguments));
    }

    [Fact]
    public void InputSnapshotCannotAlsoBeTheWritableCheckpoint()
    {
        string[] arguments = Arguments();
        string destination = arguments[Array.IndexOf(arguments, "--save-state") + 1];
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse([.. arguments, "--state", destination]));
    }

    [Fact]
    public void OrdinaryControlQueueAndSettingsOverrideKeepNormalInputAndSaveBankPolicy()
    {
        LaunchOptions options = LaunchOptions.Parse(["--control-dir", Path.Combine(Root, "control"), "--settings-dir", Path.Combine(Root, "settings")]);
        Assert.False(options.ControlExclusive);
        Assert.True(options.AcceptsPhysicalInput);
        Assert.Null(options.IsolatedPlayerSaveDirectory);
    }

    [Fact]
    public void ExistingDiagnosticSaveBankRemainsUnderItsSettingsDirectory()
    {
        LaunchOptions options = LaunchOptions.Parse([.. Arguments().Where(argument => argument != "--control-exclusive"), "--battle-test", "fixture.json"]);
        Assert.False(options.AcceptsPhysicalInput);
        Assert.Equal(Path.Combine(Root, "settings", "player-saves"), options.IsolatedPlayerSaveDirectory);
    }

    private static string[] Arguments() => ["--control-exclusive", "--control-dir", Path.Combine(Root, "control"), "--settings-dir", Path.Combine(Root, "settings"), "--save-state", Path.Combine(Root, "save", "checkpoint.json")];
}
