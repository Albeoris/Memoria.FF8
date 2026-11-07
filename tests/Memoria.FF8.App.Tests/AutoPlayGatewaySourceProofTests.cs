using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayGatewaySourceProofTests
{
    [Fact]
    public void ValidationUsesTheSameFilteredGatewayIndicesAsTheNativeObserver()
    {
        FieldGateway absent = Gateway(0x7FFF, -1);
        FieldGateway disabled = Gateway(911, -1);
        FieldGateway first = Gateway(910, 1);
        FieldGateway world = Gateway(6, -1);
        FieldGateway last = Gateway(912, 2);
        FieldInformation information = new(0, 0, [], [], [absent, disabled, first, world, last], []);

        Assert.True(GameWorld.VerifyAutoPlayGatewayDestination(information, 0, 910));
        Assert.False(GameWorld.VerifyAutoPlayGatewayDestination(information, 0, 911));
        Assert.False(GameWorld.VerifyAutoPlayGatewayDestination(information, 1, 6));
        Assert.True(GameWorld.VerifyAutoPlayGatewayDestination(information, 2, 912));
        Assert.False(GameWorld.VerifyAutoPlayGatewayDestination(information, -1, 910));
        Assert.False(GameWorld.VerifyAutoPlayGatewayDestination(information, 3, 910));
    }

    [Fact]
    public void AnUnsupportedSentinelArrivalCannotCertifyAFieldRoute()
    {
        FieldGateway sentinel = Gateway(910, 1) with { DestinationX = short.MaxValue };
        FieldInformation information = new(0, 0, [], [], [sentinel], []);

        Assert.False(GameWorld.VerifyAutoPlayGatewayDestination(information, 0, 910));
    }

    [Fact]
    public void NativeSavedArrivalAndImportedSourceInfAgreeWithoutChangingTheScene()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayGatewayDestinationTests.1");
        byte[] original = Hash(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(910, session.State.FieldId);
        Assert.Equal<ushort?>(920, world.Events.Scene.PreviousFieldWord84);
        JsonElement before = world.Events.Capture();
        try
        {
            Assert.True(world.VerifyAutoPlayGatewayDestination(920, 0, 910));
            Assert.False(world.VerifyAutoPlayGatewayDestination(920, 0, 911));
            Assert.False(world.VerifyAutoPlayGatewayDestination(919, 0, 910));
            Assert.False(world.VerifyAutoPlayGatewayDestination(920, -1, 910));
            Assert.False(world.VerifyAutoPlayGatewayDestination(920, int.MaxValue, 910));
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        }
        finally
        {
            Assert.Equal(original, Hash(source));
        }
    }

    private static FieldGateway Gateway(ushort destination, short triangle)
    {
        return new(new(0, 0, 0), new(100, 0, 0), 10, 20, triangle, destination, 0, 0, 0);
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }
}
