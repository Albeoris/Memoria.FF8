using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeDreamPlayerAbsenceTests
{
    [Fact]
    public void ActualUnboundDreamArrivalHasAReadOnlyPortableReceiptWithoutCreatingAPlayer()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeDreamPlayerAbsenceTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        JsonElement before = world.Events.Capture();
        GameState state = session.State;

        NativePlayerAbsenceReceipt receipt = Assert.IsType<NativePlayerAbsenceReceipt>(world.Events.InspectNativePlayerAbsence());

        Assert.Equal(742, receipt.FieldId);
        Assert.Equal(world.ReadAutoPlayFieldProgramHash(742), receipt.ProgramHash);
        Assert.Equal(receipt.Fingerprint, world.Events.ReadPersistentPredicateFingerprint(receipt.Reads));
        Assert.True(session.State.CharacterVitals!.DreamEnabled);
        Assert.Null(world.Events.PlayerActor);
        Assert.Equal(PlayerTriggerKind.Waiting, world.Events.NextTrigger.Kind);
        Assert.All(world.Events.Scene.NativeActorObjectIds, objectId => Assert.Equal(-2, world.Events.Actors[objectId].CharacterId));
        Assert.Same(state, session.State);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));

        GameSession sourceSession = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeDreamPlayerAbsenceTests.2")));
        using GameWorld source = new(sourceSession, TestResources.GameDirectory, validateWorldPresentation: false);
        source.EnsureField();
        Assert.Equal(receipt.Fingerprint, source.Events.ReadPersistentPredicateFingerprint(receipt.Reads));
    }

    [Fact]
    public void TheSamePartyWithoutTheActualDreamFlagDoesNotReuseItsSalarySuppressionProof()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeDreamPlayerAbsenceTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        NativePlayerAbsenceReceipt receipt = Assert.IsType<NativePlayerAbsenceReceipt>(world.Events.InspectNativePlayerAbsence());
        int[] party = world.Events.Scene.Party.ToArray();
        session.SetCharacterVitals(session.State.CharacterVitals! with { DreamEnabled = false });
        JsonElement changed = world.Events.Capture();

        Assert.Equal(party, world.Events.Scene.Party);
        Assert.NotEqual(receipt.Fingerprint, world.Events.ReadPersistentPredicateFingerprint(receipt.Reads));
        Assert.Null(world.Events.InspectNativePlayerAbsence());
        Assert.True(JsonElement.DeepEquals(changed, world.Events.Capture()));
    }

    [Fact]
    public void DreamSuppressionDoesNotHideAnOwnedSalaryDisplayCountdown()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeDreamPlayerAbsenceTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.NotNull(world.Events.InspectNativePlayerAbsence());
        session.SetNativeEventOwners(session.State.WorldControls!, seeD: session.State.SeeD! with { SalaryDisplayCountdown184 = 1 });
        JsonElement before = world.Events.Capture();

        Assert.Null(world.Events.InspectNativePlayerAbsence());
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
    }
}
