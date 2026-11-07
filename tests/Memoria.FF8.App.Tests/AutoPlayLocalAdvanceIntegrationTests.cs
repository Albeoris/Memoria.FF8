using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayLocalAdvanceIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public void AuthenticBlockedDirectApproachCanNaturallySteerBackOntoTheNativeRoute()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayLocalAdvanceIntegrationTests.1");
        byte[] original = Hash(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(923, session.State.FieldId);
        AutoPlayTarget[] targets = world.DiscoverAutoPlayTargets().Where(target => target.CoverageId == "923:gateway:2").OrderBy(target => target.Order).ToArray();
        Assert.NotEmpty(targets);
        int callbacks = 0;
        int progress = 0;
        world.Events.NativePlayerMovementProcessed += _ => callbacks++;
        try
        {
            JsonElement before = world.Events.Capture();
            Assert.False(world.TryFindAutoPlayWalkingRoute(targets[0], out _));
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
            int? admitted = null;
            foreach (AutoPlayTarget target in targets)
            {
                for (int attempt = 0; attempt < 8 && session.State.FieldId == 923; attempt++)
                {
                    AutoPlayWalkResult result = world.WalkAutoPlayApproach(target, () => progress++);
                    if (result.Arrived)
                    {
                        result = world.WalkAutoPlayContact(target, () => progress++);
                    }
                    output.WriteLine($"Target={target.Id}; attempt={attempt}; result={result}; nativeXYZ={world.Events.Scene.NativePlayerPosition}; callbacks={callbacks}");
                    Assert.Null(world.Events.LastFault);
                    if (result.GatewayIndex is int gateway)
                    {
                        admitted = gateway;
                        break;
                    }
                    Assert.Equal(923, session.State.FieldId);
                    if (!result.Interrupted)
                    {
                        break;
                    }
                    Assert.Equal(PlayerTriggerKind.Playable, world.NextTrigger.Kind);
                    world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1));
                }
                if (admitted is not null)
                {
                    break;
                }
            }
            Assert.Equal(2, admitted);
            Assert.Equal(924, session.State.FieldId);
            Assert.True(callbacks > 0);
            Assert.True(progress > 0);
        }
        finally
        {
            Assert.Equal(original, Hash(source));
        }
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }
}
