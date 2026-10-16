using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCastlePartyReserveTests
{
    [Fact]
    public void Actual575PartySelectionRefreshesBothReserveBanksOnNativeReturnAndFreshLoad()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "../fixtures")))
        {
            root = root.Parent;
        }
        string? source = root is null ? null : Path.Combine(root.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(source is not null && File.Exists(source), "The preserved original575 party-menu standalone is required.");
        GameState original = GameSession.Load(source!);
        GameSession session = new(original);
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        using GameWorld world = new(session, gameDirectory);
        world.EnsureField();
        Assert.Equal((575, 7, 110, 975), (world.Events.FieldId, world.Events.PartyMenu!.ObjectId, world.Events.PartyMenu.ScriptId, world.Events.PartyMenu.Operation));
        Assert.Equal(new byte[] { 2, 3, 4, 2, 3, 4 }, session.CharacterVitals!.JunctionPartyBackup);
        for (int callback = 0; world.Events.PartyMenu!.Phase != NativePartyMenuPhase.Active; callback++)
        {
            Assert.True(callback < 8);
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Equal(1, world.Events.PartyMenu!.LockedMask & 1);
        int slot = Array.IndexOf(world.Events.PartyMenu.TemporaryCombatParty!, (byte)5);
        Assert.InRange(slot, 0, 2);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.SelectPartySlot, slot)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.SelectPartyMember, 2)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.Equal(NativePartyMenuPhase.Closing, world.Events.PartyMenu!.Phase);
        Assert.Equal(new byte[] { 2, 0, 1 }, session.CharacterVitals!.CombatParty);
        Assert.Equal(new[] { 0, 2, 1 }, world.Events.Scene.Party);
        //PHS confirmation itself does not fabricate the reserve producer.
        Assert.Equal(new byte[] { 2, 3, 4, 2, 3, 4 }, session.CharacterVitals.JunctionPartyBackup);

        session.SetEventState(world.Events.Capture());
        GameState independentState = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession resumedSession = new(independentState);
        using GameWorld resumed = new(resumedSession, gameDirectory);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        bool observedReturnProducer = false;
        for (int callback = 0; world.Events.PartyMenu is not null; callback++)
        {
            Assert.True(callback < 12);
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            if (world.Events.PartyMenu?.Phase == NativePartyMenuPhase.FieldResumeInitialization)
            {
                observedReturnProducer = true;
                Assert.Equal(new byte[] { 3, 4, 5, 3, 4, 5 }, session.CharacterVitals!.JunctionPartyBackup);
                Assert.Equal(session.CharacterVitals.JunctionPartyBackup, resumedSession.CharacterVitals!.JunctionPartyBackup);
            }
        }
        Assert.True(observedReturnProducer);
        //Original575 caller now requests its ordinary same-field map reload.
        Assert.Equal((575, -399f, 562f), (session.State.FieldId, session.State.X, session.State.Y));
        Assert.Equal(new byte[] { 3, 4, 5, 3, 4, 5 }, session.CharacterVitals!.JunctionPartyBackup);
        world.EnsureField();
        resumed.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(575, world.Events.FieldId);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(new byte[] { 3, 4, 5, 3, 4, 5 }, session.CharacterVitals!.JunctionPartyBackup);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
    }
}
