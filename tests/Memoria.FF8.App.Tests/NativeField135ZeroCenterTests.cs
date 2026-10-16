using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeField135ZeroCenterTests
{
    [Fact]
    public void AuthenticLavaUsesSharedCurrentWrapAndRejectsTheRemovedLegacyFrame()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        GameState recreated = NativeField135LinearScrollTests.RecreateLava(directory, source);
        using var world = new GameWorld(new GameSession(recreated), directory);
        world.EnsureField();
        BackgroundScrollFrame frame = NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors)!;
        Assert.Equal(Vector2.Zero, frame.ActorOffset);
        Assert.Equal(1, frame.NativeWrap!.NativeNormalVersion);
        Assert.True(NormalBackgroundWrapCertificate.Supports(world.Field!, world.Events.Scene));
        var renderer = new FieldRenderer(world.Field!);
        Assert.NotEmpty(renderer.Render(world.Events.BackgroundStates, [], TimeSpan.Zero, nativeWrapFrame: frame.NativeWrap).ToArray());
        FieldNativeBackgroundWrapFrame legacy = frame.NativeWrap with { NativeNormalVersion = 0 };
        Assert.Throws<NotSupportedException>(() => renderer.Render(world.Events.BackgroundStates, [], TimeSpan.Zero, nativeWrapFrame: legacy));
    }
}
