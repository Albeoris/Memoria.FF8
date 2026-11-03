using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePartyRelevantAbsenceTests
{
    [Fact]
    public void InactiveNonpartyTemplatesDoNotRequireUnrepresentedAppearanceGuards()
    {
        string snapshot = TestResources.FixturePath("App.Tests.NativePartyRelevantAbsenceTests.1");
        byte[] original = File.ReadAllBytes(snapshot);
        GameSession session = new(GameSession.Load(snapshot));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        JsonElement before = world.Events.Capture();
        GameState state = session.State;
        Assert.Null(world.Events.PlayerActor);
        Assert.False(world.Events.EventMemory.HasRepresentedExactBytes(721, 1));
        NativePlayerAbsenceReceipt receipt = Assert.IsType<NativePlayerAbsenceReceipt>(world.Events.InspectNativePlayerAbsence());
        Assert.Equal(121, receipt.FieldId);
        Assert.Contains(receipt.Reads, read => read.Address == 720);
        Assert.Contains(receipt.Reads, read => read.Address == 256 && read.Width == 2);
        Assert.DoesNotContain(receipt.Reads, read => read.Address is 721 or 722 or 723 or 624);
        Assert.Null(world.Events.ReadPersistentPredicateFingerprint([new("PSHM_B", 721, 1)]));
        Assert.Same(state, session.State);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));

        world.Events.Scene.Party[1] = 1;
        Assert.Null(world.Events.InspectNativePlayerAbsence());
        Assert.NotEqual(receipt.Fingerprint, world.Events.ReadPersistentPredicateFingerprint(receipt.Reads));
        world.Events.Scene.Party[1] = 255;
        Assert.Equal(receipt.Fingerprint, world.Events.ReadPersistentPredicateFingerprint(receipt.Reads));

        string sourceSnapshot = TestResources.FixturePath("App.Tests.NativePartyRelevantAbsenceTests.2");
        byte[] sourceOriginal = File.ReadAllBytes(sourceSnapshot);
        GameSession sourceSession = new(GameSession.Load(sourceSnapshot));
        using GameWorld source = new(sourceSession, TestResources.GameDirectory, validateWorldPresentation: false);
        source.EnsureField();
        Assert.Equal(125, sourceSession.State.FieldId);
        Assert.True(source.Events.CanAcceptMovementInput);
        Assert.Equal(receipt.Fingerprint, source.Events.ReadPersistentPredicateFingerprint(receipt.Reads));
        Assert.Equal(original, File.ReadAllBytes(snapshot));
        Assert.Equal(sourceOriginal, File.ReadAllBytes(sourceSnapshot));
    }
}
